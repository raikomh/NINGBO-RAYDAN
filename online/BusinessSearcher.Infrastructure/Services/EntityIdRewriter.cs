using BusinessSearcher.Application.DTOs.Sync;

namespace BusinessSearcher.Infrastructure.Services
{
    /// <summary>
    /// Reescribe el único identificador que legítimamente difiere entre "local" y "online": el
    /// <c>TenantId</c>. Los demás Ids (Product, Sale, Warehouse, etc.) son GUIDs generados en el
    /// cliente y se preservan tal cual — no hay riesgo de colisión y así las referencias cruzadas
    /// dentro del paquete (p.ej. SaleItem.ProductId) siguen siendo válidas sin necesitar una tabla
    /// de mapeo de Ids persistida.
    /// </summary>
    public static class EntityIdRewriter
    {
        public static SyncPushEnvelopeDto RewriteTenantId(SyncPushEnvelopeDto envelope, Guid onlineTenantId)
            => envelope with
            {
                BusinessInfos = envelope.BusinessInfos.Select(x => x with { TenantId = onlineTenantId }).ToList(),
                Suppliers = envelope.Suppliers.Select(x => x with { TenantId = onlineTenantId }).ToList(),
                Warehouses = envelope.Warehouses.Select(x => x with { TenantId = onlineTenantId }).ToList(),
                Categories = envelope.Categories.Select(x => x with { TenantId = onlineTenantId }).ToList(),
                Products = envelope.Products.Select(x => x with { TenantId = onlineTenantId }).ToList(),
                Sales = envelope.Sales.Select(x => x with { TenantId = onlineTenantId }).ToList(),
                Purchases = envelope.Purchases.Select(x => x with { TenantId = onlineTenantId }).ToList(),
                InventoryMovements = envelope.InventoryMovements.Select(x => x with { TenantId = onlineTenantId }).ToList(),
                CashRegisters = envelope.CashRegisters.Select(x => x with
                {
                    TenantId = onlineTenantId,
                    Movements = x.Movements.Select(m => m with { TenantId = onlineTenantId }).ToList()
                }).ToList(),
                OperationsUsers = envelope.OperationsUsers.Select(x => x with { TenantId = onlineTenantId }).ToList(),
                Expenses = envelope.Expenses.Select(x => x with { TenantId = onlineTenantId }).ToList()
            };
    }
}
