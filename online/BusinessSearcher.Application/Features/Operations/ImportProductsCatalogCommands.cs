using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace BusinessSearcher.Application.Features.Operations.Products
{
    /// <summary>
    /// Importa un catálogo de productos (Excel) a un almacén / punto de venta.
    /// - El catálogo se identifica por su fecha: el mismo catálogo (misma fecha) no puede subirse dos veces al
    ///   mismo almacén. Sí puede subirse a otro almacén o con otra fecha.
    /// - Producto nuevo (código de barras que no existe en el negocio): se crea con su stock.
    /// - Producto existente (mismo código): no se duplica. Su stock en el almacén se SUMA. Si estaba desactivado
    ///   se reactiva.
    /// - Si el precio del archivo es distinto del precio del sistema, el usuario decide por producto:
    ///   "keep" mantiene el precio del sistema; "accept" usa el precio del archivo solo en este almacén.
    ///   Mientras falte alguna decisión no se escribe nada y se devuelven los conflictos.
    /// Todo-o-nada ante errores de archivo.
    /// </summary>
    /// <param name="ExchangeRate">Tasa USD→CUP para el precio en CUP. Si no se indica, se usa la última registrada.</param>
    /// <param name="CatalogDate">Fecha del catálogo (identifica el catálogo).</param>
    /// <param name="PriceDecisions">Código de barras → "keep" | "accept". Solo para los productos con conflicto de precio.</param>
    public record ImportProductsCatalogCommand(IFormFile File, Guid WarehouseId, decimal? ExchangeRate,
        DateOnly CatalogDate, IReadOnlyDictionary<string, string>? PriceDecisions)
        : IRequest<ImportProductsResultDto>;

    public class ImportProductsCatalogHandler : IRequestHandler<ImportProductsCatalogCommand, ImportProductsResultDto>
    {
        private const string Keep = "keep";
        private const string Accept = "accept";

        private readonly IExcelProductCatalogParser _parser;
        private readonly ICategoryRepository _categories;
        private readonly IProductRepository _products;
        private readonly IWarehouseRepository _warehouses;
        private readonly IExchangeRateRepository _rates;
        private readonly ICatalogImportRepository _catalogImports;
        private readonly IOperationsUnitOfWork _uow;
        private readonly ICurrentUserService _u;
        private readonly IFileStorageService _fileStorage;

        public ImportProductsCatalogHandler(
            IExcelProductCatalogParser parser, ICategoryRepository categories, IProductRepository products,
            IWarehouseRepository warehouses, IExchangeRateRepository rates, ICatalogImportRepository catalogImports,
            IOperationsUnitOfWork uow, ICurrentUserService u, IFileStorageService fileStorage)
        {
            _parser = parser; _categories = categories; _products = products; _warehouses = warehouses;
            _rates = rates; _catalogImports = catalogImports; _uow = uow; _u = u; _fileStorage = fileStorage;
        }

        public async Task<ImportProductsResultDto> Handle(ImportProductsCatalogCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);

            if (r.CatalogDate == default)
                throw new DomainException("Indica la fecha del catálogo.");

            var warehouse = await _warehouses.GetByIdAsync(t, r.WarehouseId, ct)
                ?? throw new DomainException("Almacén no encontrado.");

            if (r.ExchangeRate is <= 0)
                throw new DomainException("La tasa de cambio debe ser mayor que 0.");

            // Regla: el mismo catálogo no se sube dos veces al mismo almacén.
            var sameCatalog = await _catalogImports.GetByWarehouseAndDateAsync(t, warehouse.Id, r.CatalogDate, ct);
            if (sameCatalog is not null)
                throw new ConflictException(
                    $"El catálogo del {r.CatalogDate:dd/MM/yyyy} ya se subió a {warehouse.Name} el "
                    + $"{sameCatalog.CreatedAt:dd/MM/yyyy HH:mm}. No se puede subir dos veces al mismo punto de venta. "
                    + "Si es otro catálogo, indica su fecha.");

            var decisions = NormalizeDecisions(r.PriceDecisions);

            ExcelProductCatalogParseResult parsed;
            using (var stream = r.File.OpenReadStream())
                parsed = _parser.Parse(stream);

            if (!parsed.IsValid)
                throw new DomainException("El archivo tiene errores y no se importó nada. "
                    + string.Join(" ", parsed.Errors.Take(10)));

            var rate = r.ExchangeRate ?? (await _rates.GetLatestAsync(t, ct))?.Rate;

            // Busca cada producto y detecta conflictos de precio (solo precio, en el almacén de destino).
            var existingByBarcode = new Dictionary<string, Product?>(StringComparer.Ordinal);
            var conflicts = new List<PriceConflictDto>();
            foreach (var row in parsed.Rows)
            {
                var existing = await _products.GetByBarcodeAnyStateAsync(t, row.Barcode, ct);
                existingByBarcode[row.Barcode] = existing;
                if (existing is null) continue;

                var (_, sellUsd) = existing.PriceFor(warehouse.Id);
                if (sellUsd is not null && Cents(sellUsd.Value) != Cents(row.PriceUsd))
                    conflicts.Add(new PriceConflictDto(existing.Id, row.Barcode, existing.Name, sellUsd.Value, row.PriceUsd));
            }

            // Mientras falte alguna decisión de precio no se escribe nada.
            if (conflicts.Any(c => !decisions.ContainsKey(c.Barcode)))
                return new ImportProductsResultDto(NeedsDecision: true, PriceConflicts: conflicts,
                    CreatedCount: 0, AddedCount: 0, ReactivatedCount: 0, PricesUpdatedCount: 0, CategoriesCreatedCount: 0,
                    Products: new List<ImportedProductDto>(), Warnings: new List<string>());

            var conflictBarcodes = conflicts.Select(c => c.Barcode).ToHashSet(StringComparer.Ordinal);

            // Categorías: reutiliza las existentes (sin distinguir mayúsculas) y crea las que falten.
            var categoryIds = (await _categories.GetByTenantAsync(t, ct))
                .GroupBy(c => CategoryKey(c.Name))
                .ToDictionary(g => g.Key, g => g.First().Id);

            var categoriesCreated = 0;
            foreach (var name in parsed.Rows.Select(x => x.Category).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (categoryIds.ContainsKey(CategoryKey(name))) continue;
                var category = Category.Create(t, name, null, null);
                await _categories.AddAsync(category, ct);
                categoryIds[CategoryKey(name)] = category.Id;
                categoriesCreated++;
            }

            var results = new List<ImportedProductDto>();
            var pricesUpdated = 0;
            var imagesUploaded = 0;
            var imagesFailed = 0;
            foreach (var row in parsed.Rows)
            {
                var existing = existingByBarcode[row.Barcode];
                var imageUrl = await TryUploadRowImageAsync(t, row, ct, r => imagesUploaded++, r => imagesFailed++);

                if (existing is null)
                {
                    var sellPrice = rate is null ? 0m : Math.Round(row.PriceUsd * rate.Value, 2, MidpointRounding.AwayFromZero);
                    var product = Product.Create(t, row.Name, costPrice: 0m, sellPrice: sellPrice, barcode: row.Barcode,
                        description: row.Description, categoryId: categoryIds[CategoryKey(row.Category)],
                        sellPriceUsd: row.PriceUsd, imageUrl: imageUrl, isPubliclyVisible: false);

                    if (row.Stock > 0)
                        product.AdjustStock(warehouse.Id, row.Stock);

                    await _products.AddAsync(product, ct);
                    results.Add(new ImportedProductDto(row.Barcode, product.Id, "Creado", 0, row.Stock, null));
                    continue;
                }

                // No se pisa una foto ya cargada manualmente; solo se completa si el producto no tenía.
                if (imageUrl is not null && string.IsNullOrEmpty(existing.ImageUrl))
                {
                    existing.Update(existing.Name, existing.CostPrice, existing.SellPrice, existing.Barcode,
                        existing.Description, existing.Unit, existing.CategoryId, existing.CostPriceUSD,
                        existing.SellPriceUSD, existing.MinStock, existing.TaxRate, existing.BatchNumber,
                        existing.ExpirationDate, existing.ForSale, imageUrl, existing.MinOrderQuantity);
                }

                var previous = existing.Stocks.FirstOrDefault(s => s.WarehouseId == warehouse.Id)?.Quantity ?? 0;
                var reactivated = !existing.IsActive;
                if (reactivated)
                    existing.Activate();

                string? priceDecision = null;
                if (conflictBarcodes.Contains(row.Barcode))
                {
                    if (decisions[row.Barcode] == Accept)
                    {
                        var (currentCup, _) = existing.PriceFor(warehouse.Id);
                        var newCup = rate is null ? currentCup : Math.Round(row.PriceUsd * rate.Value, 2, MidpointRounding.AwayFromZero);
                        existing.SetPriceForWarehouse(warehouse.Id, newCup, row.PriceUsd);
                        pricesUpdated++;
                        priceDecision = "accepted";
                    }
                    else
                    {
                        priceDecision = "kept";
                    }
                }

                // Mismo producto en otro catálogo: el stock se suma al que ya había en este almacén.
                if (row.Stock > 0)
                    existing.AdjustStock(warehouse.Id, row.Stock);

                await _products.UpdateAsync(existing, ct);

                var status = reactivated ? "Reactivado" : row.Stock > 0 ? "Sumado" : "Sin stock";
                results.Add(new ImportedProductDto(row.Barcode, existing.Id, status, previous, previous + row.Stock, priceDecision));
            }

            await _catalogImports.AddAsync(CatalogImport.Create(t, warehouse.Id, r.CatalogDate,
                r.File.FileName, parsed.Rows.Count, _u.AccountId), ct);

            await _uow.SaveChangesAsync(ct);

            var created = results.Count(x => x.Status == "Creado");
            var warnings = new List<string>();
            if (rate is null && created > 0)
                warnings.Add("No hay tasa de cambio: el precio en CUP de los productos nuevos quedó en 0. Registra la tasa y actualiza los precios.");
            if (created > 0)
                warnings.Add("Los productos nuevos quedan ocultos en la búsqueda pública hasta que los publiques.");
            if (pricesUpdated > 0)
                warnings.Add($"Precio del archivo aplicado solo en {warehouse.Name} a {pricesUpdated} producto(s).");
            if (imagesFailed > 0)
                warnings.Add($"No se pudo subir la foto de {imagesFailed} producto(s) del archivo.");

            return new ImportProductsResultDto(
                NeedsDecision: false,
                PriceConflicts: new List<PriceConflictDto>(),
                CreatedCount: created,
                AddedCount: results.Count(x => x.Status == "Sumado"),
                ReactivatedCount: results.Count(x => x.Status == "Reactivado"),
                PricesUpdatedCount: pricesUpdated,
                CategoriesCreatedCount: categoriesCreated,
                Products: results,
                Warnings: warnings);
        }

        private static Dictionary<string, string> NormalizeDecisions(IReadOnlyDictionary<string, string>? raw)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (raw is null) return result;
            foreach (var (barcode, action) in raw)
            {
                var a = (action ?? string.Empty).Trim().ToLowerInvariant();
                if (a != Keep && a != Accept)
                    throw new DomainException($"Decisión de precio no válida para el código {barcode}: usa '{Keep}' o '{Accept}'.");
                result[barcode.Trim()] = a;
            }
            return result;
        }

        /// <summary>Sube la foto incrustada en la fila del Excel (si trae una) a Backblaze. Nunca aborta el
        /// import: si falla la subida de una foto puntual, esa fila sigue sin imagen y se avisa al final.</summary>
        private async Task<string?> TryUploadRowImageAsync(
            Guid tenantId, ProductCatalogRowDto row, CancellationToken ct, Action<ProductCatalogRowDto> onSuccess,
            Action<ProductCatalogRowDto> onFailure)
        {
            if (row.ImageBytes is null || row.ImageContentType is null) return null;
            try
            {
                using var ms = new MemoryStream(row.ImageBytes);
                var ext = row.ImageContentType switch { "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg" };
                var url = await _fileStorage.UploadAsync(
                    ms, $"{row.Barcode}{ext}", row.ImageContentType, $"products/{tenantId:N}", ct);
                onSuccess(row);
                return url;
            }
            catch
            {
                onFailure(row);
                return null;
            }
        }

        private static decimal Cents(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private static string CategoryKey(string name) => name.Trim().ToLowerInvariant();
    }
}
