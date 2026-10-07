using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.DbContexts
{
    /// <summary>
    /// Contexto de Operaciones (TPV/ERP): proveedores, almacenes, gastos, tasas de cambio,
    /// compras, ventas, caja, etc. Todo por-negocio (TenantId). Schema public, historial
    /// de migraciones propio (__ef_migrations_operations).
    /// </summary>
    public class OperationsDbContext : DbContext
    {
        public DbSet<Supplier>        Suppliers        => Set<Supplier>();
        public DbSet<Warehouse>       Warehouses       => Set<Warehouse>();
        public DbSet<Expense>         Expenses         => Set<Expense>();
        public DbSet<ExchangeRateLog> ExchangeRateLogs => Set<ExchangeRateLog>();
        public DbSet<RoleSalaryConfig> RoleSalaryConfigs => Set<RoleSalaryConfig>();

        // Catálogo (POS)
        public DbSet<Category>            Categories          => Set<Category>();
        public DbSet<Product>             Products            => Set<Product>();
        public DbSet<ProductStock>        ProductStocks       => Set<ProductStock>();
        public DbSet<CatalogImport>       CatalogImports      => Set<CatalogImport>();
        public DbSet<ProductPriceHistory> ProductPriceHistory => Set<ProductPriceHistory>();

        // Ventas (POS) y Caja
        public DbSet<Sale>         Sales         => Set<Sale>();
        public DbSet<SaleItem>     SaleItems     => Set<SaleItem>();
        public DbSet<SalePayment>  SalePayments  => Set<SalePayment>();
        public DbSet<Terminal>     Terminals     => Set<Terminal>();
        public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
        public DbSet<CashMovement> CashMovements => Set<CashMovement>();

        // Sub-usuarios operativos
        public DbSet<OperationsUser> OperationsUsers => Set<OperationsUser>();

        // Compras e inventario avanzado
        public DbSet<PurchaseRequest>    PurchaseRequests    => Set<PurchaseRequest>();
        public DbSet<Purchase>           Purchases           => Set<Purchase>();
        public DbSet<PurchaseItem>       PurchaseItems       => Set<PurchaseItem>();
        public DbSet<InventoryMovement>  InventoryMovements  => Set<InventoryMovement>();
        public DbSet<InventoryCount>     InventoryCounts     => Set<InventoryCount>();
        public DbSet<InventoryCountItem> InventoryCountItems => Set<InventoryCountItem>();

        // Administración: datos fiscales, auditoría y configuración clave-valor
        public DbSet<BusinessInfo> BusinessInfos => Set<BusinessInfo>();
        public DbSet<AuditLog>     AuditLogs     => Set<AuditLog>();
        public DbSet<Setting>      Settings      => Set<Setting>();

        public OperationsDbContext(DbContextOptions<OperationsDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (Database.IsRelational())
                modelBuilder.HasDefaultSchema("public");

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(OperationsDbContext).Assembly,
                t => t.Namespace != null && t.Namespace.Contains("Configurations.Operations"));

            base.OnModelCreating(modelBuilder);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Modified))
            {
                if (entry.Entity is Domain.Common.Entity)
                    entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
