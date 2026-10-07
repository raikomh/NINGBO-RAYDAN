using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace BusinessSearcher.Application.Features.Operations
{
    internal static class CatalogMapper
    {
        public static CategoryDto ToDto(Category c) => new(c.Id, c.Name, c.Description, c.Code);

        public static ProductStockDto ToDto(ProductStock s) => new(s.WarehouseId, s.Quantity, s.AverageCost, s.AverageCostUSD,
            s.SellPrice, s.SellPriceUSD);

        public static ProductDto ToDto(Product p) => new(
            p.Id, p.Barcode, p.Name, p.Description, p.Unit, p.CategoryId, p.CostPrice, p.SellPrice,
            p.CostPriceUSD, p.SellPriceUSD, p.MinStock, p.TaxRate, p.BatchNumber, p.ExpirationDate, p.ForSale,
            p.TotalStock, p.Stocks.Select(ToDto).ToList(),
            p.ImageUrl, p.IsPubliclyVisible, p.MinOrderQuantity);

        public static ProductPriceHistoryDto ToDto(ProductPriceHistory h) => new(
            h.Id, h.ProductId, h.OldCostPrice, h.NewCostPrice, h.OldSellPrice, h.NewSellPrice,
            h.OldCostPriceUSD, h.NewCostPriceUSD, h.OldSellPriceUSD, h.NewSellPriceUSD, h.ChangeDate, h.UserId, h.Reason);
    }
}

// ── Categorías ──
namespace BusinessSearcher.Application.Features.Operations.Categories
{
    public record CreateCategoryCommand(CreateCategoryDto Dto) : IRequest<CategoryDto>;
    public record UpdateCategoryCommand(Guid Id, CreateCategoryDto Dto) : IRequest<CategoryDto>;
    public record DeleteCategoryCommand(Guid Id) : IRequest;
    public record GetCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;

    public class CreateCategoryHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
    {
        private readonly ICategoryRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CreateCategoryHandler(ICategoryRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<CategoryDto> Handle(CreateCategoryCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var c = Category.Create(t, d.Name, d.Description, d.Code);
            await _repo.AddAsync(c, ct); await _uow.SaveChangesAsync(ct);
            return CatalogMapper.ToDto(c);
        }
    }

    public class UpdateCategoryHandler : IRequestHandler<UpdateCategoryCommand, CategoryDto>
    {
        private readonly ICategoryRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public UpdateCategoryHandler(ICategoryRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<CategoryDto> Handle(UpdateCategoryCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var c = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Categoría no encontrada.");
            c.Update(d.Name, d.Description, d.Code);
            await _repo.UpdateAsync(c, ct); await _uow.SaveChangesAsync(ct);
            return CatalogMapper.ToDto(c);
        }
    }

    public class DeleteCategoryHandler : IRequestHandler<DeleteCategoryCommand>
    {
        private readonly ICategoryRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public DeleteCategoryHandler(ICategoryRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task Handle(DeleteCategoryCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var c = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Categoría no encontrada.");
            c.Deactivate(); await _repo.UpdateAsync(c, ct); await _uow.SaveChangesAsync(ct);
        }
    }

    public class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
    {
        private readonly ICategoryRepository _repo; private readonly ICurrentUserService _u;
        public GetCategoriesHandler(ICategoryRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<CategoryDto>> Handle(GetCategoriesQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, ct)).Select(CatalogMapper.ToDto).ToList();
        }
    }
}

// ── Productos ──
namespace BusinessSearcher.Application.Features.Operations.Products
{
    /// <summary>Un código de barras pertenece a un solo producto del negocio, esté activo o desactivado.</summary>
    internal static class ProductBarcodeRules
    {
        public static async Task EnsureAvailableAsync(IProductRepository repo, Guid tenantId, string? barcode,
            Guid? exceptProductId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;
            var code = barcode.Trim();
            var existing = await repo.GetByBarcodeAnyStateAsync(tenantId, code, ct);
            if (existing is not null && existing.Id != exceptProductId)
                throw new DomainException(existing.IsActive
                    ? $"Ya existe un producto con el código {code} ({existing.Name})."
                    : $"Ya existe un producto desactivado con el código {code} ({existing.Name}). Vuelve a importarlo para reactivarlo.");
        }
    }

    public record CreateProductCommand(CreateProductDto Dto) : IRequest<ProductDto>;
    public record UpdateProductCommand(Guid Id, CreateProductDto Dto) : IRequest<ProductDto>;
    public record DeleteProductCommand(Guid Id) : IRequest;
    public record AdjustProductStockCommand(Guid Id, AdjustStockDto Dto) : IRequest<ProductDto>;
    public record SetProductPublicVisibilityCommand(Guid Id, SetProductPublicVisibilityDto Dto) : IRequest<ProductDto>;
    public record GetProductsQuery(string? Search, Guid? CategoryId, Guid? WarehouseId, bool? LowStockOnly) : IRequest<IReadOnlyList<ProductDto>>;
    public record GetProductByIdQuery(Guid Id) : IRequest<ProductDto>;
    public record GetProductByBarcodeQuery(string Barcode) : IRequest<ProductDto?>;
    public record GetProductPriceHistoryQuery(Guid ProductId) : IRequest<IReadOnlyList<ProductPriceHistoryDto>>;

    public class CreateProductHandler : IRequestHandler<CreateProductCommand, ProductDto>
    {
        private readonly IProductRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CreateProductHandler(IProductRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<ProductDto> Handle(CreateProductCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            await ProductBarcodeRules.EnsureAvailableAsync(_repo, t, d.Barcode, null, ct);
            var p = Product.Create(t, d.Name, d.CostPrice, d.SellPrice, d.Barcode, d.Description, d.Unit, d.CategoryId,
                d.CostPriceUSD, d.SellPriceUSD, d.MinStock, d.TaxRate, d.BatchNumber, d.ExpirationDate, d.ForSale,
                d.ImageUrl, isPubliclyVisible: d.IsPubliclyVisible, minOrderQuantity: d.MinOrderQuantity);
            if (d.InitialWarehouseId.HasValue && d.InitialStock > 0)
                p.AdjustStock(d.InitialWarehouseId.Value, d.InitialStock);
            await _repo.AddAsync(p, ct); await _uow.SaveChangesAsync(ct);
            return CatalogMapper.ToDto(p);
        }
    }

    public class UpdateProductHandler : IRequestHandler<UpdateProductCommand, ProductDto>
    {
        private readonly IProductRepository _repo; private readonly IProductPriceHistoryRepository _hist;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public UpdateProductHandler(IProductRepository repo, IProductPriceHistoryRepository hist, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _repo = repo; _hist = hist; _uow = uow; _u = u; }
        public async Task<ProductDto> Handle(UpdateProductCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var p = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Producto no encontrado.");

            await ProductBarcodeRules.EnsureAvailableAsync(_repo, t, d.Barcode, r.Id, ct);
            var priceChanged = p.CostPrice != d.CostPrice || p.SellPrice != d.SellPrice
                || p.CostPriceUSD != d.CostPriceUSD || p.SellPriceUSD != d.SellPriceUSD;
            var oldCost = p.CostPrice; var oldSell = p.SellPrice; var oldCostUsd = p.CostPriceUSD; var oldSellUsd = p.SellPriceUSD;

            p.Update(d.Name, d.CostPrice, d.SellPrice, d.Barcode, d.Description, d.Unit, d.CategoryId,
                d.CostPriceUSD, d.SellPriceUSD, d.MinStock, d.TaxRate, d.BatchNumber, d.ExpirationDate, d.ForSale,
                d.ImageUrl, d.MinOrderQuantity);
            await _repo.UpdateAsync(p, ct);

            if (priceChanged)
                await _hist.AddAsync(ProductPriceHistory.Create(t, p.Id, oldCost, d.CostPrice, oldSell, d.SellPrice,
                    oldCostUsd, d.CostPriceUSD, oldSellUsd, d.SellPriceUSD, _u.AccountId, d.PriceChangeReason), ct);

            await _uow.SaveChangesAsync(ct);
            return CatalogMapper.ToDto(p);
        }
    }

    public class DeleteProductHandler : IRequestHandler<DeleteProductCommand>
    {
        private readonly IProductRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public DeleteProductHandler(IProductRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task Handle(DeleteProductCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var p = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Producto no encontrado.");
            p.Deactivate(); await _repo.UpdateAsync(p, ct); await _uow.SaveChangesAsync(ct);
        }
    }

    public class AdjustProductStockHandler : IRequestHandler<AdjustProductStockCommand, ProductDto>
    {
        private readonly IProductRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public AdjustProductStockHandler(IProductRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<ProductDto> Handle(AdjustProductStockCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var p = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Producto no encontrado.");
            p.AdjustStock(r.Dto.WarehouseId, r.Dto.Delta);
            await _repo.UpdateAsync(p, ct); await _uow.SaveChangesAsync(ct);
            return CatalogMapper.ToDto(p);
        }
    }

    public class SetProductPublicVisibilityHandler : IRequestHandler<SetProductPublicVisibilityCommand, ProductDto>
    {
        private readonly IProductRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public SetProductPublicVisibilityHandler(IProductRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<ProductDto> Handle(SetProductPublicVisibilityCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var p = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Producto no encontrado.");
            p.SetPublicVisibility(r.Dto.IsPubliclyVisible);
            await _repo.UpdateAsync(p, ct); await _uow.SaveChangesAsync(ct);
            return CatalogMapper.ToDto(p);
        }
    }

    public class GetProductsHandler : IRequestHandler<GetProductsQuery, IReadOnlyList<ProductDto>>
    {
        private readonly IProductRepository _repo; private readonly ICurrentUserService _u;
        public GetProductsHandler(IProductRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<ProductDto>> Handle(GetProductsQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, r.Search, r.CategoryId, r.WarehouseId, r.LowStockOnly, ct))
                .Select(CatalogMapper.ToDto).ToList();
        }
    }

    public class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, ProductDto>
    {
        private readonly IProductRepository _repo; private readonly ICurrentUserService _u;
        public GetProductByIdHandler(IProductRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<ProductDto> Handle(GetProductByIdQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var p = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Producto no encontrado.");
            return CatalogMapper.ToDto(p);
        }
    }

    public class GetProductByBarcodeHandler : IRequestHandler<GetProductByBarcodeQuery, ProductDto?>
    {
        private readonly IProductRepository _repo; private readonly ICurrentUserService _u;
        public GetProductByBarcodeHandler(IProductRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<ProductDto?> Handle(GetProductByBarcodeQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var p = await _repo.GetByBarcodeAsync(t, r.Barcode, ct);
            return p is null ? null : CatalogMapper.ToDto(p);
        }
    }

    public class GetProductPriceHistoryHandler : IRequestHandler<GetProductPriceHistoryQuery, IReadOnlyList<ProductPriceHistoryDto>>
    {
        private readonly IProductPriceHistoryRepository _repo; private readonly ICurrentUserService _u;
        public GetProductPriceHistoryHandler(IProductPriceHistoryRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<ProductPriceHistoryDto>> Handle(GetProductPriceHistoryQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByProductAsync(t, r.ProductId, ct)).Select(CatalogMapper.ToDto).ToList();
        }
    }

    // ── Subida de foto de producto ──
    public record UploadProductImageCommand(Guid ProductId, IFormFile File) : IRequest<string>;

    public class UploadProductImageCommandValidator : AbstractValidator<UploadProductImageCommand>
    {
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

        public UploadProductImageCommandValidator()
        {
            RuleFor(x => x.ProductId).NotEmpty();
            RuleFor(x => x.File)
                .NotNull().WithMessage("El archivo es requerido.")
                .Must(f => f.Length > 0).WithMessage("El archivo no puede estar vacío.")
                .Must(f => f.Length <= MaxFileSizeBytes).WithMessage("El archivo no puede superar 5MB.")
                .Must(f => new[] { "image/jpeg", "image/png", "image/webp", "image/jpg" }
                    .Contains(f.ContentType.ToLowerInvariant()))
                    .WithMessage("Solo se permiten imágenes JPEG, PNG o WEBP.");
        }
    }

    public class UploadProductImageCommandHandler : IRequestHandler<UploadProductImageCommand, string>
    {
        private readonly IProductRepository _repo; private readonly IOperationsUnitOfWork _uow;
        private readonly ICurrentUserService _u; private readonly IFileStorageService _fileStorage;

        public UploadProductImageCommandHandler(
            IProductRepository repo, IOperationsUnitOfWork uow,
            ICurrentUserService u, IFileStorageService fileStorage)
        { _repo = repo; _uow = uow; _u = u; _fileStorage = fileStorage; }

        public async Task<string> Handle(UploadProductImageCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var p = await _repo.GetByIdAsync(t, r.ProductId, ct) ?? throw new DomainException("Producto no encontrado.");

            await using var stream = r.File.OpenReadStream();
            var imageUrl = await _fileStorage.UploadAsync(
                stream, r.File.FileName, r.File.ContentType, $"products/{t:N}", ct);

            if (!string.IsNullOrEmpty(p.ImageUrl))
            {
                try { await _fileStorage.DeleteAsync(p.ImageUrl, ct); }
                catch { /* No abortar si falla la eliminación de la imagen anterior */ }
            }

            p.Update(p.Name, p.CostPrice, p.SellPrice, p.Barcode, p.Description, p.Unit, p.CategoryId,
                p.CostPriceUSD, p.SellPriceUSD, p.MinStock, p.TaxRate, p.BatchNumber, p.ExpirationDate, p.ForSale,
                imageUrl, p.MinOrderQuantity);

            await _repo.UpdateAsync(p, ct); await _uow.SaveChangesAsync(ct);
            return imageUrl;
        }
    }

    // ── Feed de notificaciones operativas (stock bajo + cambios de precio recientes) ──
    internal static class OperationalNotificationsBuilder
    {
        public static async Task<List<OperationalNotificationDto>> BuildAsync(
            IProductRepository products, IProductPriceHistoryRepository priceHistory,
            Guid tenantId, int lowStockLimit, int priceChangeHours, CancellationToken ct)
        {
            var notifications = new List<OperationalNotificationDto>();

            var lowStock = (await products.GetByTenantAsync(tenantId, null, null, null, true, ct)).Take(lowStockLimit);
            foreach (var p in lowStock)
            {
                // Fecha del último cambio de STOCK (no de cualquier edición del producto): así,
                // editar nombre/precio/etc. de un producto ya en stock bajo no revive la alerta.
                var stockChangedAt = p.Stocks.Count > 0
                    ? p.Stocks.Max(s => s.UpdatedAt ?? s.CreatedAt)
                    : p.CreatedAt;
                notifications.Add(new OperationalNotificationDto("LowStock", $"Stock bajo: {p.Name}",
                    $"Quedan {p.TotalStock} unidades (mínimo {p.MinStock}).", stockChangedAt, p.Id));
            }

            var since = DateTime.UtcNow.AddHours(-priceChangeHours);
            var priceChanges = await priceHistory.GetRecentAsync(tenantId, since, ct);
            var productNames = new Dictionary<Guid, string>();
            foreach (var id in priceChanges.Select(h => h.ProductId).Distinct())
            {
                var p = await products.GetByIdAsync(tenantId, id, ct);
                if (p is not null) productNames[id] = p.Name;
            }
            foreach (var h in priceChanges)
            {
                var name = productNames.TryGetValue(h.ProductId, out var n) ? n : "Producto eliminado";
                notifications.Add(new OperationalNotificationDto("PriceChange", $"Cambio de precio: {name}",
                    $"Precio de venta: {h.OldSellPrice:0.##} → {h.NewSellPrice:0.##}", h.ChangeDate, h.ProductId));
            }

            return notifications.OrderByDescending(n => n.Date).ToList();
        }

        /// <summary>Clave de configuración (tabla genérica Settings) donde se guarda la última vez
        /// que la cuenta autenticada revisó el feed de notificaciones.</summary>
        public static string SeenAtKey(Guid accountId) => $"ops.notifications.seenAt.{accountId:N}";
    }

    public record GetOperationalNotificationsQuery(int LowStockLimit = 50, int PriceChangeHours = 24)
        : IRequest<IReadOnlyList<OperationalNotificationDto>>;

    public class GetOperationalNotificationsHandler
        : IRequestHandler<GetOperationalNotificationsQuery, IReadOnlyList<OperationalNotificationDto>>
    {
        private readonly IProductRepository _products;
        private readonly IProductPriceHistoryRepository _priceHistory;
        private readonly ICurrentUserService _u;
        public GetOperationalNotificationsHandler(
            IProductRepository products, IProductPriceHistoryRepository priceHistory, ICurrentUserService u)
        { _products = products; _priceHistory = priceHistory; _u = u; }

        public Task<IReadOnlyList<OperationalNotificationDto>> Handle(GetOperationalNotificationsQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return BuildAsync(t, r, ct);
        }

        private async Task<IReadOnlyList<OperationalNotificationDto>> BuildAsync(
            Guid t, GetOperationalNotificationsQuery r, CancellationToken ct)
            => await OperationalNotificationsBuilder.BuildAsync(_products, _priceHistory, t, r.LowStockLimit, r.PriceChangeHours, ct);
    }

    // ── Cantidad de notificaciones no vistas por la cuenta autenticada ──
    public record GetNotificationsUnreadCountQuery(int LowStockLimit = 50, int PriceChangeHours = 24)
        : IRequest<int>;

    public class GetNotificationsUnreadCountHandler : IRequestHandler<GetNotificationsUnreadCountQuery, int>
    {
        private readonly IProductRepository _products;
        private readonly IProductPriceHistoryRepository _priceHistory;
        private readonly ISettingRepository _settings;
        private readonly ICurrentUserService _u;
        public GetNotificationsUnreadCountHandler(IProductRepository products, IProductPriceHistoryRepository priceHistory,
            ISettingRepository settings, ICurrentUserService u)
        { _products = products; _priceHistory = priceHistory; _settings = settings; _u = u; }

        public async Task<int> Handle(GetNotificationsUnreadCountQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var notifications = await OperationalNotificationsBuilder.BuildAsync(
                _products, _priceHistory, t, r.LowStockLimit, r.PriceChangeHours, ct);

            var marker = await _settings.GetByKeyAsync(t, OperationalNotificationsBuilder.SeenAtKey(_u.AccountId), ct);
            var seenAt = marker is not null && DateTime.TryParse(marker.Value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed) ? parsed : DateTime.MinValue;

            return notifications.Count(n => n.Date > seenAt);
        }
    }

    // ── Marca el feed de notificaciones como visto por la cuenta autenticada ──
    public record MarkNotificationsSeenCommand : IRequest<Unit>;

    public class MarkNotificationsSeenHandler : IRequestHandler<MarkNotificationsSeenCommand, Unit>
    {
        private readonly ISettingRepository _settings;
        private readonly IOperationsUnitOfWork _uow;
        private readonly ICurrentUserService _u;
        public MarkNotificationsSeenHandler(ISettingRepository settings, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _settings = settings; _uow = uow; _u = u; }

        public async Task<Unit> Handle(MarkNotificationsSeenCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var key = OperationalNotificationsBuilder.SeenAtKey(_u.AccountId);
            var now = DateTime.UtcNow.ToString("O");

            var existing = await _settings.GetByKeyAsync(t, key, ct);
            if (existing is null) await _settings.AddAsync(Setting.Create(t, key, now), ct);
            else { existing.UpdateValue(now); await _settings.UpdateAsync(existing, ct); }

            await _uow.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}
