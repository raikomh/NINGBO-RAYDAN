using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Repositories;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using BusinessSearcher.Infrastructure.Persistence.Repositories.Radar;
using BusinessSearcher.Infrastructure.Persistence.Repositories.StoreManagement;
using BusinessSearcher.Infrastructure.Persistence.Repositories.TenantManagement;
using BusinessSearcher.Infrastructure.Persistence.UnitOfWork;
using StoreUoW = BusinessSearcher.Infrastructure.Persistence.UnitOfWork.StoreUnitOfWorkImpl;
using BusinessSearcher.Infrastructure.SchemaManagement;
using BusinessSearcher.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BusinessSearcher.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, IConfiguration configuration)
        {
            var useInMemory = configuration.GetValue<bool>("UseInMemoryDatabase");

            // ── DbContexts ────────────────────────────────────────────────────────
            if (useInMemory)
            {
                services.AddDbContext<TenantManagementDbContext>(opts =>
                    opts.UseInMemoryDatabase("TenantManagementDb"));

                services.AddScoped<StoreDbContext>(_ =>
                {
                    var opts = new DbContextOptionsBuilder<StoreDbContext>()
                        .UseInMemoryDatabase("StoreDb").Options;
                    return new StoreDbContext(opts, "public");
                });

                services.AddDbContext<RadarDbContext>(opts =>
                    opts.UseInMemoryDatabase("RadarDb"));

                services.AddDbContext<OperationsDbContext>(opts =>
                    opts.UseInMemoryDatabase("OperationsDb"));

                services.AddScoped<ITenantSchemaManager, NoOpTenantSchemaManager>();
                services.AddScoped<ISearchRepository,    InMemorySearchRepository>();
                services.AddHealthChecks();
            }
            else
            {
                var connStr = configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connStr))
                    throw new InvalidOperationException(
                        "ConnectionString 'DefaultConnection' vacía o no configurada. Configúrala en formato Npgsql " +
                        "(Host=...;Database=...;Username=...;Password=...;SSL Mode=Require) vía user-secrets o la variable " +
                        "de entorno ConnectionStrings__DefaultConnection. La URI 'postgresql://...' de Neon NO es válida para Npgsql.");
                if (connStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                    connStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "La cadena está en formato URI ('postgresql://...'), que Npgsql no acepta. Conviértela a formato " +
                        "clave=valor: Host=<host>;Database=<db>;Username=<user>;Password=<pass>;SSL Mode=Require;Trust Server Certificate=true.");

                // Npgsql 8: EnableDynamicJson necesario para List<string> → jsonb
                var dataSource = new NpgsqlDataSourceBuilder(connStr)
                    .EnableDynamicJson()
                    .Build();

                services.AddDbContext<TenantManagementDbContext>(opts =>
                    opts.UseNpgsql(dataSource).UseSnakeCaseNamingConvention());

                services.AddScoped<StoreDbContext>(sp =>
                {
                    var currentUser = sp.GetRequiredService<ICurrentUserService>();
                    var schema = currentUser.TenantId != Guid.Empty
                        ? $"tenant_{currentUser.TenantId:N}"
                        : "public";
                    var opts = new DbContextOptionsBuilder<StoreDbContext>()
                        .UseNpgsql(dataSource).UseSnakeCaseNamingConvention().Options;
                    return new StoreDbContext(opts, schema);
                });

                services.AddDbContext<RadarDbContext>(opts =>
                    opts.UseNpgsql(dataSource,
                            o => o.MigrationsHistoryTable("__ef_migrations_radar", "public"))
                        .UseSnakeCaseNamingConvention());

                services.AddDbContext<OperationsDbContext>(opts =>
                    opts.UseNpgsql(dataSource,
                            o => o.MigrationsHistoryTable("__ef_migrations_operations", "public"))
                        .UseSnakeCaseNamingConvention());

                services.AddScoped<ITenantSchemaManager>(sp =>
                    new TenantSchemaManager(connStr,
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TenantSchemaManager>>()));

                services.AddScoped<ISearchRepository>(sp =>
                    new SearchRepository(connStr,
                        sp.GetRequiredService<TenantManagementDbContext>(),
                        sp.GetRequiredService<OperationsDbContext>()));

                services.AddHealthChecks()
                    .AddNpgSql(connStr, name: "postgresql", tags: new[] { "db", "ready" });
            }

            // ── Repositories ──────────────────────────────────────────────────────
            services.AddScoped<ITenantRepository,      TenantRepository>();
            services.AddScoped<IStoreRepository,       StoreRepository>();
            services.AddScoped<IChatRepository,        ChatRepository>();
            services.AddScoped<IMarketingVisitRepository, MarketingVisitRepository>();
            services.AddScoped<ISyncLogRepository,      SyncLogRepository>();

            // ── Radar (red colaborativa de disponibilidad) ────────────────────────
            services.AddScoped<IClientRepository,             ClientRepository>();
            services.AddScoped<IClientProductRepository,      ClientProductRepository>();
            services.AddScoped<IReferralRepository,           ReferralRepository>();
            services.AddScoped<IReferralMonthlySettlementRepository, ReferralMonthlySettlementRepository>();
            services.AddScoped<IMipymeReferralRepository,     MipymeReferralRepository>();
            services.AddScoped<IMipymeReferralMonthlySettlementRepository, MipymeReferralMonthlySettlementRepository>();
            services.AddScoped<IAvailabilityReportRepository, AvailabilityReportRepository>();
            services.AddScoped<IAvailabilityAlertRepository,  AvailabilityAlertRepository>();
            services.AddScoped<ICommunityQuestionRepository,  CommunityQuestionRepository>();

            // ── Operations (TPV/ERP) ──────────────────────────────────────────────
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.ISupplierRepository,     Persistence.Repositories.Operations.SupplierRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IWarehouseRepository,    Persistence.Repositories.Operations.WarehouseRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IExpenseRepository,      Persistence.Repositories.Operations.ExpenseRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IExchangeRateRepository, Persistence.Repositories.Operations.ExchangeRateRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IRoleSalaryConfigRepository, Persistence.Repositories.Operations.RoleSalaryConfigRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.ICategoryRepository,            Persistence.Repositories.Operations.CategoryRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IProductRepository,             Persistence.Repositories.Operations.ProductRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.ICatalogImportRepository,      Persistence.Repositories.Operations.CatalogImportRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IProductPriceHistoryRepository, Persistence.Repositories.Operations.ProductPriceHistoryRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.ISaleRepository,          Persistence.Repositories.Operations.SaleRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.ICashRegisterRepository,  Persistence.Repositories.Operations.CashRegisterRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.ICashMovementRepository,  Persistence.Repositories.Operations.CashMovementRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IOperationsUserRepository, Persistence.Repositories.Operations.OperationsUserRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IManagerRepository, Persistence.Repositories.Operations.ManagerRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IPurchaseRequestRepository,   Persistence.Repositories.Operations.PurchaseRequestRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IPurchaseRepository,          Persistence.Repositories.Operations.PurchaseRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IInventoryMovementRepository, Persistence.Repositories.Operations.InventoryMovementRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IInventoryCountRepository,    Persistence.Repositories.Operations.InventoryCountRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IBusinessInfoRepository, Persistence.Repositories.Operations.BusinessInfoRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.IAuditLogRepository,     Persistence.Repositories.Operations.AuditLogRepository>();
            services.AddScoped<Domain.BoundedContext.Operations.Repositories.ISettingRepository,      Persistence.Repositories.Operations.SettingRepository>();
            services.AddScoped<IOperationsReportExporter, OperationsReportExporter>();

            // ── Unit of Work ──────────────────────────────────────────────────────
            services.AddScoped<IUnitOfWork,      TenantManagementUnitOfWork>();
            services.AddScoped<IStoreUnitOfWork, StoreUoW>();
            services.AddScoped<IRadarUnitOfWork, RadarUnitOfWork>();
            services.AddScoped<IOperationsUnitOfWork, OperationsUnitOfWork>();

            // ── Auth Services ─────────────────────────────────────────────────────
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService,  CurrentUserService>();
            services.AddScoped<IJwtTokenService,     JwtTokenService>();
            services.AddScoped<IPasswordHasher,      PasswordHasher>();
            services.AddScoped<ISecureTokenGenerator,SecureTokenGenerator>();
            services.AddScoped<IDateTimeService,     DateTimeService>();

            // ── Notification Services ─────────────────────────────────────────────
            services.AddScoped<IAvailabilityNotifier,    SignalRAvailabilityNotifier>();
            services.AddScoped<IChatNotifier,            SignalRChatNotifier>();
            services.AddScoped<IRadarNotifier,           SignalRRadarNotifier>();
            services.AddScoped<IFcmNotificationService,  FcmNotificationService>();
            // Email: SMTP por defecto; con Email:Provider="resend" usa la API HTTP
            // de Resend (puerto 443), que sí funciona donde SMTP está bloqueado (Render free).
            if (string.Equals(configuration["Email:Provider"], "resend", StringComparison.OrdinalIgnoreCase))
                services.AddHttpClient<IEmailService, ResendEmailService>();
            else
                services.AddScoped<IEmailService,        SmtpEmailService>();

            // ── File Storage ──────────────────────────────────────────────────────
            services.AddScoped<IFileStorageService,      CloudinaryFileStorageService>();

            // ── Excel (import de catálogo mayorista / import de ventas) ───────────
            services.AddScoped<IExcelCatalogParser,      ClosedXmlCatalogParser>();
            services.AddScoped<IExcelSalesImportParser,  ClosedXmlSalesImportParser>();
            services.AddScoped<IExcelProductCatalogParser, ClosedXmlProductCatalogParser>();

            // ── IA (GroqCloud) — asistente de comida, mercado inteligente, predicción ─
            services.AddHttpClient<IAiChatService, GroqAiChatService>();

            // ── Sincronización (ingesta de pushes / export para pull de instalaciones en modo Local) ─
            services.AddScoped<ISyncIngestService, SyncIngestService>();
            services.AddScoped<ISyncExportService, SyncExportService>();

            // ── SignalR ───────────────────────────────────────────────────────────
            services.AddSignalR();

            // ── Memory Cache (para Rate Limiting) ─────────────────────────────────
            services.AddMemoryCache();

            return services;
        }

        // ── Stubs para modo InMemory (desarrollo sin PostgreSQL) ─────────────────

        private sealed class NoOpTenantSchemaManager : ITenantSchemaManager
        {
            public Task CreateSchemaAsync(Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
            public Task DropSchemaAsync(Guid tenantId, CancellationToken ct = default)   => Task.CompletedTask;
            public Task<bool> SchemaExistsAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(true);
        }

        // Búsqueda funcional sobre la BD InMemory: el catálogo vive en OperationsDbContext
        // (Operations.Product, IsPubliclyVisible=true), la info de tienda en StoreDbContext
        // "public" (un solo schema plano en InMemory). Permite probar la app localmente sin PostgreSQL.
        private sealed class InMemorySearchRepository : ISearchRepository
        {
            private readonly StoreDbContext _storeDb;
            private readonly OperationsDbContext _opsDb;
            private readonly TenantManagementDbContext _tenantDb;

            public InMemorySearchRepository(StoreDbContext storeDb, OperationsDbContext opsDb, TenantManagementDbContext tenantDb)
            { _storeDb = storeDb; _opsDb = opsDb; _tenantDb = tenantDb; }

            private async Task<List<(Domain.BoundedContext.StoreManagement.Aggregates.Store Store,
                                     Domain.BoundedContext.Operations.Aggregates.Product Product)>> FilterAsync(
                string? query, string? city, Guid? categoryId, bool? onlyAvailable,
                decimal? minPrice, decimal? maxPrice, Guid? storeId, TenantType? tenantType, CancellationToken ct)
            {
                var stores = await _storeDb.Stores.Where(s => s.IsActive).ToListAsync(ct);
                var products = await _opsDb.Products
                    .Include(p => p.Stocks)
                    .Where(p => p.IsActive && p.ForSale && p.IsPubliclyVisible)
                    .ToListAsync(ct);

                if (tenantType.HasValue)
                {
                    var tenantIds = await _tenantDb.Tenants.Where(t => t.Type == tenantType.Value).Select(t => t.Id).ToListAsync(ct);
                    products = products.Where(p => tenantIds.Contains(p.TenantId)).ToList();
                }

                var q = query?.Trim().ToLowerInvariant();
                var c = city?.Trim().ToLowerInvariant();

                return (from p in products
                        join s in stores on p.TenantId equals s.TenantId
                        select (Store: s, Product: p))
                    .Where(x => string.IsNullOrEmpty(q)  || x.Product.Name.ToLowerInvariant().Contains(q))
                    .Where(x => string.IsNullOrEmpty(c)  || x.Store.Address.City.ToLowerInvariant() == c)
                    .Where(x => !categoryId.HasValue     || x.Product.CategoryId == categoryId.Value)
                    .Where(x => !storeId.HasValue        || x.Store.TenantId == storeId.Value)
                    .Where(x => onlyAvailable != true    || x.Product.TotalStock > 0)
                    .Where(x => !minPrice.HasValue        || x.Product.SellPrice >= minPrice.Value)
                    .Where(x => !maxPrice.HasValue        || x.Product.SellPrice <= maxPrice.Value)
                    .OrderBy(x => x.Product.Name)
                    .ToList();
            }

            private async Task<IEnumerable<ProductSearchResult>> SearchInternalAsync(
                string? query, string? city, Guid? categoryId, bool? onlyAvailable,
                decimal? minPrice, decimal? maxPrice, Guid? storeId, TenantType? tenantType,
                int page, int pageSize, CancellationToken ct)
            {
                var all = await FilterAsync(query, city, categoryId, onlyAvailable, minPrice, maxPrice, storeId, tenantType, ct);
                var categories = await _opsDb.Categories.ToListAsync(ct);

                return all
                    .Skip((Math.Max(page, 1) - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x =>
                    {
                        var cat = categories.FirstOrDefault(k => k.Id == x.Product.CategoryId);
                        return new ProductSearchResult
                        {
                            ProductId          = x.Product.Id,
                            ProductName        = x.Product.Name,
                            ProductDescription = x.Product.Description,
                            Price              = x.Product.SellPrice,
                            Currency           = "CUP",
                            ImageUrl           = x.Product.ImageUrl,
                            IsAvailable        = x.Product.TotalStock > 0,
                            Stock              = x.Product.TotalStock,
                            CategoryName       = cat?.Name,
                            StoreId            = x.Store.TenantId,
                            StoreName          = x.Store.Name,
                            StoreAddress       = x.Store.Address.ToString(),
                            City               = x.Store.Address.City,
                            Latitude           = x.Store.Address.Latitude,
                            Longitude          = x.Store.Address.Longitude,
                            StorePhone         = x.Store.Phone.Value,
                            IsStoreOpen        = x.Store.IsOpenNow(),
                            StoreLogoUrl       = x.Store.LogoUrl,
                            TenantId           = x.Store.TenantId,
                            MinOrderQuantity   = x.Product.MinOrderQuantity
                        };
                    })
                    .ToList();
            }

            public Task<IEnumerable<ProductSearchResult>> SearchProductsAsync(
                string? query, string? city, Guid? categoryId,
                bool? onlyAvailable, decimal? minPrice, decimal? maxPrice,
                Guid? storeId = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
                => SearchInternalAsync(query, city, categoryId, onlyAvailable, minPrice, maxPrice, storeId, null, page, pageSize, ct);

            public async Task<int> CountSearchResultsAsync(
                string? query, string? city, Guid? categoryId,
                bool? onlyAvailable, decimal? minPrice, decimal? maxPrice,
                Guid? storeId = null, CancellationToken ct = default)
                => (await FilterAsync(query, city, categoryId, onlyAvailable, minPrice, maxPrice, storeId, null, ct)).Count;

            public Task<IEnumerable<ProductSearchResult>> GetWholesaleCatalogAsync(
                string? query, string? city, int page = 1, int pageSize = 20, CancellationToken ct = default)
                => SearchInternalAsync(query, city, null, true, null, null, null,
                    Domain.BoundedContext.TenantManagement.Enums.TenantType.Wholesale, page, pageSize, ct);

            public async Task<int> CountWholesaleCatalogAsync(
                string? query, string? city, CancellationToken ct = default)
                => (await FilterAsync(query, city, null, true, null, null, null,
                    Domain.BoundedContext.TenantManagement.Enums.TenantType.Wholesale, ct)).Count;
        }
    }
}
