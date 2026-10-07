# Correcciones aplicadas sobre el código original

## Domain

| Archivo | Problema original | Corrección |
|---|---|---|
| `Entity.cs` | Typo `DomianEvents` | Renombrado a `DomainEvents` |
| `DomainExceptions.cs` | Nombre en plural, inconsistente con .NET | Renombrado a `DomainException` (singular) |
| `Email.cs` | Validación débil (`Contains('@')`) | Regex robusto |
| `Plan.cs` | Enum ubicado en `ValueObjects/` | Movido a `Enums/` |
| `Tenant.cs` | Sin `PasswordHash`, sin factory method | Agregado `PasswordHash`, constructor privado + `Tenant.Create(...)` |
| `Store.cs` | **Sin `TenantId`** (rompía el multi-tenant) | Agregado `TenantId` requerido en el constructor |
| `Store.cs` | `StoreFactory` en Application creaba el aggregate | Eliminado — ahora `Store.Create(...)` está en el propio aggregate |
| `Product.cs` | `isActive` (minúscula), `Active()` poco expresivo | `IsAvailable` (PascalCase) + `ToggleAvailability()` |
| Eventos de dominio | Duplicados en `Domain/Events/` y en las entidades | Un solo lugar: dentro del aggregate que los genera |
| `TenantPayment.cs` | Validación `amount >= 0` (permitía pagos de $0) | Corregido a `amount > 0` |

## Application

| Archivo | Problema original | Corrección |
|---|---|---|
| `IUnitOfWortk.cs` | Typo en el nombre | Renombrado a `IUnitOfWork.cs` |
| `TransactionBehavior.cs` | `using System.Windows.Input` (WPF, no compila en Web API) | Eliminado; ahora usa `ITransactionalCommand` + `IUnitOfWork` |
| `ToggleProductStatusCommandHandler` | Modificaba `Product` directamente, sin pasar por el Aggregate Root | Ahora usa `store.ToggleProductAvailability(productId)` |
| `RegisterTenantCommandHandler` | No recibía ni hasheaba contraseña | Agregado `IPasswordHasher`, hashea antes de `Tenant.Create(...)` |
| `CreateStoreCommandHandler` | Usaba `StoreFactory` externo | Usa `Store.Create(...)` directamente (encapsulamiento correcto) |

## Infrastructure (estaba vacía — implementación añadida completa)

- `TenantRepository` — implementación completa de `ITenantRepository`
- `StoreRepository` — implementación completa de `IStoreRepository`
- `SearchRepository` — búsqueda cross-tenant con SQL crudo (Npgsql) sobre todos los schemas activos
- `TenantSchemaManager` — crea/elimina schemas PostgreSQL dinámicamente (`tenant_{guid}`)
- `ServiceCollectionExtensions` — registro completo de DI (DbContexts, repos, JWT, BCrypt, SignalR, FCM)
- `JwtTokenService`, `PasswordHasher`, `CurrentUserService` — implementados desde cero
- `SignalRAvailabilityNotifier` + `AvailabilityHub` — notificación en tiempo real al cambiar disponibilidad
- `FcmNotificationService` — integración con Firebase Cloud Messaging

## API (estaba vacía — implementación añadida completa)

- `Program.cs` — registra JWT, EF Core, MediatR, FluentValidation, CORS, Swagger, SignalR
- `AuthController`, `StoreController`, `ProductsController`, `CategoriesController`, `SchedulesController` — implementados
- `SearchController` — endpoint público (sin auth) para la búsqueda cross-tenant
- `GlobalExceptionHandler` — middleware que traduce `DomainException`/`ValidationException` a respuestas JSON consistentes
- `appsettings.json` / `appsettings.Development.json` — configuración de JWT, connection string, CORS, Firebase

## Paquetes NuGet añadidos por proyecto

**Domain** — ninguno (regla de Clean Architecture: el dominio no depende de frameworks)

**Application**
```
MediatR 12.4.1
FluentValidation 11.10.0
FluentValidation.DependencyInjectionExtensions 11.10.0
```

**Infrastructure**
```
Microsoft.EntityFrameworkCore 8.0.10
Microsoft.EntityFrameworkCore.Design 8.0.10
Npgsql.EntityFrameworkCore.PostgreSQL 8.0.10
EFCore.NamingConventions 8.0.3
Microsoft.IdentityModel.Tokens 8.2.1
System.IdentityModel.Tokens.Jwt 8.2.1
BCrypt.Net-Next 4.0.3
Microsoft.AspNetCore.SignalR.Common 8.0.10
FirebaseAdmin 3.1.0
```

**WebApi**
```
Microsoft.AspNetCore.Authentication.JwtBearer 8.0.10
Microsoft.AspNetCore.SignalR 1.2.0
Swashbuckle.AspNetCore 6.9.0
```

## Pendiente de tu parte
1. Generar tu propia clave JWT secreta (mínimo 32 caracteres) en `appsettings.Development.json`
2. Crear un proyecto en Firebase Console y descargar `serviceAccountKey.json` si vas a usar push notifications ahora
3. Tener PostgreSQL corriendo localmente con la base `business_searcher_dev` creada
4. Ejecutar la migración inicial de EF Core (ver README.md)
