using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Domain.Exceptions;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BusinessSearcher.Infrastructure.Persistence.UnitOfWork
{
    public class TenantManagementUnitOfWork : IUnitOfWork
    {
        private readonly TenantManagementDbContext _context;
        private IDbContextTransaction? _transaction;

        public TenantManagementUnitOfWork(TenantManagementDbContext context)
            => _context = context;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is not null)
            {
                await _context.SaveChangesAsync(cancellationToken);
                await _transaction.CommitAsync(cancellationToken);
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is not null)
                await _transaction.RollbackAsync(cancellationToken);
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
    }

    public class RadarUnitOfWork : IRadarUnitOfWork
    {
        private readonly RadarDbContext _context;
        private IDbContextTransaction? _transaction;

        public RadarUnitOfWork(RadarDbContext context) => _context = context;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is not null)
            {
                await _context.SaveChangesAsync(cancellationToken);
                await _transaction.CommitAsync(cancellationToken);
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is not null)
                await _transaction.RollbackAsync(cancellationToken);
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
    }

    public class OperationsUnitOfWork : IOperationsUnitOfWork
    {
        private readonly OperationsDbContext _context;
        private IDbContextTransaction? _transaction;

        public OperationsUnitOfWork(OperationsDbContext context) => _context = context;

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Otra operación ya guardó una fila que este request también tocó (p.ej. dos ventas
                // descontando el mismo stock casi al mismo tiempo). Recarga esas filas con el valor
                // real de la base y deja que la capa Application decida cómo reintentar: no depende
                // de EF Core, así que la excepción de dominio no filtra tipos de Infraestructura.
                foreach (var entry in ex.Entries)
                    await entry.ReloadAsync(cancellationToken);
                throw new ConcurrencyConflictException(
                    "Otra operación modificó el mismo registro al mismo tiempo.", ex);
            }
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is not null)
            {
                await _context.SaveChangesAsync(cancellationToken);
                await _transaction.CommitAsync(cancellationToken);
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is not null)
                await _transaction.RollbackAsync(cancellationToken);
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
    }

    public class StoreUnitOfWorkImpl : IStoreUnitOfWork
    {
        private readonly StoreDbContext _context;
        private IDbContextTransaction? _transaction;

        public StoreUnitOfWorkImpl(StoreDbContext context) => _context = context;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is not null)
            {
                await _context.SaveChangesAsync(cancellationToken);
                await _transaction.CommitAsync(cancellationToken);
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is not null)
                await _transaction.RollbackAsync(cancellationToken);
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
    }
}

namespace BusinessSearcher.Infrastructure.SchemaManagement
{
    public class TenantSchemaManager : ITenantSchemaManager
    {
        private readonly string _connectionString;
        private readonly ILogger<TenantSchemaManager> _logger;

        public TenantSchemaManager(string connectionString, ILogger<TenantSchemaManager> logger)
        {
            _connectionString = connectionString;
            _logger           = logger;
        }

        public async Task CreateSchemaAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var schema = $"tenant_{tenantId:N}";
            _logger.LogInformation("Creando schema PostgreSQL: {Schema}", schema);

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd  = new NpgsqlCommand(GetCreateSchemaDdl(schema), conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Schema {Schema} creado exitosamente", schema);
        }

        public async Task DropSchemaAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var schema = $"tenant_{tenantId:N}";
            _logger.LogWarning("Eliminando schema PostgreSQL: {Schema}", schema);

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd  = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<bool> SchemaExistsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var schema = $"tenant_{tenantId:N}";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(
                "SELECT COUNT(1) FROM information_schema.schemata WHERE schema_name = @schema", conn);
            cmd.Parameters.AddWithValue("@schema", schema);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) > 0;
        }

        private static string GetCreateSchemaDdl(string schema) => $@"
            CREATE SCHEMA IF NOT EXISTS ""{schema}"";

            CREATE TABLE IF NOT EXISTS ""{schema}"".stores (
                id              UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
                tenant_id       UUID          NOT NULL,
                name            VARCHAR(150)  NOT NULL,
                description     VARCHAR(500),
                address_street  VARCHAR(300)  NOT NULL,
                address_city    VARCHAR(100)  NOT NULL,
                address_state   VARCHAR(100)  NOT NULL,
                address_country VARCHAR(100)  NOT NULL,
                latitude        DOUBLE PRECISION,
                longitude       DOUBLE PRECISION,
                phone           VARCHAR(30)   NOT NULL,
                logo_url        TEXT,
                is_active       BOOLEAN       NOT NULL DEFAULT true,
                created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
                updated_at      TIMESTAMPTZ
            );

            CREATE TABLE IF NOT EXISTS ""{schema}"".store_schedules (
                id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                store_id    UUID         NOT NULL REFERENCES ""{schema}"".stores(id) ON DELETE CASCADE,
                day_of_week VARCHAR(15)  NOT NULL,
                open_time   TIME         NOT NULL,
                close_time  TIME         NOT NULL,
                is_closed   BOOLEAN      NOT NULL DEFAULT false,
                created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                updated_at  TIMESTAMPTZ,
                UNIQUE(store_id, day_of_week)
            );

            CREATE INDEX IF NOT EXISTS ""idx_{schema}_stores_city""
                ON ""{schema}"".stores(address_city);
            CREATE INDEX IF NOT EXISTS ""idx_{schema}_stores_active""
                ON ""{schema}"".stores(is_active);
        ";
    }
}
