using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Common;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace BusinessSearcher.Application.Features.StoreManagement.Commands.Catalog
{
    // ═══════════════════════════════════════════════════════════════
    // IMPORT CATALOG — reemplaza el catálogo visible públicamente (mayorista) del
    // negocio autenticado. Los productos privados (no publicados) no se tocan.
    // ═══════════════════════════════════════════════════════════════
    public record ImportWholesaleCatalogCommand(IFormFile File, string Currency)
        : IRequest<ImportCatalogResultDto>;

    public class ImportWholesaleCatalogCommandValidator : AbstractValidator<ImportWholesaleCatalogCommand>
    {
        public static readonly string[] AllowedCurrencies = { "CUP", "USD", "EUR", "MXN", "BRL" };

        public ImportWholesaleCatalogCommandValidator()
        {
            RuleFor(x => x.File)
                .NotNull().WithMessage("Debes adjuntar un archivo Excel (.xlsx).")
                .Must(f => f!.Length > 0).WithMessage("El archivo está vacío.")
                .Must(f => f!.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    .WithMessage("El archivo debe tener extensión .xlsx.");

            RuleFor(x => x.Currency)
                .NotEmpty().WithMessage("Debes indicar la moneda del catálogo.")
                .Must(c => AllowedCurrencies.Contains(c?.ToUpperInvariant()))
                    .WithMessage($"La moneda debe ser una de: {string.Join(", ", AllowedCurrencies)}.");
        }
    }

    public class ImportWholesaleCatalogCommandHandler
        : IRequestHandler<ImportWholesaleCatalogCommand, ImportCatalogResultDto>
    {
        private const string WholesaleCatalogCategoryName = "Catálogo Mayorista";

        private readonly IProductRepository    _productRepo;
        private readonly ICategoryRepository   _categoryRepo;
        private readonly IOperationsUnitOfWork _unitOfWork;
        private readonly ICurrentUserService   _currentUser;
        private readonly IExcelCatalogParser   _excelParser;

        public ImportWholesaleCatalogCommandHandler(
            IProductRepository    productRepo,
            ICategoryRepository   categoryRepo,
            IOperationsUnitOfWork unitOfWork,
            ICurrentUserService   currentUser,
            IExcelCatalogParser   excelParser)
        {
            _productRepo  = productRepo;
            _categoryRepo = categoryRepo;
            _unitOfWork   = unitOfWork;
            _currentUser  = currentUser;
            _excelParser  = excelParser;
        }

        public async Task<ImportCatalogResultDto> Handle(
            ImportWholesaleCatalogCommand request, CancellationToken cancellationToken)
        {
            var tenantId = _currentUser.TenantId;

            using var stream = request.File.OpenReadStream();
            var parseResult  = _excelParser.ParseWholesaleCatalog(stream, cancellationToken);

            if (!parseResult.IsValid)
                return new ImportCatalogResultDto(false, 0, parseResult.Errors);

            var currency = request.Currency.ToUpperInvariant();

            // Categoría "Catálogo Mayorista" (crea si no existe)
            var categories = await _categoryRepo.GetByTenantAsync(tenantId, cancellationToken);
            var category = categories.FirstOrDefault(c =>
                c.Name.Equals(WholesaleCatalogCategoryName, StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                category = Category.Create(tenantId, WholesaleCatalogCategoryName);
                await _categoryRepo.AddAsync(category, cancellationToken);
            }

            // Desactiva el catálogo mayorista previamente publicado (no toca inventario privado/POS)
            var existing = await _productRepo.GetByTenantAsync(
                tenantId, search: null, categoryId: category.Id, warehouseId: null, lowStockOnly: null, cancellationToken);
            foreach (var old in existing.Where(p => p.IsPubliclyVisible))
                old.Deactivate();

            var isUsd = currency == "USD";
            foreach (var row in parseResult.Rows)
            {
                var product = Product.Create(
                    tenantId, row.ProductName, costPrice: row.Price, sellPrice: row.Price,
                    categoryId: category.Id, forSale: true, isPubliclyVisible: true,
                    costPriceUsd: isUsd ? row.Price : null, sellPriceUsd: isUsd ? row.Price : null,
                    minOrderQuantity: row.MinOrderQuantity);
                await _productRepo.AddAsync(product, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ImportCatalogResultDto(true, parseResult.Rows.Count, Array.Empty<string>());
        }
    }
}
