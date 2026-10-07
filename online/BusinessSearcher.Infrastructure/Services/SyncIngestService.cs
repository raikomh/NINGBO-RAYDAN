using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Sync;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessSearcher.Infrastructure.Services
{
    /// <summary>
    /// Integra un <see cref="SyncPushEnvelopeDto"/> (ya reescrito por <see cref="EntityIdRewriter"/>)
    /// en los datos operativos del tenant online. "Datos maestros" (que se editan luego de creados)
    /// se actualizan con el método <c>Update</c> del propio agregado si ya existen; "datos
    /// transaccionales" (ventas, compras, movimientos) se insertan una sola vez y no se vuelven a
    /// tocar si ya llegaron en una sincronización anterior (idempotente ante reintentos).
    /// </summary>
    public class SyncIngestService : ISyncIngestService
    {
        private readonly OperationsDbContext _db;
        private readonly ILogger<SyncIngestService> _logger;

        public SyncIngestService(OperationsDbContext db, ILogger<SyncIngestService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<SyncIngestResultDto> IngestAsync(
            Guid onlineTenantId, SyncPushEnvelopeDto envelope, CancellationToken cancellationToken = default)
        {
            var rewritten = EntityIdRewriter.RewriteTenantId(envelope, onlineTenantId);
            var conflicts = new List<string>();
            var accepted = 0;

            accepted += await IngestBusinessInfoAsync(onlineTenantId, rewritten.BusinessInfos, cancellationToken);
            accepted += await IngestSuppliersAsync(rewritten.Suppliers, cancellationToken);
            accepted += await IngestWarehousesAsync(rewritten.Warehouses, cancellationToken);
            accepted += await IngestCategoriesAsync(rewritten.Categories, cancellationToken);
            accepted += await IngestProductsAsync(rewritten.Products, cancellationToken);
            accepted += await IngestTerminalsAsync(rewritten.Terminals, cancellationToken);
            accepted += await IngestOperationsUsersAsync(onlineTenantId, rewritten.OperationsUsers, conflicts, cancellationToken);

            accepted += await IngestSalesAsync(rewritten.Sales, cancellationToken);
            accepted += await IngestPurchasesAsync(rewritten.Purchases, cancellationToken);
            accepted += await IngestInventoryMovementsAsync(rewritten.InventoryMovements, cancellationToken);
            accepted += await IngestCashRegistersAsync(rewritten.CashRegisters, cancellationToken);
            accepted += await IngestExpensesAsync(rewritten.Expenses, cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Sync ingest tenant {TenantId}: {Sent} enviados, {Accepted} aceptados, {Conflicts} conflictos.",
                onlineTenantId, envelope.TotalItems, accepted, conflicts.Count);

            return new SyncIngestResultDto(envelope.TotalItems, accepted, conflicts);
        }

        // ── Datos maestros (upsert con Update del agregado) ────────────────────────

        private async Task<int> IngestBusinessInfoAsync(
            Guid tenantId, IReadOnlyList<SyncBusinessInfoDto> items, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var dto = items[^1]; // uno por tenant: el más reciente del paquete gana
            var existing = await _db.BusinessInfos.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
            if (existing is null)
            {
                _db.BusinessInfos.Add(BusinessInfo.Restore(
                    dto.Id, dto.CreatedAt, dto.UpdatedAt, tenantId,
                    dto.Name, dto.Address, dto.Phone, dto.Email, dto.TaxId, dto.LogoUrl));
            }
            else
            {
                existing.Update(dto.Name, dto.Address, dto.Phone, dto.Email, dto.TaxId, dto.LogoUrl);
            }
            return 1;
        }

        private async Task<int> IngestSuppliersAsync(IReadOnlyList<SyncSupplierDto> items, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var ids = items.Select(x => x.Id).ToList();
            var existingById = await _db.Suppliers.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

            foreach (var dto in items)
            {
                if (existingById.TryGetValue(dto.Id, out var existing))
                    existing.Update(dto.Name, dto.Phone, dto.Email, dto.Contact, dto.Address, dto.Category, dto.Latitude, dto.Longitude);
                else
                    _db.Suppliers.Add(Supplier.Restore(
                        dto.Id, dto.CreatedAt, dto.UpdatedAt, dto.TenantId,
                        dto.Name, dto.Contact, dto.Phone, dto.Email, dto.Address, dto.Latitude, dto.Longitude, dto.Category, dto.IsActive));
            }
            return items.Count;
        }

        private async Task<int> IngestWarehousesAsync(IReadOnlyList<SyncWarehouseDto> items, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var ids = items.Select(x => x.Id).ToList();
            var existingById = await _db.Warehouses.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

            foreach (var dto in items)
            {
                if (existingById.TryGetValue(dto.Id, out var existing))
                    existing.Update(dto.Name, dto.Location, dto.Description);
                else
                    _db.Warehouses.Add(Warehouse.Restore(
                        dto.Id, dto.CreatedAt, dto.UpdatedAt, dto.TenantId, dto.Name, dto.Location, dto.Description, dto.IsActive));
            }
            return items.Count;
        }

        private async Task<int> IngestCategoriesAsync(IReadOnlyList<SyncCategoryDto> items, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var ids = items.Select(x => x.Id).ToList();
            var existingById = await _db.Categories.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

            foreach (var dto in items)
            {
                if (existingById.TryGetValue(dto.Id, out var existing))
                    existing.Update(dto.Name, dto.Description, dto.Code);
                else
                    _db.Categories.Add(Category.Restore(
                        dto.Id, dto.CreatedAt, dto.UpdatedAt, dto.TenantId, dto.Name, dto.Description, dto.Code, dto.IsActive));
            }
            return items.Count;
        }

        private async Task<int> IngestProductsAsync(IReadOnlyList<SyncProductDto> items, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var ids = items.Select(x => x.Id).ToList();
            var existingById = await _db.Products.Include(p => p.Stocks)
                .Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

            foreach (var dto in items)
            {
                if (existingById.TryGetValue(dto.Id, out var existing))
                {
                    existing.Update(dto.Name, dto.CostPrice, dto.SellPrice, dto.Barcode, dto.Description, dto.Unit,
                        dto.CategoryId, dto.CostPriceUSD, dto.SellPriceUSD, dto.MinStock, dto.TaxRate,
                        dto.BatchNumber, dto.ExpirationDate, dto.ForSale, dto.ImageUrl, dto.MinOrderQuantity);
                    if (existing.IsPubliclyVisible != dto.IsPubliclyVisible)
                        existing.SetPublicVisibility(dto.IsPubliclyVisible);
                    if (dto.IsActive == false && existing.IsActive)
                        existing.Deactivate();

                    foreach (var s in dto.Stocks)
                    {
                        var stock = existing.StockFor(s.WarehouseId);
                        stock.SetQuantity(s.Quantity);
                        stock.SetAverageCost(s.AverageCost, s.AverageCostUSD);
                    }
                }
                else
                {
                    var stocks = dto.Stocks.Select(s => ProductStock.Restore(
                        s.Id, s.CreatedAt, s.UpdatedAt, dto.Id, s.WarehouseId, s.Quantity, s.AverageCost, s.AverageCostUSD));
                    _db.Products.Add(Product.Restore(
                        dto.Id, dto.CreatedAt, dto.UpdatedAt, dto.TenantId, dto.Barcode, dto.Name, dto.Description,
                        dto.Unit, dto.CategoryId, dto.CostPrice, dto.SellPrice, dto.CostPriceUSD, dto.SellPriceUSD,
                        dto.MinStock, dto.TaxRate, dto.BatchNumber, dto.ExpirationDate, dto.ForSale, dto.IsActive,
                        dto.ImageUrl, dto.IsPubliclyVisible, dto.MinOrderQuantity, stocks));
                }
            }
            return items.Count;
        }

        private async Task<int> IngestTerminalsAsync(IReadOnlyList<SyncTerminalDto> items, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var ids = items.Select(x => x.Id).ToList();
            var existingById = await _db.Terminals.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

            foreach (var dto in items)
            {
                if (existingById.TryGetValue(dto.Id, out var existing))
                    existing.Update(dto.Name, dto.Description, dto.WarehouseId, dto.IsActive);
                else
                    _db.Terminals.Add(Terminal.Restore(
                        dto.Id, dto.CreatedAt, dto.UpdatedAt, dto.TenantId, dto.Name, dto.Description, dto.WarehouseId, dto.IsActive));
            }
            return items.Count;
        }

        // OperationsUser.Email tiene índice único GLOBAL (no por tenant): si el email ya
        // pertenece a otro tenant online, se rechaza esa fila y se reporta como conflicto.
        private async Task<int> IngestOperationsUsersAsync(
            Guid tenantId, IReadOnlyList<SyncOperationsUserDto> items, List<string> conflicts, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var ids = items.Select(x => x.Id).ToList();
            var existingById = await _db.OperationsUsers.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            var accepted = 0;

            foreach (var dto in items)
            {
                if (existingById.TryGetValue(dto.Id, out var existing))
                {
                    existing.Update(dto.Name, Enum.Parse<OperationsRole>(dto.Role, true), dto.AvatarUrl,
                        dto.AssignedRegisterId, dto.AssignedWarehouseId, dto.IsActive);
                    accepted++;
                    continue;
                }

                var emailOwner = await _db.OperationsUsers.FirstOrDefaultAsync(u => u.Email == dto.Email, ct);
                if (emailOwner is not null && emailOwner.TenantId != tenantId)
                {
                    conflicts.Add($"OperationsUser '{dto.Email}' ya pertenece a otro negocio online; se omitió.");
                    continue;
                }
                if (emailOwner is not null)
                {
                    // Mismo tenant, ya tiene un usuario con este email creado con otro Id: actualiza ese en vez de duplicar.
                    emailOwner.Update(dto.Name, Enum.Parse<OperationsRole>(dto.Role, true), dto.AvatarUrl,
                        dto.AssignedRegisterId, dto.AssignedWarehouseId, dto.IsActive);
                    accepted++;
                    continue;
                }

                _db.OperationsUsers.Add(OperationsUser.Restore(
                    dto.Id, dto.CreatedAt, dto.UpdatedAt, tenantId, dto.Name, dto.Email, dto.PasswordHash,
                    Enum.Parse<OperationsRole>(dto.Role, true), dto.AvatarUrl,
                    dto.AssignedRegisterId, dto.AssignedWarehouseId, dto.IsActive));
                accepted++;
            }
            return accepted;
        }

        // ── Datos transaccionales (insert-if-missing, no se editan luego) ──────────

        private async Task<int> IngestSalesAsync(IReadOnlyList<SyncSaleDto> items, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var ids = items.Select(x => x.Id).ToList();
            var existingIds = await _db.Sales.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
            var existingSet = existingIds.ToHashSet();

            var accepted = 0;
            foreach (var dto in items)
            {
                if (existingSet.Contains(dto.Id)) { accepted++; continue; }

                var sale = Sale.Restore(
                    dto.Id, dto.CreatedAt, dto.UpdatedAt, dto.TenantId, dto.Date, dto.Subtotal, dto.Discount,
                    dto.Total, dto.SubtotalUSD, dto.TotalUSD, dto.TaxAmount, Enum.Parse<PaymentMethod>(dto.PaymentMethod, true),
                    dto.CashierId, dto.RegisterId, dto.TerminalName, Enum.Parse<Currency>(dto.PaymentCurrency, true),
                    dto.ExchangeRate, Enum.Parse<SaleStatus>(dto.Status, true),
                    dto.Items.Select(i => SaleItem.Restore(
                        i.Id, i.CreatedAt, i.UpdatedAt, i.ProductId, i.ProductName, i.WarehouseId, i.Quantity,
                        i.UnitPrice, Enum.Parse<DiscountType>(i.DiscountType, true), i.DiscountValue)),
                    dto.Payments.Select(p => SalePayment.Restore(
                        p.Id, p.CreatedAt, p.UpdatedAt, Enum.Parse<PaymentMethod>(p.Method, true), p.Amount,
                        Enum.Parse<Currency>(p.Currency, true), p.AmountUSD, p.TransactionId, p.CashTendered,
                        p.Change, p.ChangeCurrency is null ? null : Enum.Parse<Currency>(p.ChangeCurrency, true))));

                _db.Sales.Add(sale);
                accepted++;
            }
            return accepted;
        }

        private async Task<int> IngestPurchasesAsync(IReadOnlyList<SyncPurchaseDto> items, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var ids = items.Select(x => x.Id).ToList();
            var existingSet = (await _db.Purchases.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct)).ToHashSet();

            var accepted = 0;
            foreach (var dto in items)
            {
                if (existingSet.Contains(dto.Id)) { accepted++; continue; }

                var purchase = Purchase.Restore(
                    dto.Id, dto.CreatedAt, dto.UpdatedAt, dto.TenantId, dto.SupplierId, dto.WarehouseId, dto.UserId,
                    dto.Total, dto.TotalUSD, dto.AssociatedExpenses, dto.AssociatedExpensesUSD,
                    Enum.Parse<Currency>(dto.Currency, true), dto.ExchangeRate, Enum.Parse<PurchaseStatus>(dto.Status, true),
                    dto.InvoiceUrl, dto.Date,
                    dto.Items.Select(i => PurchaseItem.Restore(
                        i.Id, i.CreatedAt, i.UpdatedAt, i.ProductId, i.ProductName, i.Quantity, i.CostPrice,
                        i.BatchNumber, i.ExpirationDate)),
                    dto.PurchaseRequestIds);

                _db.Purchases.Add(purchase);
                accepted++;
            }
            return accepted;
        }

        private async Task<int> IngestInventoryMovementsAsync(IReadOnlyList<SyncInventoryMovementDto> items, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var ids = items.Select(x => x.Id).ToList();
            var existingSet = (await _db.InventoryMovements.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct)).ToHashSet();

            var accepted = 0;
            foreach (var dto in items)
            {
                if (existingSet.Contains(dto.Id)) { accepted++; continue; }

                _db.InventoryMovements.Add(InventoryMovement.Restore(
                    dto.Id, dto.CreatedAt, dto.UpdatedAt, dto.TenantId, dto.ProductId, dto.ProductName,
                    dto.FromWarehouseId, dto.ToWarehouseId, dto.Quantity,
                    Enum.Parse<InventoryMovementType>(dto.Type, true), dto.Reason, dto.UserId, dto.Date));
                accepted++;
            }
            return accepted;
        }

        private async Task<int> IngestCashRegistersAsync(IReadOnlyList<SyncCashRegisterDto> items, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var ids = items.Select(x => x.Id).ToList();
            var existingSet = (await _db.CashRegisters.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct)).ToHashSet();

            var accepted = 0;
            foreach (var dto in items)
            {
                if (existingSet.Contains(dto.Id)) { accepted++; continue; }

                _db.CashRegisters.Add(CashRegister.Restore(
                    dto.Id, dto.CreatedAt, dto.UpdatedAt, dto.TenantId, dto.WarehouseId, dto.TerminalId,
                    dto.OpenDate, dto.CloseDate, dto.InitialAmount, dto.ExpectedAmount, dto.ActualAmount, dto.Difference,
                    dto.InitialAmountUSD, dto.ExpectedAmountUSD, dto.ActualAmountUSD, dto.DifferenceUSD,
                    Enum.Parse<CashRegisterStatus>(dto.Status, true), dto.OpenedBy, dto.ClosedBy, dto.SalesCount,
                    dto.TotalSales, dto.TotalExpenses, dto.TotalCashIn, dto.TotalCashOut, dto.InventoryCountCompleted));

                foreach (var m in dto.Movements)
                    _db.CashMovements.Add(CashMovement.Restore(
                        m.Id, m.CreatedAt, m.UpdatedAt, m.TenantId, m.RegisterId, m.Date,
                        Enum.Parse<CashMovementType>(m.Type, true), m.Amount, m.AmountUSD,
                        Enum.Parse<Currency>(m.Currency, true), m.Description, m.UserId));

                accepted++;
            }
            return accepted;
        }

        private async Task<int> IngestExpensesAsync(IReadOnlyList<SyncExpenseDto> items, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var ids = items.Select(x => x.Id).ToList();
            var existingSet = (await _db.Expenses.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct)).ToHashSet();

            var accepted = 0;
            foreach (var dto in items)
            {
                if (existingSet.Contains(dto.Id)) { accepted++; continue; }

                _db.Expenses.Add(Expense.Restore(
                    dto.Id, dto.CreatedAt, dto.UpdatedAt, dto.TenantId, Enum.Parse<ExpenseType>(dto.Type, true),
                    dto.Amount, dto.AmountUSD, dto.Description, dto.UserId, dto.RegisterId, dto.Date));
                accepted++;
            }
            return accepted;
        }
    }
}
