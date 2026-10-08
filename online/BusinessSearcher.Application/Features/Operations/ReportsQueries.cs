using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using MediatR;

namespace BusinessSearcher.Application.Features.Operations.Reports
{
    public record GetSalesReportQuery(DateTime? From, DateTime? To, Guid? WarehouseId = null) : IRequest<SalesReportDto>;
    public record GetInventoryReportQuery(bool? LowStockOnly, Guid? WarehouseId = null) : IRequest<InventoryReportDto>;
    public record GetExpensesReportQuery(DateTime? From, DateTime? To) : IRequest<ExpensesReportDto>;

    public class GetSalesReportHandler : IRequestHandler<GetSalesReportQuery, SalesReportDto>
    {
        private readonly ISaleRepository _sales; private readonly ICurrentUserService _u;
        public GetSalesReportHandler(ISaleRepository sales, ICurrentUserService u) { _sales = sales; _u = u; }
        public async Task<SalesReportDto> Handle(GetSalesReportQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var sales = await _sales.GetByTenantAsync(t, r.From, r.To, null, null, r.WarehouseId, ct);
            var rows = sales.Select(s => new SalesReportRowDto(
                s.Date, s.RegisterId, s.PaymentMethod.ToString(), s.PaymentCurrency.ToString(), s.Total, s.Status.ToString())).ToList();
            var totalSales = sales.Where(s => s.Status == SaleStatus.Completed).Sum(s => s.Total);
            var totalRefunded = sales.Where(s => s.Status == SaleStatus.Refunded).Sum(s => s.Total);
            return new SalesReportDto(sales.Count, totalSales, totalRefunded, rows);
        }
    }

    public class GetInventoryReportHandler : IRequestHandler<GetInventoryReportQuery, InventoryReportDto>
    {
        private readonly IProductRepository _products; private readonly ICurrentUserService _u;
        public GetInventoryReportHandler(IProductRepository products, ICurrentUserService u) { _products = products; _u = u; }
        public async Task<InventoryReportDto> Handle(GetInventoryReportQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var products = await _products.GetByTenantAsync(t, null, null, r.WarehouseId, r.LowStockOnly, ct);
            // Con tienda seleccionada, la cantidad es la existencia en ESE almacén, no la suma de todos
            // (TotalStock), para que el reporte refleje solo lo que hay en la tienda activa.
            int StockFor(Domain.BoundedContext.Operations.Aggregates.Product p) => r.WarehouseId.HasValue
                ? p.Stocks.Where(s => s.WarehouseId == r.WarehouseId.Value).Sum(s => s.Quantity)
                : p.TotalStock;
            var rows = products.Select(p => new InventoryReportRowDto(
                p.Name, p.Barcode, StockFor(p), p.MinStock, p.SellPrice, StockFor(p) <= p.MinStock)).ToList();
            var value = products.Sum(p => StockFor(p) * p.CostPrice);
            return new InventoryReportDto(products.Count, rows.Count(x => x.LowStock), value, rows);
        }
    }

    public class GetExpensesReportHandler : IRequestHandler<GetExpensesReportQuery, ExpensesReportDto>
    {
        private readonly IExpenseRepository _expenses; private readonly ICurrentUserService _u;
        public GetExpensesReportHandler(IExpenseRepository expenses, ICurrentUserService u) { _expenses = expenses; _u = u; }
        public async Task<ExpensesReportDto> Handle(GetExpensesReportQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var expenses = await _expenses.GetByTenantAsync(t, r.From, r.To, ct);
            var rows = expenses.Select(e => new ExpensesReportRowDto(e.Date, e.Type.ToString(), e.Amount, e.Description)).ToList();
            return new ExpensesReportDto(expenses.Count, expenses.Sum(e => e.Amount), rows);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // DASHBOARD MENSUAL — ventas, compras, mermas, gastos y ganancia por mes de un año
    // ═══════════════════════════════════════════════════════════════
    public record GetMonthlyDashboardQuery(int? Year, Guid? WarehouseId = null) : IRequest<MonthlyDashboardDto>;

    public class GetMonthlyDashboardHandler : IRequestHandler<GetMonthlyDashboardQuery, MonthlyDashboardDto>
    {
        private static readonly string[] MonthNames =
        {
            "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"
        };

        private readonly ISaleRepository _sales;
        private readonly IPurchaseRepository _purchases;
        private readonly IInventoryMovementRepository _movements;
        private readonly IExpenseRepository _expenses;
        private readonly IProductRepository _products;
        private readonly ICurrentUserService _u;

        public GetMonthlyDashboardHandler(
            ISaleRepository sales, IPurchaseRepository purchases, IInventoryMovementRepository movements,
            IExpenseRepository expenses, IProductRepository products, ICurrentUserService u)
        {
            _sales = sales; _purchases = purchases; _movements = movements;
            _expenses = expenses; _products = products; _u = u;
        }

        public async Task<MonthlyDashboardDto> Handle(GetMonthlyDashboardQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var year = r.Year ?? DateTime.UtcNow.Year;

            var (months, yearProfit, yearSales, yearCost, yearPurchases, yearExpenses, yearMerma) = await ComputeYearAsync(t, year, r.WarehouseId, ct);

            decimal? prevProfit = null;
            decimal? changePercent = null;
            if (year > 1) // no tiene sentido comparar contra "año 0"; guarda simple contra overflow
            {
                var (_, prevYearProfit, _, _, _, _, _) = await ComputeYearAsync(t, year - 1, r.WarehouseId, ct);
                prevProfit = prevYearProfit;
                if (prevYearProfit != 0)
                    changePercent = Math.Round((yearProfit - prevYearProfit) / Math.Abs(prevYearProfit) * 100m, 2);
            }

            return new MonthlyDashboardDto(
                year, months, yearSales, yearCost, yearPurchases, yearExpenses, yearMerma, yearProfit,
                prevProfit, changePercent);
        }

        private async Task<(IReadOnlyList<MonthlyDashboardRowDto> Months, decimal YearProfit,
            decimal YearSales, decimal YearCost, decimal YearPurchases, decimal YearExpenses, decimal YearMerma)>
            ComputeYearAsync(Guid tenantId, int year, Guid? warehouseId, CancellationToken ct)
        {
            var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var yearEnd   = yearStart.AddYears(1).AddTicks(-1);

            var sales     = await _sales.GetByTenantAsync(tenantId, yearStart, yearEnd, null, null, warehouseId, ct);
            var purchases = await _purchases.GetByTenantAsync(tenantId, yearStart, yearEnd, null, warehouseId, ct);
            var movements = await _movements.GetByTenantAsync(tenantId, yearStart, yearEnd, null, warehouseId, ct);
            // Los gastos (alquiler, salarios, etc.) son del negocio completo, no de una tienda puntual:
            // no se filtran por almacén aunque haya una tienda seleccionada.
            var expenses  = await _expenses.GetByTenantAsync(tenantId, yearStart, yearEnd, ct);
            var products  = await _products.GetByTenantAsync(tenantId, null, null, null, null, ct);
            var costById  = products.ToDictionary(p => p.Id, p => p.CostPrice);

            var mermas = movements.Where(m => m.Type == Domain.BoundedContext.Operations.Enums.InventoryMovementType.Merma).ToList();

            var rows = new List<MonthlyDashboardRowDto>();
            for (var month = 1; month <= 12; month++)
            {
                var monthSales     = sales.Where(s => s.Date.Month == month).ToList();
                var completedSales = monthSales.Where(s => s.Status == SaleStatus.Completed).ToList();
                var salesTotal     = completedSales.Sum(s => s.Total);
                var refundsTotal   = monthSales.Where(s => s.Status == SaleStatus.Refunded).Sum(s => s.Total);

                // Costo de venta = costo actual de inventario de cada producto vendido (ventas completadas).
                var costOfGoodsSold = completedSales
                    .SelectMany(s => s.Items)
                    .Sum(i => i.Quantity * (costById.TryGetValue(i.ProductId, out var ic) ? ic : 0));

                // Excluye las compras canceladas: nunca llegaron a efectuarse.
                var monthPurchases = purchases.Where(p => p.Date.Month == month && p.Status != PurchaseStatus.Cancelled).ToList();
                var purchasesTotal = monthPurchases.Sum(p => p.Total);

                var monthMermas = mermas.Where(m => m.Date.Month == month).ToList();
                var mermaValue  = monthMermas.Sum(m => m.Quantity * (costById.TryGetValue(m.ProductId, out var c) ? c : 0));

                var monthExpenses = expenses.Where(e => e.Date.Month == month).Sum(e => e.Amount);

                // Ganancia = Venta − Costo de venta − Gasto − Merma. La compra no afecta este valor.
                // El reembolso NO se resta aparte: al reembolsar se repone el stock completo (ver
                // RefundSaleHandler), así que la venta reembolsada ya queda fuera de "Venta" y de
                // "Costo de venta" (ambas solo cuentan Status == Completed); su efecto neto es 0,
                // no una pérdida por el monto total de la venta.
                var profit = salesTotal - costOfGoodsSold - monthExpenses - mermaValue;

                rows.Add(new MonthlyDashboardRowDto(
                    month, MonthNames[month - 1],
                    salesTotal, refundsTotal, completedSales.Count,
                    costOfGoodsSold,
                    purchasesTotal, monthPurchases.Count,
                    monthMermas.Count, mermaValue,
                    monthExpenses, profit));
            }

            // Bruto (ventas completadas), sin restar reembolsos: "Ventas" nunca debe salir negativo.
            // El impacto de los reembolsos ya se refleja en "Ganancia" (ver profit más arriba).
            var yearSalesTotal     = rows.Sum(x => x.SalesTotal);
            var yearCostTotal      = rows.Sum(x => x.CostOfGoodsSold);
            var yearPurchasesTotal = rows.Sum(x => x.PurchasesTotal);
            var yearExpensesTotal  = rows.Sum(x => x.ExpensesTotal);
            var yearMermaTotal     = rows.Sum(x => x.MermaValue);
            var yearProfitTotal    = rows.Sum(x => x.Profit);

            return (rows, yearProfitTotal, yearSalesTotal, yearCostTotal, yearPurchasesTotal, yearExpensesTotal, yearMermaTotal);
        }
    }
}
