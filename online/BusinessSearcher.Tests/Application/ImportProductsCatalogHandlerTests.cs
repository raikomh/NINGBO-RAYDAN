using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Application.Features.Operations.Products;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace BusinessSearcher.Tests.Application
{
    /// <summary>
    /// Reglas de importación de catálogos por fecha y punto de venta:
    /// - el mismo catálogo no se sube dos veces al mismo almacén;
    /// - el stock de un producto existente se suma;
    /// - un conflicto de precio exige decisión y el precio aceptado solo aplica a ese almacén.
    /// </summary>
    public class ImportProductsCatalogHandlerTests
    {
        private static readonly DateOnly Sep28 = new(2026, 9, 28);

        private readonly Guid _tenant = Guid.NewGuid();
        private readonly Guid _account = Guid.NewGuid();
        private readonly Mock<IExcelProductCatalogParser> _parser = new();
        private readonly Mock<ICategoryRepository> _categories = new();
        private readonly Mock<IProductRepository> _products = new();
        private readonly Mock<IWarehouseRepository> _warehouses = new();
        private readonly Mock<IExchangeRateRepository> _rates = new();
        private readonly Mock<ICatalogImportRepository> _imports = new();
        private readonly Mock<IOperationsUnitOfWork> _uow = new();
        private readonly Mock<ICurrentUserService> _user = new();
        private readonly Mock<IFileStorageService> _fileStorage = new();
        private readonly Warehouse _plaza;
        private readonly Warehouse _otra;

        public ImportProductsCatalogHandlerTests()
        {
            _plaza = Warehouse.Create(_tenant, "Plaza");
            _otra = Warehouse.Create(_tenant, "Otra");
            _user.Setup(u => u.TenantId).Returns(_tenant);
            _user.Setup(u => u.AccountId).Returns(_account);
            _warehouses.Setup(w => w.GetByIdAsync(_tenant, _plaza.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_plaza);
            _warehouses.Setup(w => w.GetByIdAsync(_tenant, _otra.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_otra);
            _categories.Setup(c => c.GetByTenantAsync(_tenant, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Category>());
            _imports.Setup(i => i.GetByWarehouseAndDateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>())).ReturnsAsync((CatalogImport?)null);
            _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        [Fact]
        public async Task SameCatalog_SameWarehouse_IsRejected()
        {
            _imports.Setup(i => i.GetByWarehouseAndDateAsync(_tenant, _plaza.Id, Sep28, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogImport.Create(_tenant, _plaza.Id, Sep28, "catalogo.xlsx", 1, _account));
            Catalog(Row("100", stock: 5, price: 6m));

            await Assert.ThrowsAsync<ConflictException>(() => Handler().Handle(Cmd(_plaza, Sep28), CancellationToken.None));
            _products.Verify(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SameCatalog_OtherWarehouse_IsAllowed()
        {
            _imports.Setup(i => i.GetByWarehouseAndDateAsync(_tenant, _plaza.Id, Sep28, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogImport.Create(_tenant, _plaza.Id, Sep28, "catalogo.xlsx", 1, _account));
            Catalog(Row("100", stock: 5, price: 6m));

            var result = await Handler().Handle(Cmd(_otra, Sep28), CancellationToken.None);

            Assert.False(result.NeedsDecision);
            Assert.Equal(1, result.CreatedCount);
        }

        [Fact]
        public async Task NewProduct_IsCreated_WithStock()
        {
            Catalog(Row("200", stock: 7, price: 3m));

            var result = await Handler().Handle(Cmd(_plaza, Sep28), CancellationToken.None);

            Assert.Equal(1, result.CreatedCount);
            _products.Verify(p => p.AddAsync(It.Is<Product>(x => x.Barcode == "200" && x.TotalStock == 7), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExistingProduct_DifferentCatalog_StockIsAdded_NotReplaced()
        {
            var existing = Existing("300", price: 5m, _plaza, stock: 10);
            Catalog(Row("300", stock: 5, price: 5m));

            var result = await Handler().Handle(Cmd(_plaza, Sep28), CancellationToken.None);

            Assert.Equal(15, existing.Stocks.First(s => s.WarehouseId == _plaza.Id).Quantity);
            Assert.Equal(1, result.AddedCount);
            Assert.Equal(0, result.CreatedCount);
            var line = Assert.Single(result.Products);
            Assert.Equal(10, line.PreviousStock);
            Assert.Equal(15, line.NewStock);
            Assert.Equal("Sumado", line.Status);
        }

        [Fact]
        public async Task PriceConflict_WithoutDecision_WritesNothing()
        {
            var existing = Existing("400", price: 5m, _plaza, stock: 10);
            Catalog(Row("400", stock: 5, price: 6m));

            var result = await Handler().Handle(Cmd(_plaza, Sep28), CancellationToken.None);

            Assert.True(result.NeedsDecision);
            var conflict = Assert.Single(result.PriceConflicts);
            Assert.Equal(5m, conflict.SystemPriceUsd);
            Assert.Equal(6m, conflict.ExcelPriceUsd);
            Assert.Equal(10, existing.Stocks.First(s => s.WarehouseId == _plaza.Id).Quantity);
            _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            _imports.Verify(i => i.AddAsync(It.IsAny<CatalogImport>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PriceConflict_Accepted_AppliesOnlyToThisWarehouse()
        {
            var existing = Existing("500", price: 5m, _plaza, stock: 10);
            existing.AdjustStock(_otra.Id, 3);
            Catalog(Row("500", stock: 5, price: 6m));

            var result = await Handler().Handle(Cmd(_plaza, Sep28, new() { ["500"] = "accept" }), CancellationToken.None);

            Assert.False(result.NeedsDecision);
            Assert.Equal(1, result.PricesUpdatedCount);
            Assert.Equal(6m, existing.PriceFor(_plaza.Id).SellPriceUSD);
            Assert.Equal(5m, existing.PriceFor(_otra.Id).SellPriceUSD);
            Assert.Equal(5m, existing.SellPriceUSD);
            Assert.Equal("accepted", Assert.Single(result.Products).PriceDecision);
        }

        [Fact]
        public async Task PriceConflict_Kept_KeepsSystemPrice_ButStillAddsStock()
        {
            var existing = Existing("600", price: 5m, _plaza, stock: 10);
            Catalog(Row("600", stock: 5, price: 6m));

            var result = await Handler().Handle(Cmd(_plaza, Sep28, new() { ["600"] = "keep" }), CancellationToken.None);

            Assert.Equal(0, result.PricesUpdatedCount);
            Assert.Equal(5m, existing.PriceFor(_plaza.Id).SellPriceUSD);
            Assert.Equal(15, existing.Stocks.First(s => s.WarehouseId == _plaza.Id).Quantity);
            Assert.Equal("kept", Assert.Single(result.Products).PriceDecision);
        }

        private ImportProductsCatalogHandler Handler() => new(_parser.Object, _categories.Object, _products.Object,
            _warehouses.Object, _rates.Object, _imports.Object, _uow.Object, _user.Object, _fileStorage.Object);

        private ImportProductsCatalogCommand Cmd(Warehouse wh, DateOnly date, Dictionary<string, string>? decisions = null)
            => new(FakeFile(), wh.Id, 120m, date, decisions);

        private void Catalog(params ProductCatalogRowDto[] rows)
            => _parser.Setup(p => p.Parse(It.IsAny<Stream>()))
                .Returns(new ExcelProductCatalogParseResult(true, rows, Array.Empty<string>()));

        private static ProductCatalogRowDto Row(string barcode, int stock, decimal price)
            => new(RowNumber: 2, Barcode: barcode, Name: "Producto " + barcode, Category: "General",
                Stock: stock, PriceUsd: price, Description: null);

        private Product Existing(string barcode, decimal price, Warehouse wh, int stock)
        {
            var p = Product.Create(_tenant, "Producto " + barcode, 0m, price * 120m, barcode: barcode,
                sellPriceUsd: price, isPubliclyVisible: true);
            if (stock > 0) p.AdjustStock(wh.Id, stock);
            _products.Setup(x => x.GetByBarcodeAnyStateAsync(_tenant, barcode, It.IsAny<CancellationToken>())).ReturnsAsync(p);
            return p;
        }

        private static IFormFile FakeFile()
        {
            var f = new Mock<IFormFile>();
            f.Setup(x => x.OpenReadStream()).Returns(() => new MemoryStream());
            f.Setup(x => x.FileName).Returns("catalogo.xlsx");
            return f.Object;
        }
    }
}
