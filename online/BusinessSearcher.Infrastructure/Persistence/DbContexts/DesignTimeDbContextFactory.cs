using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BusinessSearcher.Infrastructure.Persistence.DbContexts
{
    /// <summary>
    /// Usado exclusivamente por "dotnet ef migrations" en tiempo de diseño.
    /// No se usa en runtime.
    /// </summary>
    public class TenantManagementDbContextFactory
        : IDesignTimeDbContextFactory<TenantManagementDbContext>
    {
        public TenantManagementDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connString = config.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=business_searcher;Username=postgres;Password=postgres";

            var optionsBuilder = new DbContextOptionsBuilder<TenantManagementDbContext>();
            // UseSnakeCaseNamingConvention DEBE ir aquí, igual que en AddInfrastructure: EF usa
            // esta fábrica con prioridad sobre el host de la app, así que si falta, el modelo de
            // diseño no coincide con el de ejecución y las migraciones salen con los nombres de
            // columna en PascalCase. Fue lo que creó "LastSyncedAt" y "SyncApiKeyHash" en la base
            // mientras la aplicación consultaba last_synced_at y sync_api_key_hash.
            optionsBuilder.UseNpgsql(connString)
                .UseSnakeCaseNamingConvention();

            return new TenantManagementDbContext(optionsBuilder.Options);
        }
    }

    /// <summary>
    /// Usado exclusivamente por "dotnet ef migrations" para el contexto del radar.
    /// Mantiene su propia tabla de historial de migraciones para no colisionar con TenantManagement.
    /// </summary>
    public class RadarDbContextFactory
        : IDesignTimeDbContextFactory<RadarDbContext>
    {
        public RadarDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connString = config.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=business_searcher;Username=postgres;Password=postgres";

            var optionsBuilder = new DbContextOptionsBuilder<RadarDbContext>();
            optionsBuilder.UseNpgsql(connString,
                    o => o.MigrationsHistoryTable("__ef_migrations_radar", "public"))
                .UseSnakeCaseNamingConvention();

            return new RadarDbContext(optionsBuilder.Options);
        }
    }

    /// <summary>Design-time factory del contexto Operations (TPV/ERP).</summary>
    public class OperationsDbContextFactory
        : IDesignTimeDbContextFactory<OperationsDbContext>
    {
        public OperationsDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connString = config.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=business_searcher;Username=postgres;Password=postgres";

            var optionsBuilder = new DbContextOptionsBuilder<OperationsDbContext>();
            optionsBuilder.UseNpgsql(connString,
                    o => o.MigrationsHistoryTable("__ef_migrations_operations", "public"))
                .UseSnakeCaseNamingConvention();

            return new OperationsDbContext(optionsBuilder.Options);
        }
    }

    /// <summary>
    /// StoreDbContext usa schemas dinámicos por tenant (no se puede migrar con EF Core).
    /// Su estructura se gestiona via TenantSchemaManager (DDL manual).
    /// </summary>
    public class StoreDbContextFactory
        : IDesignTimeDbContextFactory<StoreDbContext>
    {
        public StoreDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<StoreDbContext>();
            optionsBuilder.UseNpgsql(
                "Host=localhost;Port=5432;Database=business_searcher;Username=postgres;Password=postgres",
                o => o.MigrationsHistoryTable("__ef_migrations_store", "public"));

            return new StoreDbContext(optionsBuilder.Options, "design_time");
        }
    }
}
