using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace BusinessSearcher.Application.Features.Operations
{
    internal static class SalesMapper
    {
        public static SaleItemDto ToDto(SaleItem i) => new(
            i.ProductId, i.ProductName, i.WarehouseId, i.Quantity, i.UnitPrice,
            i.DiscountType.ToString(), i.DiscountValue, i.LineDiscount, i.LineTotal);

        public static SalePaymentDto ToDto(SalePayment p) => new(
            p.Method.ToString(), p.Amount, p.Currency.ToString(), p.AmountUSD, p.TransactionId,
            p.CashTendered, p.Change, p.ChangeCurrency?.ToString());

        public static SaleDto ToDto(Sale s) => new(
            s.Id, s.Date, s.Subtotal, s.Discount, s.Total, s.SubtotalUSD, s.TotalUSD, s.TaxAmount,
            s.PaymentMethod.ToString(), s.PaymentCurrency.ToString(), s.ExchangeRate, s.Status.ToString(),
            s.CashierId, s.RegisterId, s.WarehouseName,
            s.Items.Select(ToDto).ToList(), s.Payments.Select(ToDto).ToList(), null, s.ManagerCode);

        /// <summary>El cajero de una venta es un OperationsUser, o el dueño del negocio (Tenant) si vendió
        /// él mismo desde su propia sesión: no tiene fila en OperationsUser, así que no está en <paramref name="workerNames"/>.</summary>
        public static SaleDto WithCashierName(SaleDto dto, IReadOnlyDictionary<Guid, string> workerNames)
            => dto with { CashierName = workerNames.TryGetValue(dto.CashierId, out var name) ? name : "Dueño del negocio" };

        public static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
            => Enum.TryParse<TEnum>(value, true, out var e) ? e : fallback;
    }
}

namespace BusinessSearcher.Application.Features.Operations.Sales
{
    public record CreateSaleCommand(CreateSaleDto Dto) : IRequest<SaleDto>;
    public record UpdateSaleCommand(Guid Id, UpdateSaleDto Dto) : IRequest<SaleDto>;
    public record RefundSaleCommand(Guid Id) : IRequest<SaleDto>;
    public record GetSalesQuery(DateTime? From, DateTime? To, Guid? RegisterId, Guid? CashierId, Guid? WarehouseId = null) : IRequest<IReadOnlyList<SaleDto>>;
    public record GetSaleByIdQuery(Guid Id) : IRequest<SaleDto>;

    /// <summary>
    /// Importa ventas desde un Excel (Código, Producto, Cantidad, Precio): cada fila crea una venta
    /// de un solo producto que descuenta stock real y entra a la caja abierta del usuario actual.
    /// Si algún precio del Excel difiere del precio del sistema y <see cref="AcceptNewPrices"/> viene
    /// null, no registra nada todavía: devuelve las diferencias para que el cliente confirme.
    /// </summary>
    public record ImportSalesCommand(IFormFile File, bool? AcceptNewPrices) : IRequest<ImportSalesResultDto>;

    public class ImportSalesCommandValidator : AbstractValidator<ImportSalesCommand>
    {
        public ImportSalesCommandValidator()
        {
            RuleFor(x => x.File)
                .NotNull().WithMessage("Debes adjuntar un archivo Excel (.xlsx).")
                .Must(f => f!.Length > 0).WithMessage("El archivo está vacío.")
                .Must(f => f!.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    .WithMessage("El archivo debe tener extensión .xlsx.");
        }
    }

    public class CreateSaleHandler : IRequestHandler<CreateSaleCommand, SaleDto>
    {
        private readonly ISaleRepository _sales; private readonly IProductRepository _products;
        private readonly ICashRegisterRepository _registers; private readonly ICashMovementRepository _movements;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        private readonly IManagerRepository _managers; private readonly IWarehouseRepository _warehouses;
        public CreateSaleHandler(ISaleRepository sales, IProductRepository products, ICashRegisterRepository registers,
            ICashMovementRepository movements, IOperationsUnitOfWork uow, ICurrentUserService u, IManagerRepository managers,
            IWarehouseRepository warehouses)
        { _sales = sales; _products = products; _registers = registers; _movements = movements; _uow = uow; _u = u; _managers = managers; _warehouses = warehouses; }

        public async Task<SaleDto> Handle(CreateSaleCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            if (d.Items is null || d.Items.Count == 0) throw new DomainException("La venta debe tener al menos un producto.");

            var currency = SalesMapper.ParseEnum(d.PaymentCurrency, Currency.CUP);
            var method   = SalesMapper.ParseEnum(d.PaymentMethod, PaymentMethod.Cash);

            string? managerCode = null;
            if (!string.IsNullOrWhiteSpace(d.ManagerCode))
            {
                var manager = await _managers.GetByCodeAsync(t, d.ManagerCode, ct)
                              ?? throw new DomainException($"No existe un gestor con el código {d.ManagerCode.Trim()}.");
                if (!manager.IsActive) throw new DomainException($"El gestor {manager.Code} está inactivo.");
                managerCode = manager.Code;
            }

            var saleItems = new List<SaleItem>();
            var productsTouched = new List<Product>();
            var stockAdjustments = new List<(Product Product, Guid WarehouseId, int Delta)>();
            foreach (var it in d.Items)
            {
                var p = await _products.GetByIdAsync(t, it.ProductId, ct)
                        ?? throw new DomainException($"Producto {it.ProductId} no encontrado.");
                var (sellCup, sellUsd) = p.PriceFor(it.WarehouseId); // precio del punto de venta (almacén)
                var unitPrice = it.UnitPriceOverride
                    ?? (currency == Currency.USD && sellUsd.HasValue ? sellUsd.Value : sellCup);
                var discType  = SalesMapper.ParseEnum(it.DiscountType, DiscountType.Amount);

                p.AdjustStock(it.WarehouseId, -it.Quantity); // descuenta stock (valida disponibilidad)
                stockAdjustments.Add((p, it.WarehouseId, -it.Quantity));
                productsTouched.Add(p);
                saleItems.Add(SaleItem.Create(p.Id, p.Name, it.WarehouseId, it.Quantity, unitPrice, discType, it.DiscountValue));
            }

            var payments = (d.Payments ?? new List<CreateSalePaymentDto>()).Select(p => SalePayment.Create(
                SalesMapper.ParseEnum(p.Method, PaymentMethod.Cash), p.Amount,
                SalesMapper.ParseEnum(p.Currency, Currency.CUP), p.AmountUSD, p.TransactionId,
                p.CashTendered, p.Change, p.ChangeCurrency is null ? null : SalesMapper.ParseEnum<Currency>(p.ChangeCurrency, Currency.CUP)))
                .ToList();

            var warehouseName = (await _warehouses.GetByIdAsync(t, saleItems[0].WarehouseId, ct))?.Name;
            var sale = Sale.Create(t, _u.AccountId, d.RegisterId, method, currency, saleItems, payments,
                d.ExchangeRate, d.TaxAmount, warehouseName, managerCode);

            foreach (var p in productsTouched) await _products.UpdateAsync(p, ct);
            await _sales.AddAsync(sale, ct);

            // Impacto en caja: efectivo en CUP que entra a la gaveta
            var register = await _registers.GetByIdAsync(t, d.RegisterId, ct);
            if (register is not null && register.Status == CashRegisterStatus.Open)
            {
                var cashIn = payments.Where(p => p.Method == PaymentMethod.Cash && p.Currency == Currency.CUP).Sum(p => p.Amount);
                register.RegisterSale(cashIn);
                await _registers.UpdateAsync(register, ct);
            }

            await OpsMapper.SaveWithStockRetryAsync(_uow, stockAdjustments, ct);
            return SalesMapper.ToDto(sale);
        }
    }

    public class UpdateSaleHandler : IRequestHandler<UpdateSaleCommand, SaleDto>
    {
        private readonly ISaleRepository _sales; private readonly IProductRepository _products;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public UpdateSaleHandler(ISaleRepository sales, IProductRepository products, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _sales = sales; _products = products; _uow = uow; _u = u; }

        public async Task<SaleDto> Handle(UpdateSaleCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            if (d.Items is null || d.Items.Count == 0) throw new DomainException("La venta debe tener al menos un producto.");

            var sale = await _sales.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Venta no encontrada.");
            if (sale.Status == SaleStatus.Refunded) throw new DomainException("No se puede editar una venta reembolsada.");

            var touched = new Dictionary<Guid, Product>();
            async Task<Product> GetOrLoad(Guid productId)
            {
                if (touched.TryGetValue(productId, out var cached)) return cached;
                var p = await _products.GetByIdAsync(t, productId, ct) ?? throw new DomainException($"Producto {productId} no encontrado.");
                touched[productId] = p;
                return p;
            }

            var stockAdjustments = new List<(Product Product, Guid WarehouseId, int Delta)>();

            // Restaura el stock de los renglones anteriores; se vuelve a descontar abajo con los nuevos.
            foreach (var oldItem in sale.Items)
            {
                var p = await GetOrLoad(oldItem.ProductId);
                p.AdjustStock(oldItem.WarehouseId, oldItem.Quantity);
                stockAdjustments.Add((p, oldItem.WarehouseId, oldItem.Quantity));
            }

            var newItems = new List<SaleItem>();
            foreach (var it in d.Items)
            {
                var p = await GetOrLoad(it.ProductId);
                var (sellCup, sellUsd) = p.PriceFor(it.WarehouseId); // precio del punto de venta (almacén)
                var unitPrice = it.UnitPriceOverride
                    ?? (sale.PaymentCurrency == Currency.USD && sellUsd.HasValue ? sellUsd.Value : sellCup);
                var discType = SalesMapper.ParseEnum(it.DiscountType, DiscountType.Amount);
                p.AdjustStock(it.WarehouseId, -it.Quantity);
                stockAdjustments.Add((p, it.WarehouseId, -it.Quantity));
                newItems.Add(SaleItem.Create(p.Id, p.Name, it.WarehouseId, it.Quantity, unitPrice, discType, it.DiscountValue));
            }

            foreach (var p in touched.Values) await _products.UpdateAsync(p, ct);

            sale.UpdateItems(newItems);
            await _sales.UpdateAsync(sale, ct);
            await OpsMapper.SaveWithStockRetryAsync(_uow, stockAdjustments, ct);
            return SalesMapper.ToDto(sale);
        }
    }

    public class RefundSaleHandler : IRequestHandler<RefundSaleCommand, SaleDto>
    {
        private readonly ISaleRepository _sales; private readonly IProductRepository _products;
        private readonly ICashRegisterRepository _registers; private readonly ICashMovementRepository _movements;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public RefundSaleHandler(ISaleRepository sales, IProductRepository products, ICashRegisterRepository registers,
            ICashMovementRepository movements, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _sales = sales; _products = products; _registers = registers; _movements = movements; _uow = uow; _u = u; }

        public async Task<SaleDto> Handle(RefundSaleCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var sale = await _sales.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Venta no encontrada.");
            sale.MarkRefunded();

            var stockAdjustments = new List<(Product Product, Guid WarehouseId, int Delta)>();
            foreach (var it in sale.Items)
            {
                var p = await _products.GetByIdAsync(t, it.ProductId, ct);
                if (p is not null)
                {
                    p.AdjustStock(it.WarehouseId, it.Quantity);
                    stockAdjustments.Add((p, it.WarehouseId, it.Quantity));
                    await _products.UpdateAsync(p, ct);
                }
            }
            await _sales.UpdateAsync(sale, ct);

            var register = await _registers.GetByIdAsync(t, sale.RegisterId, ct);
            if (register is not null && register.Status == CashRegisterStatus.Open)
            {
                // Solo el efectivo sale de la gaveta: una venta con tarjeta o transferencia no mueve caja.
                var cashCup = sale.Payments.Where(p => p.Method == PaymentMethod.Cash && p.Currency == Currency.CUP).Sum(p => p.Amount);
                var cashUsd = sale.Payments.Where(p => p.Method == PaymentMethod.Cash && p.Currency == Currency.USD).Sum(p => p.Amount);
                if (cashCup > 0)
                {
                    register.RegisterCashMovement(CashMovementType.Refund, cashCup);
                    await _movements.AddAsync(CashMovement.Create(t, register.Id, CashMovementType.Refund, cashCup,
                        Currency.CUP, $"Reembolso de venta {sale.Id}", userId: _u.AccountId), ct);
                }
                if (cashUsd > 0)
                {
                    // Informativo: el cuadre de caja solo se calcula en CUP.
                    await _movements.AddAsync(CashMovement.Create(t, register.Id, CashMovementType.Refund, cashUsd,
                        Currency.USD, $"Reembolso USD de venta {sale.Id}", userId: _u.AccountId), ct);
                }
                await _registers.UpdateAsync(register, ct);
            }

            await OpsMapper.SaveWithStockRetryAsync(_uow, stockAdjustments, ct);
            return SalesMapper.ToDto(sale);
        }
    }

    public class GetSalesHandler : IRequestHandler<GetSalesQuery, IReadOnlyList<SaleDto>>
    {
        private readonly ISaleRepository _repo; private readonly IOperationsUserRepository _users; private readonly ICurrentUserService _u;
        public GetSalesHandler(ISaleRepository repo, IOperationsUserRepository users, ICurrentUserService u) { _repo = repo; _users = users; _u = u; }
        public async Task<IReadOnlyList<SaleDto>> Handle(GetSalesQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var sales = await _repo.GetByTenantAsync(t, r.From, OpsMapper.EndOfDay(r.To), r.RegisterId, r.CashierId, r.WarehouseId, ct);
            var names = (await _users.GetByTenantAsync(t, ct)).ToDictionary(w => w.Id, w => w.Name);
            return sales.Select(s => SalesMapper.WithCashierName(SalesMapper.ToDto(s), names)).ToList();
        }
    }

    public class GetSaleByIdHandler : IRequestHandler<GetSaleByIdQuery, SaleDto>
    {
        private readonly ISaleRepository _repo; private readonly IOperationsUserRepository _users; private readonly ICurrentUserService _u;
        public GetSaleByIdHandler(ISaleRepository repo, IOperationsUserRepository users, ICurrentUserService u) { _repo = repo; _users = users; _u = u; }
        public async Task<SaleDto> Handle(GetSaleByIdQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var s = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Venta no encontrada.");
            var names = (await _users.GetByTenantAsync(t, ct)).ToDictionary(w => w.Id, w => w.Name);
            return SalesMapper.WithCashierName(SalesMapper.ToDto(s), names);
        }
    }

    public class ImportSalesHandler : IRequestHandler<ImportSalesCommand, ImportSalesResultDto>
    {
        private readonly ISaleRepository _sales; private readonly IProductRepository _products;
        private readonly ICashRegisterRepository _registers; private readonly IWarehouseRepository _warehouses;
        private readonly IProductPriceHistoryRepository _priceHistory;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        private readonly IExcelSalesImportParser _parser;

        public ImportSalesHandler(ISaleRepository sales, IProductRepository products, ICashRegisterRepository registers,
            IWarehouseRepository warehouses, IProductPriceHistoryRepository priceHistory, IOperationsUnitOfWork uow,
            ICurrentUserService u, IExcelSalesImportParser parser)
        { _sales = sales; _products = products; _registers = registers; _warehouses = warehouses; _priceHistory = priceHistory; _uow = uow; _u = u; _parser = parser; }

        public async Task<ImportSalesResultDto> Handle(ImportSalesCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var none = Array.Empty<string>();
            var noDiffs = Array.Empty<SalesImportPriceDiffDto>();

            using var stream = r.File.OpenReadStream();
            var parseResult = _parser.ParseSales(stream, ct);
            if (!parseResult.IsValid)
                return new ImportSalesResultDto(false, false, 0, 0, noDiffs, parseResult.Errors);

            var register = await _registers.GetOpenForUserAsync(t, _u.AccountId, ct)
                ?? throw new DomainException("Debes tener una caja abierta para importar ventas.");

            var warehouseId = register.WarehouseId;
            if (warehouseId is null)
            {
                var warehouses = await _warehouses.GetByTenantAsync(t, ct);
                warehouseId = warehouses.FirstOrDefault()?.Id
                    ?? throw new DomainException("No hay almacenes configurados para recibir las ventas importadas.");
            }
            var warehouseName = (await _warehouses.GetByIdAsync(t, warehouseId.Value, ct))?.Name;

            // Resuelve cada fila contra el catálogo: código exacto primero, si no nombre exacto.
            var resolved = new List<(SalesImportRowDto Row, Product Product)>();
            var resolveErrors = new List<string>();
            foreach (var row in parseResult.Rows)
            {
                Product? p = null;
                if (!string.IsNullOrWhiteSpace(row.Barcode))
                    p = await _products.GetByBarcodeAsync(t, row.Barcode, ct);
                if (p is null && !string.IsNullOrWhiteSpace(row.ProductName))
                {
                    var candidates = await _products.GetByTenantAsync(t, row.ProductName, null, null, null, ct);
                    p = candidates.FirstOrDefault(x => string.Equals(x.Name, row.ProductName, StringComparison.OrdinalIgnoreCase));
                }
                if (p is null)
                    resolveErrors.Add($"Fila {row.RowNumber}: producto '{row.ProductName ?? row.Barcode}' no encontrado en el catálogo.");
                else
                    resolved.Add((row, p));
            }

            if (resolveErrors.Count > 0)
                return new ImportSalesResultDto(false, false, 0, 0, noDiffs, resolveErrors);

            var diffs = resolved
                .Where(x => x.Row.Price != x.Product.SellPrice)
                .Select(x => new SalesImportPriceDiffDto(x.Row.RowNumber, x.Product.Name, x.Product.Barcode, x.Row.Quantity, x.Product.SellPrice, x.Row.Price))
                .ToList();

            if (diffs.Count > 0 && r.AcceptNewPrices is null)
                return new ImportSalesResultDto(false, true, 0, 0, diffs, none);

            var acceptPrices = r.AcceptNewPrices == true;
            var importedCount = 0; var pricesUpdatedCount = 0;
            var stockAdjustments = new List<(Product Product, Guid WarehouseId, int Delta)>();

            foreach (var (row, product) in resolved)
            {
                var priceDiffers = row.Price != product.SellPrice;
                var unitPrice = acceptPrices && priceDiffers ? row.Price : product.SellPrice;

                if (acceptPrices && priceDiffers)
                {
                    var oldSell = product.SellPrice;
                    product.Update(product.Name, product.CostPrice, row.Price, product.Barcode, product.Description,
                        product.Unit, product.CategoryId, product.CostPriceUSD, product.SellPriceUSD, product.MinStock,
                        product.TaxRate, product.BatchNumber, product.ExpirationDate, product.ForSale, product.ImageUrl,
                        product.MinOrderQuantity);
                    await _priceHistory.AddAsync(ProductPriceHistory.Create(t, product.Id, product.CostPrice, product.CostPrice,
                        oldSell, row.Price, product.CostPriceUSD, product.CostPriceUSD, product.SellPriceUSD, product.SellPriceUSD,
                        _u.AccountId, "Importación de ventas"), ct);
                    pricesUpdatedCount++;
                }

                product.AdjustStock(warehouseId.Value, -row.Quantity);
                stockAdjustments.Add((product, warehouseId.Value, -row.Quantity));
                await _products.UpdateAsync(product, ct);

                var item = SaleItem.Create(product.Id, product.Name, warehouseId.Value, row.Quantity, unitPrice);
                var payment = SalePayment.Create(PaymentMethod.Cash, item.LineTotal, Currency.CUP, cashTendered: item.LineTotal, change: 0);
                var sale = Sale.Create(t, _u.AccountId, register.Id, PaymentMethod.Cash, Currency.CUP,
                    new[] { item }, new[] { payment }, warehouseName: warehouseName);
                await _sales.AddAsync(sale, ct);

                register.RegisterSale(payment.Amount);
                importedCount++;
            }

            await _registers.UpdateAsync(register, ct);
            await OpsMapper.SaveWithStockRetryAsync(_uow, stockAdjustments, ct);

            return new ImportSalesResultDto(true, false, importedCount, pricesUpdatedCount, noDiffs, none);
        }
    }
}
