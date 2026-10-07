# NINGBO RAYDAN

Proyecto de gestión de negocios (TPV, inventario, catálogos por punto de venta y búsqueda pública).

## Estructura

- `businesssearcher-frontend/`: aplicación web (React + Vite + MUI). Se ejecuta con `npm run dev` (puerto 4321).
- `online/`: backend .NET 8 (`BusinessSearcher.sln`, proyecto de arranque `BusinessSearcher.WebApi`). Se ejecuta con `dotnet run --project BusinessSearcher.WebApi` (puerto 62560, HTTPS 62559).

## Configuración

Este repositorio no contiene secretos. Los valores marcados con `CHANGE_ME` en `online/BusinessSearcher.WebApi/appsettings*.json` se configuran fuera del repositorio:

- Con `dotnet user-secrets` en `BusinessSearcher.WebApi`, o con variables de entorno. Claves necesarias: `ConnectionStrings:DefaultConnection`, `Jwt:Secret`, `Email:SmtpPassword`, `Stripe:SecretKey`, `Stripe:WebhookSecret`, `Groq:ApiKey`, `Cloudinary:ApiKey` y `Cloudinary:ApiSecret`.
- `SEED_ADMIN_PASSWORD`: contraseña del usuario administrador creado al iniciar en una base vacía. Si no se define, se genera una aleatoria.

Para desarrollo local con el backend, define `VITE_DEV_API_TARGET=http://localhost:62560` en `businesssearcher-frontend/.env.local`.
