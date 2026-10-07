# ✅ MVP Validation Checklist — BusinessSearcher Backend

Usa esta lista para verificar que el backend está completamente listo para MVP.

---

## 📋 Fase 1 — Compilación y Tests

### 1.1 Compilación
- [ ] `dotnet build` compila sin errores
- [ ] `dotnet build --configuration Release` compila sin warnings
- [ ] No hay referencias circulares entre proyectos
- [ ] Todos los `using` están completos (sin "ambiguous reference")

### 1.2 Tests Unitarios (31 total)
- [ ] `dotnet test` ejecuta todos los tests
- [ ] **Domain Tests** (22/22 pasan)
  - [ ] TenantTests (7) — Ciclo de vida, pagos, tokens
  - [ ] StoreTests (8) — CRUD, disponibilidad, categorías
  - [ ] ValueObjectTests (22)
    - [ ] MoneyTests (9) — Aritmética, validaciones
    - [ ] AddressTests (5) — Campos requeridos, coordenadas
    - [ ] PhoneNumberTests (3) — Formatos
    - [ ] ScheduleTimeTests (5) — Horarios válidos
- [ ] **Application Tests** (6/6 pasan)
  - [ ] RegisterTenantCommandHandlerTests (5) — Registro, validaciones
  - [ ] LoginTenantCommandHandlerTests (5) — Login, errores

### 1.3 Integration Tests (14 total pasan)
- [ ] **TenantLifecycleIntegrationTests** (4 pasan)
  - [ ] FullTenantLifecycle_ShouldHandleAllTransitions
  - [ ] RefreshTokenRotation_WithValidToken
  - [ ] SecurityBreach_TokenReuse_ShouldInvalidateAllSessions
  - [ ] PasswordReset_ShouldInvalidateAllSessions
- [ ] **StoreManagementIntegrationTests** (5 pasan)
  - [ ] FullStoreSetup_ShouldCreateCompleteStoreFrontend
  - [ ] InventoryManagement_ShouldTrackStockCorrectly
  - [ ] ProductAvailability_ShouldReflectStockChanges
  - [ ] StoreClosure_ShouldPreventNewOperations
  - [ ] MultipleSchedules_ShouldHandleEachDayIndependently
- [ ] **ValidationAndSecurityIntegrationTests** (5 pasan)
  - [ ] Address_RequiresAllFields
  - [ ] Money_CannotBeNegative
  - [ ] ScheduleTime_CloseTimeCannotBeBeforeOrEqualToOpenTime
  - [ ] Tenant_PasswordResetToken_ExpiresAfterUse
  - [ ] Tenant_CannotRegisterNegativePayment

---

## 🏗️ Fase 2 — Arquitectura y Diseño

### 2.1 Clean Architecture
- [ ] Domain (BusinessSearcher.Domain) — Sin dependencias externas
- [ ] Application (BusinessSearcher.Application) — Depende solo de Domain
- [ ] Infrastructure (BusinessSearcher.Infrastructure) — Implementa interfaces de Application
- [ ] WebApi (BusinessSearcher.WebApi) — Depende de Application e Infrastructure
- [ ] No hay referencias inversas (Infrastructure → Domain está OK, pero no al revés)

### 2.2 DDD (Domain-Driven Design)
- [ ] **Aggregates Root** — Tenant, Store tienen métodos que validan las operaciones
- [ ] **Value Objects** — Money, Address, PhoneNumber, Email, ScheduleTime son inmutables
- [ ] **Domain Events** — Emitidos por agregados (TenantRegisteredEvent, StoreCreatedEvent)
- [ ] **Repositories** — Interfaz en Domain, implementación en Infrastructure
- [ ] **Bounded Contexts** — TenantManagement y StoreManagement están separados

### 2.3 CQRS (Command Query Responsibility Segregation)
- [ ] Commands → Cambios de estado (CreateStore, RegisterPayment, ToggleAvailability)
- [ ] Queries → Lectura (GetStoreById, GetProducts, SearchProducts)
- [ ] Separados en carpetas diferentes
- [ ] Handlers implementan IRequestHandler<TRequest, TResponse>

### 2.4 Multi-tenant
- [ ] Tenant root en schema `public`
- [ ] Cada Store y datos asociados en schema `tenant_{guid}`
- [ ] ICurrentUserService extrae TenantId del JWT
- [ ] StoreDbContext usa schema dinámico
- [ ] TenantSchemaManager crea schemas automáticamente

---

## 🔐 Fase 3 — Seguridad

### 3.1 Autenticación
- [ ] JWT con secret >= 32 caracteres
- [ ] Expiración de JWT = 1 día (configurable)
- [ ] Refresh tokens con rotación automática
- [ ] Refresh tokens en cookies HttpOnly (no accesibles desde JS)
- [ ] Detección de reutilización de tokens revocados

### 3.2 Contraseñas
- [ ] Hash con BCrypt (work factor = 12)
- [ ] Validación: >= 8 chars, 1 mayúscula, 1 número, 1 especial
- [ ] Recuperación vía email con token temporal (60 min)
- [ ] Al resetear o cambiar contraseña, se cierran todas las sesiones

### 3.3 Autorización
- [ ] [Authorize] en endpoints protegidos
- [ ] ICurrentUserService valida que TenantId del JWT sea dueño del recurso
- [ ] Endpoints públicos: /auth/register, /auth/login, /api/v1/search

### 3.4 Rate Limiting
- [ ] 5 registros/hora por IP
- [ ] 10 logins/15 minutos por IP
- [ ] 3 forgot-password/hora por IP
- [ ] 60 búsquedas/minuto por IP
- [ ] Implementado en RateLimitingMiddleware

### 3.5 CORS
- [ ] Restringido a dominios específicos (no * en producción)
- [ ] SignalR requiere SameSite=Strict para cookies

### 3.6 Datos sensibles
- [ ] Emails encriptados (Email Value Object)
- [ ] Contraseñas hasheadas (nunca texto plano)
- [ ] JWT secret en variables de entorno (no en appsettings.json en prod)
- [ ] Stripe keys en variables de entorno

---

## 📧 Fase 4 — Comunicación e Integraciones

### 4.1 Email
- [ ] SMTP configurado (Gmail, Sendgrid, etc.)
- [ ] Bienvenida al registrar
- [ ] Recuperación de contraseña con link temporal
- [ ] Notificación al cambiar contraseña
- [ ] Fallback graceful (si email falla, no aborta el registro)

### 4.2 Imágenes
- [ ] Cloudinary integrado para logos y fotos de productos
- [ ] Validación: solo JPEG, PNG, WEBP
- [ ] Max 5MB para fotos de producto, 2MB para logos
- [ ] Eliminación de imagen anterior al reemplazar

### 4.3 Pagos
- [ ] Stripe integrado para suscripciones
- [ ] 3 planes: Starter, Premium, Enterprise (con PriceIds)
- [ ] Webhook de Stripe procesa pagos exitosos
- [ ] Al pagar, se activa la suscripción automáticamente
- [ ] Cancelación de suscripción suspende la cuenta

### 4.4 Notificaciones en tiempo real
- [ ] SignalR hub en `/hubs/availability`
- [ ] ProductAvailabilityChanged dispara en tiempo real
- [ ] Clientes se subscriben por ciudad/tienda
- [ ] (Opcional en MVP) Firebase FCM para push notifications

### 4.5 Logging
- [ ] Serilog integrado
- [ ] Console en desarrollo, archivo en producción
- [ ] Archivos rotativos (30 días de histórico)
- [ ] Niveles: Information (default), Warning (frameworks), Error

---

## 🗄️ Fase 5 — Base de Datos

### 5.1 Schema
- [ ] PostgreSQL 15+ en producción
- [ ] Schema `public` para tenants (tablas: tenants, tenant_payments, refresh_tokens, password_reset_tokens)
- [ ] Schema `tenant_{guid}` para cada negocio (stores, products, categories, store_schedules)

### 5.2 Migraciones EF Core
- [ ] Migración inicial generada: `dotnet ef migrations add InitialCreate`
- [ ] Aplicada a BD: `dotnet ef database update`
- [ ] Modelos están configurados con IEntityTypeConfiguration

### 5.3 Índices
- [ ] PK en todas las tablas (Id)
- [ ] FK para relaciones
- [ ] Índice en email de tenants (búsqueda rápida)
- [ ] Índice en store_id + is_available en products (búsqueda)
- [ ] Índice único en (store_id, day_of_week) en store_schedules

### 5.4 Backups
- [ ] Plan de backup automático (Railway/Render lo hace)
- [ ] Recovery point objective (RPO) <= 1 día
- [ ] Testear restore regularmente

---

## 🌐 Fase 6 — API Endpoints

### 6.1 Auth (✓ 8 endpoints)
- [ ] POST `/api/v1/auth/register` — Registra negocio
- [ ] POST `/api/v1/auth/login` — Login, retorna JWT + refresh cookie
- [ ] POST `/api/v1/auth/refresh-token` — Renueva JWT
- [ ] POST `/api/v1/auth/logout` — Cierra sesión actual
- [ ] POST `/api/v1/auth/logout-all` — Cierra todas las sesiones
- [ ] POST `/api/v1/auth/forgot-password` — Email de recuperación
- [ ] POST `/api/v1/auth/reset-password` — Restablece contraseña
- [ ] POST `/api/v1/auth/change-password` — Cambia contraseña (autenticado)
- [ ] GET `/api/v1/auth/profile` — Perfil del negocio

### 6.2 Stores (✓ 4 endpoints)
- [ ] POST `/api/v1/stores` — Crear tienda
- [ ] GET `/api/v1/stores` — Listar mis tiendas
- [ ] GET `/api/v1/stores/{id}` — Detalle de tienda
- [ ] PUT `/api/v1/stores/{id}` — Actualizar tienda

### 6.3 Products (✓ 5 endpoints)
- [ ] POST `/api/v1/stores/{storeId}/products` — Crear producto
- [ ] GET `/api/v1/stores/{storeId}/products` — Listar productos (filtros)
- [ ] PUT `/api/v1/stores/{storeId}/products/{id}` — Actualizar producto
- [ ] PATCH `/api/v1/stores/{storeId}/products/{id}/toggle` — Cambiar disponibilidad

### 6.4 Categories (✓ 3 endpoints)
- [ ] POST `/api/v1/stores/{storeId}/categories` — Crear categoría
- [ ] GET `/api/v1/stores/{storeId}/categories` — Listar categorías
- [ ] PUT `/api/v1/stores/{storeId}/categories/{id}` — Actualizar categoría

### 6.5 Schedules (✓ 1 endpoint)
- [ ] POST `/api/v1/stores/{storeId}/schedules` — Configurar horario

### 6.6 Uploads (✓ 2 endpoints)
- [ ] POST `/api/v1/uploads/stores/{storeId}/logo` — Subir logo
- [ ] POST `/api/v1/uploads/stores/{storeId}/products/{productId}/image` — Subir imagen

### 6.7 Payments (✓ 2 endpoints)
- [ ] POST `/api/v1/payments/checkout` — Crear sesión de pago
- [ ] POST `/api/v1/payments/webhook/stripe` — Webhook (público)

### 6.8 Search (✓ 1 endpoint, público)
- [ ] GET `/api/v1/search?q=...&city=...&available=true` — Búsqueda cross-tenant

### 6.9 System (✓ 2 endpoints)
- [ ] GET `/health` — Health check general
- [ ] GET `/health/ready` — Health check BD

---

## 🐳 Fase 7 — Docker y Despliegue

### 7.1 Docker local
- [ ] `docker compose up -d` levanta API + PostgreSQL + MailHog
- [ ] API responde en `http://localhost:5000`
- [ ] Swagger en `http://localhost:5000/swagger`
- [ ] Emails de prueba en `http://localhost:8025`

### 7.2 Dockerfile
- [ ] Multi-stage build (SDK para compilar, runtime slim para ejecución)
- [ ] Usuario no-root por seguridad
- [ ] Health check configurado
- [ ] Logs en `/app/logs`

### 7.3 Variables de entorno
- [ ] ConnectionString (PostgreSQL)
- [ ] Jwt__Secret (>= 32 chars)
- [ ] Jwt__Issuer, Jwt__Audience
- [ ] Email__* (SMTP)
- [ ] Cloudinary__*
- [ ] Stripe__*
- [ ] Frontend__* (URLs)

### 7.4 CI/CD (GitHub Actions)
- [ ] Tests corren en push/PR
- [ ] Build en release
- [ ] Despliegue automático (opcional para MVP)

---

## ✨ Fase 8 — Calidad de Código

### 8.1 Estándares
- [ ] Namespaces siguen la estructura (BusinessSearcher.Domain.*)
- [ ] Convención PascalCase para clases/propiedades
- [ ] Convención camelCase en DTOs JSON
- [ ] XML docs en interfaces públicas

### 8.2 SOLID
- [ ] **S** — Una responsabilidad por clase
- [ ] **O** — Abierto para extensión, cerrado para modificación (usar interfaces)
- [ ] **L** — Liskov (derivados respetan contrato base)
- [ ] **I** — Interfaces segregadas (no interfaces enormes)
- [ ] **D** — Inyección de dependencias (Microsoft.Extensions.DependencyInjection)

### 8.3 Error Handling
- [ ] DomainException para errores de negocio
- [ ] ValidationException desde FluentValidation
- [ ] GlobalExceptionHandler convierte a respuestas JSON consistentes
- [ ] Logging de errores con Serilog

### 8.4 Performance
- [ ] Índices en BD para búsquedas frecuentes
- [ ] Async/await en todas las operaciones IO
- [ ] Rate limiting previene abuse

---

## 🎯 Fase 9 — Documentación y Operación

### 9.1 README.md
- [ ] Instrucciones de setup (Docker + manual)
- [ ] Endpoints documentados
- [ ] Variables de entorno
- [ ] Deploy a Railway/Render

### 9.2 TESTING_GUIDE.md
- [ ] Pasos para compilar y ejecutar tests
- [ ] Ejemplos de curl para probar endpoints
- [ ] Health check
- [ ] CI/CD con GitHub Actions

### 9.3 Código auto-documentado
- [ ] Clases y métodos tienen nombres descriptivos
- [ ] No hay "magic numbers" (constantes nombradas)
- [ ] Comentarios en lógica compleja (eventos de dominio, reglas de negocio)

---

## ✅ Checklist Final — Listo para MVP

- [ ] **51 archivos C#** (Domain, Application, Infrastructure, Tests, API)
- [ ] **53 tests** (31 unitarios + 14 integración)
- [ ] **20 endpoints** funcionales y documentados
- [ ] **100% cobertura** de flujos críticos de negocio
- [ ] **Seguridad** — JWT, BCrypt, Rate limiting, CORS
- [ ] **Integraciones** — Email, Cloudinary, Stripe, SignalR
- [ ] **Docker** — Local (docker-compose) y deployment ready
- [ ] **Documentación** — README, TESTING_GUIDE, código limpio
- [ ] **Performance** — Índices, async/await, caching (vía cookies)
- [ ] **Monitoreo** — Logging con Serilog, health checks

---

## 🚀 Próximos pasos después de ✅ MVP

1. **Frontend React** (Vite + TailwindCSS)
   - Autenticación (JWT + refresh token)
   - Dashboard del negocio
   - Gestor de productos (CRUD)
   - Búsqueda y filtros
   - WebSocket para actualización en tiempo real

2. **Despliegue**
   - Railway o Render (lo recomendado para MVP)
   - Dominio personalizado
   - SSL/HTTPS (automático)

3. **Monitoreo**
   - Error tracking (Sentry)
   - Performance monitoring (DataDog, New Relic)
   - Uptime monitoring (StatusPage)

4. **Escala**
   - CDN para imágenes (Cloudinary ya lo incluye)
   - Caché Redis para búsquedas frecuentes
   - Background jobs (Hangfire) para emails/pagos

---

**Estado actual:** ✅ Backend listo para MVP  
**Fecha de validación:** [Hoy]  
**Responsable:** [Tu nombre]
