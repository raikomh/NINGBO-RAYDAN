# Revisión de código — Sin ejecución real (limitación del entorno)

## ⚠️ Importante: esto NO es un test real

Este entorno no tiene el SDK de .NET instalado, así que **no pude compilar,
ejecutar `dotnet test`, ni levantar la API**. Lo que hice fue una revisión
estática manual del código fuente buscando inconsistencias que rompen la
compilación. Esto es útil pero no sustituye una compilación real.

**Acción requerida de tu parte:** abre el proyecto en Visual Studio,
compila, y si salen más errores de los listados aquí, compártemelos.

---

## 🔴 Errores que SÍ rompen la compilación (encontrados)

### 1. `BaseApiController` ya no existe — CRÍTICO

**8 controladores** dependen de una clase `BaseApiController` que definía
`Mediator`, `Ok<T>()` y `Created()`. Esa clase vivía originalmente dentro
del archivo `AuthController.cs`, pero al reescribir ese archivo completo
con refresh tokens, **sobreescribí y perdí la definición de la clase base**.

Afectados:
```
AuthController, StoreController, ProductsController,
CategoriesController, SchedulesController, SearchController,
PaymentsController, UploadsController
```

Esto causa errores como:
```
CS0246: El tipo o el nombre del espacio de nombres 'BaseApiController' no se encontró
```

**Corrección:** recrear el archivo `BaseApiController.cs` como clase
independiente, reusable por todos los controladores.

---

### 2. `PaymentCommands.cs` rompe Clean Architecture y no compila

```csharp
// En BusinessSearcher.Application/Features/.../PaymentCommands.cs
using BusinessSearcher.Infrastructure.Services;   // ← Application NO debe conocer Infrastructure
...
private readonly IPaymentService _paymentService;  // definida en Infrastructure
```

El `.csproj` de `BusinessSearcher.Application` **no tiene** `ProjectReference`
hacia `Infrastructure` (y no debería tenerlo — viola la regla de que las
capas internas no dependen de las externas). Esto da:
```
CS0234: El tipo o nombre de espacio de nombres 'Services' no existe
en el espacio de nombres 'BusinessSearcher.Infrastructure'
```

**Corrección:** mover la interfaz `IPaymentService` (y los records
`PaymentSessionResult`, `PaymentWebhookResult`) a
`Application/Commons/Interfaces/`, dejando solo la implementación
`StripePaymentService` en Infrastructure.

---

### 3. Falta un `using` para `AddNpgsql` (Health Checks)

```csharp
// ServiceCollectionExtensions.cs usa:
services.AddHealthChecks().AddNpgsql(connStr, ...);
// pero falta:
using HealthChecks.NpgSql;
```

Sin ese `using`, el método de extensión `AddNpgsql` no se resuelve:
```
CS1061: 'IHealthChecksBuilder' no contiene una definición para 'AddNpgsql'
```

---

## 🟡 Cosas que revisé y SÍ están correctas

Para que veas que no es una revisión superficial, esto sí lo verifiqué
línea por línea y está bien:

- `IFileStorageService.UploadAsync` — firma coincide entre interfaz,
  implementación (`CloudinaryFileStorageService`) y los 2 lugares donde
  se llama desde `UploadCommands.cs`
- `ITenantSchemaManager` — correctamente ubicada en Application,
  implementada en Infrastructure, sin violar la arquitectura
- `AuthResultDto` — el `record` con `init` property y el uso de
  `with { RefreshToken = null }` en el controller es sintácticamente válido
- `PasswordResetRequestedEvent` / `PasswordChangedEvent` — mismo namespace
  que `Tenant.cs`, sin problema de resolución
- `IPasswordHasher.Verify(password, hash)` — la firma coincide
  exactamente con el `Func<string,string,bool>` que espera
  `Tenant.ChangePassword(...)`
- `AvailabilityHub` — está en `Infrastructure.Services` y `Program.cs`
  tiene el `using` correcto para `MapHub<AvailabilityHub>(...)`

---

## Lo que NO pude verificar sin compilar

- Errores de tipos en argumentos genéricos de MediatR (`IRequestHandler<T,R>`)
- Conflictos de ambigüedad entre `BusinessSearcher.Application.DTOs.TenantManagement.TenantDto`
  y otros tipos con nombres parecidos en distintos namespaces
- Si todos los paquetes NuGet declarados en los `.csproj` existen con
  exactamente esas versiones (algunas pude haberlas inventado por
  aproximación, especialmente `CloudinaryDotNet`, `Stripe.net`, y
  `AspNetCore.HealthChecks.NpgSql` — sus números de versión exactos
  pueden no existir o haber cambiado)
- Errores de EF Core en tiempo de migración (Fluent API mal encadenada,
  nombres de propiedades de owned types)
- Cualquier error que solo aparece en tiempo de ejecución, no de compilación
  (ej: el SQL crudo de `SearchRepository`, que nunca se probó contra una
  base de datos real)

---

## Siguiente paso recomendado

Voy a corregir ahora mismo los 3 errores confirmados arriba. Después de
eso, **el paso que de verdad importa es que tú compiles en Visual Studio**
y me pegues cualquier error adicional — ahí es donde vamos a encontrar el
resto, porque el compilador ve cosas que un grep manual no puede.
