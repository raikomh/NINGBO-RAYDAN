using BusinessSearcher.API.Extensions;
using BusinessSearcher.API.Middleware;
using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.Extensions;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.ValueObjects;
using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Infrastructure.Extensions;
using BusinessSearcher.Infrastructure.Logging;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using BusinessSearcher.Infrastructure.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// El entorno "Neon" corre local contra la BD Neon. Por defecto los user-secrets solo
// se cargan en Development, así que aquí los añadimos también para "Neon": así la
// cadena de conexión y las API keys quedan fuera del repo (user-secrets), no en
// appsettings ni en launchSettings (que están versionados).
if (builder.Environment.IsEnvironment("Neon"))
    builder.Configuration.AddUserSecrets<Program>(optional: true);

// ── Logging (Serilog) ────────────────────────────────────────────────────────────
builder.ConfigureSerilog();

// ── Controllers ──────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy        = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        opts.JsonSerializerOptions.DefaultIgnoreCondition      =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ── Límite de tamaño de archivos (para upload de imágenes) ───────────────────────
builder.Services.Configure<FormOptions>(opt =>
{
    opt.MultipartBodyLengthLimit = 5_242_880; // 5MB
});

// ── Capas: Application → Infrastructure ─────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Auth, CORS, Swagger ──────────────────────────────────────────────────────────
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddCorsPolicy(builder.Configuration);
builder.Services.AddSwaggerWithJwt();

// ── Middleware personalizado ──────────────────────────────────────────────────────
builder.Services.AddTransient<GlobalExceptionHandler>();
builder.Services.AddTransient<AuditLogMiddleware>();

var app = builder.Build();

// "Neon" es un entorno local que apunta a la BD de Neon: se comporta como
// Development (Swagger, EnsureCreated, sin redirección HTTPS) pero contra la nube.
var isLocalEnv = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Neon");

// ── Migraciones + seed ────────────────────────────────────────────────────────────
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TenantManagementDbContext>();

    // Contra una base relacional (PostgreSQL/Neon) SIEMPRE se aplican migraciones:
    // EnsureCreated no sirve con Neon porque intenta conectarse a la BD 'postgres'
    // para CREATE DATABASE y el pooler lo rechaza. Solo InMemory usa EnsureCreated.
    if (db.Database.IsRelational())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();

    // Radar (red colaborativa de disponibilidad): tiene su propio historial de
    // migraciones (__ef_migrations_radar). En bases relacionales aplica la migración;
    // en InMemory (tests locales) usa EnsureCreated.
    var radarDb = scope.ServiceProvider.GetRequiredService<RadarDbContext>();
    if (radarDb.Database.IsRelational())
        radarDb.Database.Migrate();
    else
        radarDb.Database.EnsureCreated();

    // Operations (TPV/ERP): historial de migraciones __ef_migrations_operations.
    var opsDb = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
    if (opsDb.Database.IsRelational())
        opsDb.Database.Migrate();
    else
        opsDb.Database.EnsureCreated();

    // Seed admin (solo si no existe). NO corre en modo Local: consumiría el único
    // cupo de tenant de la instalación antes de que el dueño real se registre.
    var isLocalDeployment = BusinessSearcher.Application.Commons.DeploymentMode.IsLocal(builder.Configuration);
    if (isLocalDeployment)
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>()
            .LogInformation("=== Modo Local activo: instalación de 1 solo negocio, sin seed admin ===");

    const string adminEmail = "admin@businesssearcher.dev";
    if (!isLocalDeployment && !db.Tenants.Any(t => t.Email.Value == adminEmail))
    {
        var hasher = scope.ServiceProvider.GetRequiredService<BusinessSearcher.Application.Commons.Interfaces.IPasswordHasher>();
        var passwordHash = hasher.Hash("Another1241");
        var admin = Tenant.Create("Admin Test", adminEmail, passwordHash, TenantType.Retail);
        admin.Activate();
        // Admin no necesita pasar por verificación de email
        admin.VerifyEmail(admin.RequestEmailVerification("seed-admin-token", TimeSpan.FromDays(1)).Token);
        // …ni por aprobación: el seed lo deja listo para iniciar sesión.
        admin.Approve();
        db.Tenants.Add(admin);
        db.SaveChanges();

        // El admin es un tenant como cualquier otro: necesita su propio schema
        // de Postgres para poder tener su propia tienda (Store, productos, etc.).
        var schemaManager = scope.ServiceProvider.GetRequiredService<ITenantSchemaManager>();
        schemaManager.CreateSchemaAsync(admin.Id).GetAwaiter().GetResult();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("=== USUARIO ADMIN CREADO ===");
        logger.LogInformation("  Email:    {Email}", adminEmail);
        logger.LogInformation("  Password: Another1241");
        logger.LogInformation("  (activo, email verificado)");
        logger.LogInformation("============================");
    }

    // Tienda demo con productos: SOLO en modo InMemory (pruebas locales). En
    // PostgreSQL/Neon los productos viven en el schema por-tenant, así que este
    // seed en "public" no aplica y no debe ensuciar la base real.
    var useInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");
    var storeDb = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
    if (useInMemory && !storeDb.Stores.Any())
    {
        var seedTenant = db.Tenants.FirstOrDefault(t => t.Email.Value == "admin@businesssearcher.dev")
                         ?? db.Tenants.FirstOrDefault();
        if (seedTenant is not null)
        {
            var address = new Address("Calle 23 #456, Vedado", "Habana", "La Habana", "Cuba", 23.113, -82.366);
            var phone   = new PhoneNumber("+53 55555555");
            var store   = Store.Create(seedTenant.Id, "Mercado Demo", address, phone, "Tienda de prueba (datos locales)");

            storeDb.Stores.Add(store);
            storeDb.SaveChanges();

            // Catálogo demo: vive en Operations (Inventario), con IsPubliclyVisible=true
            // para que aparezca en la búsqueda pública de la app.
            var opsDbSeed = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
            var warehouse = BusinessSearcher.Domain.BoundedContext.Operations.Aggregates.Warehouse.Create(
                seedTenant.Id, "Almacén principal");
            var alimentos = BusinessSearcher.Domain.BoundedContext.Operations.Aggregates.Category.Create(
                seedTenant.Id, "Alimentos");

            var demoProducts = new[]
            {
                ("Pollo entero",           "Pollo fresco por libra", 300m, 20),
                ("Arroz (1 lb)",           "Arroz blanco",           150m, 50),
                ("Frijoles negros (1 lb)", "Frijoles secos",         180m, 30),
                ("Aceite (1 L)",           "Aceite vegetal",         600m, 15),
                ("Leche en polvo (500 g)", (string?)null,            450m, 8),
            };

            opsDbSeed.Warehouses.Add(warehouse);
            opsDbSeed.Categories.Add(alimentos);
            foreach (var (name, description, price, stock) in demoProducts)
            {
                var product = BusinessSearcher.Domain.BoundedContext.Operations.Aggregates.Product.Create(
                    seedTenant.Id, name, price, price, description: description, categoryId: alimentos.Id,
                    forSale: true, isPubliclyVisible: true);
                product.AdjustStock(warehouse.Id, stock);
                opsDbSeed.Products.Add(product);
            }
            opsDbSeed.SaveChanges();

            var slog = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            slog.LogInformation("=== TIENDA DEMO CREADA: 'Mercado Demo' con 5 productos ===");
        }
    }
}

// ── Herramienta de prueba de carga (SOLO entornos locales, no se despliega) ───────
// Genera N clientes-vendedores aprobados, cada uno con un producto disponible y
// ubicación aleatoria en La Habana, para poder medir cómo responde /api/v1/search
// (el buscador público que alimenta el mapa) con un volumen grande de publicaciones.
if (isLocalEnv)
{
    app.MapPost("/api/v1/dev/seed-load-test", async (int count, RadarDbContext radarDb) =>
    {
        count = Math.Clamp(count, 1, 50_000);
        var rnd = new Random(42);
        var clients  = new List<Client>(count);
        var products = new List<ClientProduct>(count);

        for (var i = 0; i < count; i++)
        {
            var client = Client.Create($"LoadTest User {i}", $"loadtest{i}@test.local", "x");
            client.Approve();
            var lat = 23.05 + rnd.NextDouble() * 0.15; // bounding box aprox. de La Habana
            var lng = -82.45 + rnd.NextDouble() * 0.25;
            client.SetLocation(lat, lng, "La Habana");
            clients.Add(client);

            products.Add(ClientProduct.Create(client.Id, $"Producto de prueba {i}", 50m + i % 500, "CUP"));
        }

        radarDb.Clients.AddRange(clients);
        radarDb.ClientProducts.AddRange(products);
        await radarDb.SaveChangesAsync();

        return Results.Ok(new { seededClients = clients.Count, seededProducts = products.Count });
    });
}

// ── Pipeline HTTP ─────────────────────────────────────────────────────────────────
if (isLocalEnv)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BusinessSearcher API v1");
        c.RoutePrefix = "swagger";
    });
}

// Rate limiting (antes de todo para bloquear lo antes posible)
app.UseMiddleware<RateLimitingMiddleware>();

// Manejo global de errores
app.UseMiddleware<GlobalExceptionHandler>();

if (!isLocalEnv)
    app.UseHttpsRedirection();
app.UseCors("DefaultPolicy");

app.UseAuthentication();
app.UseAuthorization();

// Auditoría del TPV: después de Auth para leer ICurrentUserService y el status final.
app.UseMiddleware<AuditLogMiddleware>();

app.MapControllers();

// SignalR Hubs
app.MapHub<AvailabilityHub>("/hubs/availability");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<RadarHub>("/hubs/radar");

// Health Checks
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();
