# BusinessSearcher — Backend corregido

Backend SaaS multi-tenant para gestión de mipymes de comida, con búsqueda
pública de productos en tiempo real (disponibilidad + ubicación de tienda).

## Stack
- .NET 8 / ASP.NET Core Web API
- Clean Architecture + DDD (4 capas: Domain, Application, Infrastructure, WebApi)
- PostgreSQL multi-tenant (schema `public` + un schema `tenant_{guid}` por negocio)
- EF Core 8 + Npgsql
- MediatR (CQRS) + FluentValidation
- JWT (autenticación) + BCrypt (hash de contraseñas)
- SignalR (disponibilidad de productos en tiempo real)
- Firebase Admin SDK (notificaciones push)

## Cómo abrir en Visual Studio 2022
1. Abre `BusinessSearcher.sln`
2. Click derecho en la solución → Restaurar paquetes NuGet
3. Configura tu cadena de conexión en `BusinessSearcher.WebApi/appsettings.Development.json`
4. Establece `BusinessSearcher.WebApi` como proyecto de inicio
5. F5 para ejecutar — Swagger abrirá en `/swagger`

## Pasos para la base de datos
```bash
cd BusinessSearcher.WebApi
dotnet ef migrations add InitialCreate --project ../BusinessSearcher.Infrastructure --context TenantManagementDbContext
dotnet ef database update --project ../BusinessSearcher.Infrastructure --context TenantManagementDbContext
```

Nota: el schema de cada tenant (`tenant_{guid}`) se crea dinámicamente en
tiempo de ejecución vía `TenantSchemaManager` cuando un negocio se registra
(no usa migraciones EF para esa parte, usa SQL DDL directo porque el número
de schemas es dinámico).

## Endpoints principales

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| POST | /api/v1/auth/register | No | Registra un tenant nuevo |
| POST | /api/v1/auth/login | No | Login, retorna JWT |
| GET  | /api/v1/auth/profile | Sí | Perfil del tenant autenticado |
| POST | /api/v1/stores | Sí | Crea tienda |
| GET  | /api/v1/stores/{id} | Sí | Detalle de tienda |
| GET  | /api/v1/stores | Sí | Lista tiendas del tenant |
| POST | /api/v1/stores/{storeId}/products | Sí | Crea producto |
| PATCH| /api/v1/stores/{storeId}/products/{id}/toggle | Sí | Cambia disponibilidad (dispara SignalR) |
| POST | /api/v1/stores/{storeId}/categories | Sí | Crea categoría |
| POST | /api/v1/stores/{storeId}/schedules | Sí | Configura horario de un día |
| GET  | /api/v1/search?q=&city=&available=true | No | Búsqueda pública cross-tenant |

SignalR Hub: `/hubs/availability` — método cliente `ProductAvailabilityChanged`

## Resumen de correcciones aplicadas sobre tu código original
Ver el archivo `CORRECCIONES.md` para el detalle completo de cada cambio.
