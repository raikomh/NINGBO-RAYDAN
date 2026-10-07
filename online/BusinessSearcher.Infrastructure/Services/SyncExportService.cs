using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Sync;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Services
{
    /// <summary>
    /// Arma, directo desde <see cref="OperationsDbContext"/>, el snapshot completo de los datos
    /// operativos de un tenant online para que una instalación en modo Local lo descargue (pull del
    /// Administrador, ver <c>SyncPullHttpClient</c> en el repo local) y lo integre localmente.
    /// </summary>
    public class SyncExportService : ISyncExportService
    {
        private readonly OperationsDbContext _db;

        public SyncExportService(OperationsDbContext db) => _db = db;

        public async Task<SyncPushEnvelopeDto> ExportAsync(Guid onlineTenantId, CancellationToken cancellationToken = default)
        {
            var businessInfos = await _db.BusinessInfos
                .Where(x => x.TenantId == onlineTenantId)
                .Select(x => new SyncBusinessInfoDto(
                    x.Id, x.TenantId, x.CreatedAt, x.UpdatedAt,
                    x.Name, x.Address, x.Phone, x.Email, x.TaxId, x.LogoUrl))
                .ToListAsync(cancellationToken);

            var suppliers = await _db.Suppliers
                .Where(x => x.TenantId == onlineTenantId)
                .Select(x => new SyncSupplierDto(
                    x.Id, x.TenantId, x.CreatedAt, x.UpdatedAt,
                    x.Name, x.Contact, x.Phone, x.Email, x.Address, x.Latitude, x.Longitude, x.Category, x.IsActive))
                .ToListAsync(cancellationToken);

            var warehouses = await _db.Warehouses
                .Where(x => x.TenantId == onlineTenantId)
                .Select(x => new SyncWarehouseDto(
                    x.Id, x.TenantId, x.CreatedAt, x.UpdatedAt,
                    x.Name, x.Location, x.Description, x.IsActive))
                .ToListAsync(cancellationToken);

            var categories = await _db.Categories
                .Where(x => x.TenantId == onlineTenantId)
                .Select(x => new SyncCategoryDto(
                    x.Id, x.TenantId, x.CreatedAt, x.UpdatedAt,
                    x.Name, x.Description, x.Code, x.IsActive))
                .ToListAsync(cancellationToken);

            var products = await _db.Products
                .Include(x => x.Stocks)
                .Where(x => x.TenantId == onlineTenantId)
                .ToListAsync(cancellationToken);
            var productDtos = products.Select(p => new SyncProductDto(
                p.Id, p.TenantId, p.CreatedAt, p.UpdatedAt,
                p.Barcode, p.Name, p.Description, p.Unit, p.CategoryId,
                p.CostPrice, p.SellPrice, p.CostPriceUSD, p.SellPriceUSD,
                p.MinStock, p.TaxRate, p.BatchNumber, p.ExpirationDate,
                p.ForSale, p.IsActive, p.ImageUrl, p.IsPubliclyVisible, p.MinOrderQuantity,
                p.Stocks.Select(s => new SyncProductStockDto(
                    s.Id, s.CreatedAt, s.UpdatedAt, s.ProductId, s.WarehouseId, s.Quantity, s.AverageCost, s.AverageCostUSD)).ToList())
            ).ToList();

            var sales = await _db.Sales
                .Include(x => x.Items)
                .Include(x => x.Payments)
                .Where(x => x.TenantId == onlineTenantId)
                .ToListAsync(cancellationToken);
            var saleDtos = sales.Select(s => new SyncSaleDto(
                s.Id, s.TenantId, s.CreatedAt, s.UpdatedAt,
                s.Date, s.Subtotal, s.Discount, s.Total, s.SubtotalUSD, s.TotalUSD,
                s.TaxAmount, s.PaymentMethod.ToString(), s.CashierId, s.RegisterId, s.TerminalName,
                s.PaymentCurrency.ToString(), s.ExchangeRate, s.Status.ToString(),
                s.Items.Select(i => new SyncSaleItemDto(
                    i.Id, i.CreatedAt, i.UpdatedAt, i.ProductId, i.ProductName, i.WarehouseId,
                    i.Quantity, i.UnitPrice, i.DiscountType.ToString(), i.DiscountValue)).ToList(),
                s.Payments.Select(pm => new SyncSalePaymentDto(
                    pm.Id, pm.CreatedAt, pm.UpdatedAt, pm.Method.ToString(), pm.Amount, pm.Currency.ToString(),
                    pm.AmountUSD, pm.TransactionId, pm.CashTendered, pm.Change, pm.ChangeCurrency?.ToString())).ToList())
            ).ToList();

            var purchases = await _db.Purchases
                .Include(x => x.Items)
                .Where(x => x.TenantId == onlineTenantId)
                .ToListAsync(cancellationToken);
            var purchaseDtos = purchases.Select(p => new SyncPurchaseDto(
                p.Id, p.TenantId, p.CreatedAt, p.UpdatedAt,
                p.SupplierId, p.WarehouseId, p.UserId, p.Total, p.TotalUSD,
                p.AssociatedExpenses, p.AssociatedExpensesUSD, p.Currency.ToString(), p.ExchangeRate,
                p.Status.ToString(), p.InvoiceUrl, p.Date,
                p.Items.Select(i => new SyncPurchaseItemDto(
                    i.Id, i.CreatedAt, i.UpdatedAt, i.ProductId, i.ProductName, i.Quantity, i.CostPrice,
                    i.BatchNumber, i.ExpirationDate)).ToList(),
                p.PurchaseRequestIds.ToList())
            ).ToList();

            var inventoryMovements = await _db.InventoryMovements
                .Where(x => x.TenantId == onlineTenantId)
                .Select(x => new SyncInventoryMovementDto(
                    x.Id, x.TenantId, x.CreatedAt, x.UpdatedAt,
                    x.ProductId, x.ProductName, x.FromWarehouseId, x.ToWarehouseId,
                    x.Quantity, x.Type.ToString(), x.Reason, x.UserId, x.Date))
                .ToListAsync(cancellationToken);

            var cashMovements = await _db.CashMovements
                .Where(x => x.TenantId == onlineTenantId)
                .ToListAsync(cancellationToken);

            var cashRegisters = await _db.CashRegisters
                .Where(x => x.TenantId == onlineTenantId)
                .ToListAsync(cancellationToken);
            var cashRegisterDtos = cashRegisters.Select(r => new SyncCashRegisterDto(
                r.Id, r.TenantId, r.CreatedAt, r.UpdatedAt,
                r.WarehouseId, r.TerminalId, r.OpenDate, r.CloseDate,
                r.InitialAmount, r.ExpectedAmount, r.ActualAmount, r.Difference,
                r.InitialAmountUSD, r.ExpectedAmountUSD, r.ActualAmountUSD, r.DifferenceUSD,
                r.Status.ToString(), r.OpenedBy, r.ClosedBy, r.SalesCount, r.TotalSales, r.TotalExpenses,
                r.TotalCashIn, r.TotalCashOut, r.InventoryCountCompleted,
                cashMovements.Where(m => m.RegisterId == r.Id)
                    .Select(m => new SyncCashMovementDto(
                        m.Id, m.TenantId, m.CreatedAt, m.UpdatedAt, m.RegisterId, m.Date,
                        m.Type.ToString(), m.Amount, m.AmountUSD, m.Currency.ToString(), m.Description, m.UserId))
                    .ToList())
            ).ToList();

            var terminals = await _db.Terminals
                .Where(x => x.TenantId == onlineTenantId)
                .Select(x => new SyncTerminalDto(
                    x.Id, x.TenantId, x.CreatedAt, x.UpdatedAt,
                    x.Name, x.Description, x.WarehouseId, x.IsActive))
                .ToListAsync(cancellationToken);

            var operationsUsers = await _db.OperationsUsers
                .Where(x => x.TenantId == onlineTenantId)
                .Select(x => new SyncOperationsUserDto(
                    x.Id, x.TenantId, x.CreatedAt, x.UpdatedAt,
                    x.Name, x.Email, x.PasswordHash, x.Role.ToString(), x.AvatarUrl,
                    x.AssignedRegisterId, x.AssignedWarehouseId, x.IsActive))
                .ToListAsync(cancellationToken);

            var expenses = await _db.Expenses
                .Where(x => x.TenantId == onlineTenantId)
                .Select(x => new SyncExpenseDto(
                    x.Id, x.TenantId, x.CreatedAt, x.UpdatedAt,
                    x.Type.ToString(), x.Amount, x.AmountUSD, x.Description, x.UserId, x.RegisterId, x.Date))
                .ToListAsync(cancellationToken);

            return new SyncPushEnvelopeDto(
                onlineTenantId, DateTime.UtcNow,
                businessInfos, suppliers, warehouses, categories, productDtos,
                saleDtos, purchaseDtos, inventoryMovements, cashRegisterDtos, terminals,
                operationsUsers, expenses);
        }
    }
}
