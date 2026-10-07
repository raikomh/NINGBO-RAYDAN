# 🚀 Deployment Guide — BusinessSearcher MVP

Guía paso a paso para desplegar el backend a producción.

---

## Opción 1: Railway.app (RECOMENDADO para MVP)

### Por qué Railway
- Gratis hasta 5GB/mes (suficiente para MVP)
- Database PostgreSQL incluida
- Deploy automático desde GitHub
- HTTPS/SSL automático
- Escalado simple cuando creces

### Pasos

#### 1. Preparar repositorio en GitHub
```bash
git init
git add .
git commit -m "Initial commit: Backend MVP"
git branch -M main
git remote add origin https://github.com/tu-usuario/BusinessSearcher.git
git push -u origin main
```

#### 2. Crear cuenta en Railway
- Ve a https://railway.app
- Sign up con GitHub
- Autoriza Railway en tu GitHub

#### 3. Crear nuevo proyecto en Railway
1. Click "Start a New Project"
2. Selecciona "Deploy from GitHub repo"
3. Busca y selecciona `BusinessSearcher`
4. Haz click "Deploy"

#### 4. Agregar PostgreSQL
1. En el dashboard del proyecto, click "Add Service"
2. Busca "PostgreSQL"
3. Click "Add PostgreSQL"
4. Wait for it to initialize (2-3 minutos)

#### 5. Configurar variables de entorno
En el servicio API:

```
ASPNETCORE_ENVIRONMENT=Production

ConnectionStrings__DefaultConnection=postgresql://user:pass@host:5432/businesssearcher

Jwt__Secret=TU_RANDOM_SECRET_32_CHARS_MIN

Jwt__Issuer=BusinessSearcher.API

Jwt__Audience=BusinessSearcher.Clients

Jwt__ExpirationDays=1

Email__SmtpHost=smtp.gmail.com

Email__SmtpPort=587

Email__SmtpUser=TU_EMAIL@gmail.com

Email__SmtpPassword=TU_APP_PASSWORD

Email__FromEmail=noreply@businesssearcher.com

Email__FromName=BusinessSearcher

Cloudinary__CloudName=YOUR_CLOUD_NAME

Cloudinary__ApiKey=YOUR_API_KEY

Cloudinary__ApiSecret=YOUR_API_SECRET

Stripe__SecretKey=sk_live_YOUR_KEY

Stripe__WebhookSecret=whsec_YOUR_SECRET

Stripe__Prices__Starter=price_YOUR_STARTER

Stripe__Prices__Premium=price_YOUR_PREMIUM

Stripe__Prices__Enterprise=price_YOUR_ENTERPRISE

Frontend__DashboardUrl=https://tudominio.com

Frontend__ResetPasswordUrl=https://tudominio.com/reset-password
```

#### 6. Generar Railway URL
- Ve a la pestaña "Settings" del servicio API
- En "Railway provided variables", encontrarás la URL pública
- Ej: `https://businesssearcher-prod-xxxx.railway.app`

#### 7. Configurar PostgreSQL (una sola vez)
En el terminal de Railway (o localmente):
```bash
# Obtén la connection string de Railway
# Ve a PostgreSQL service → Variables

# Luego ejecuta:
dotnet ef database update --project BusinessSearcher.Infrastructure --context TenantManagementDbContext
```

#### 8. Verificar
- URL: `https://businesssearcher-prod-xxxx.railway.app`
- Health: `/health`
- Swagger: `/swagger`

---

## Opción 2: Render.com

Muy similar a Railway, pasos:
1. https://render.com → Sign up
2. "New Web Service"
3. Conecta GitHub
4. Selecciona repo
5. Build: `dotnet build`
6. Start: `dotnet BusinessSearcher.WebApi.dll`
7. Agrega PostgreSQL desde Render dashboard

---

## Opción 3: Azure App Service + Azure Database for PostgreSQL

### Si usas Azure (más enterprise)
```bash
# Crear grupo de recursos
az group create --name businesssearcher-rg --location eastus

# Crear App Service Plan
az appservice plan create \
  --name businesssearcher-plan \
  --resource-group businesssearcher-rg \
  --sku B1 --is-linux

# Crear Web App
az webapp create \
  --resource-group businesssearcher-rg \
  --plan businesssearcher-plan \
  --name businesssearcher-api \
  --runtime "DOTNETCORE|8.0"

# Crear PostgreSQL
az postgres server create \
  --resource-group businesssearcher-rg \
  --name businesssearcher-db \
  --admin-user dbadmin \
  --admin-password 'SecurePassword123!@' \
  --sku-name B_Gen5_1 \
  --storage-size 51200

# Deploy desde Git
az webapp deployment source config-zip \
  --resource-group businesssearcher-rg \
  --name businesssearcher-api \
  --src ./deploy.zip
```

---

## Configuración Post-Deployment

### 1. Configurar dominio personalizado

**En Railway/Render:**
- Settings → Domains
- Agrega tu dominio: `api.tudominio.com`
- Añade los registros DNS en tu registrador

**DNS en Namecheap/GoDaddy/etc:**
```
Type:   CNAME
Name:   api
Value:  xxx.railway.app  (o lo que te dé Railway)
```

### 2. Configurar webhook de Stripe

En Stripe Dashboard:
1. Settings → Webhooks
2. "Add endpoint"
3. URL: `https://api.tudominio.com/api/v1/payments/webhook/stripe`
4. Eventos: `checkout.session.completed`, `invoice.payment_succeeded`, `invoice.payment_failed`, `customer.subscription.deleted`
5. Copia el "Signing secret" en `Stripe__WebhookSecret`

### 3. Configurar email (Gmail)

1. Habilita 2FA en tu cuenta Gmail
2. Genera "App Password" (en Security)
3. Usa eso en `Email__SmtpPassword`

### 4. Crear cuenta Cloudinary (gratuita)

1. https://cloudinary.com → Sign up (free tier)
2. Dashboard → Cloud Name, API Key, API Secret
3. Copia en las variables de entorno

### 5. Health check y monitoreo

```bash
# Verifica que está corriendo
curl https://api.tudominio.com/health

# Monitorea con StatusPage.io (gratuito)
# O Uptime Robot (uptime robot)
```

---

## Checklist Pre-Producción

- [ ] Variables de entorno están todas configuradas
- [ ] Base de datos PostgreSQL está creada y migrada
- [ ] JWT Secret es aleatorio (mínimo 32 caracteres)
- [ ] HTTPS está habilitado (automático en Railway/Render)
- [ ] Logs están siendo almacenados
- [ ] Health check responde `/health` → 200 OK
- [ ] Swagger NO está accesible en producción (opcional deshabilitar)
  ```csharp
  if (!app.Environment.IsProduction()) {
      app.UseSwagger();
      app.UseSwaggerUI(...);
  }
  ```
- [ ] CORS está limitado a dominios reales (NO * en producción)
  ```json
  "Cors": {
    "AllowedOrigins": ["https://tudominio.com"]
  }
  ```
- [ ] Rate limiting está activo
- [ ] Backups automáticos están configurados (Railway/Render lo hacen)
- [ ] Error tracking está configurado (Sentry, etc.)

---

## Monitoreo Continuo

### Sentry (error tracking)
```bash
# Install
dotnet add package Sentry.Serilog

# En Program.cs
builder.Host.UseSerilog()
    .ConfigureSerilog();
```

### DataDog (APM + Logs)
```bash
# Install
dotnet add package Datadog.Trace
```

### Uptime Robot (ping)
- https://uptimerobot.com
- Monitorea `https://api.tudominio.com/health`
- Alerta por email si cae

---

## Rollback (si algo falla)

**En Railway:**
1. Dashboard → Deployments
2. Click en el deployment anterior (verde)
3. "Redeploy"
4. Espera 2 minutos

**En Git (volver a commit anterior):**
```bash
git revert HEAD
git push
```

---

## Escalado (cuando creces)

### Fase 1: Cache (búsquedas frecuentes)
Añade Redis:
```bash
# En Railway: Add Service → Redis
```

### Fase 2: Load Balancing
Si tienes mucho tráfico:
```bash
# Escala a 2-3 instancias
# Railway: Settings → Scale to multiple instances
```

### Fase 3: CDN
Cloudinary ya proporciona CDN para imágenes.

### Fase 4: Database replicas
PostgreSQL Premium en Railway/Render.

---

## Solución de problemas en producción

### "502 Bad Gateway"
```bash
# Ver logs en Railway dashboard
# Causa común: variable de entorno faltante
# Solución: agrega la variable y redeploy
```

### "Connection refused" a BD
```bash
# Asegúrate que PostegreSQL está corriendo
# Verifica la ConnectionString
# Prueba desde terminal: psql [connection-string]
```

### "JWT validation failed"
```bash
# El secret en appsettings NO coincide
# Solución: regenera un secret aleatorio y actualiza en todas partes
```

### "CORS error" desde frontend
```bash
# Agrega tu dominio frontend a "Cors.AllowedOrigins"
# Redeploy la API
```

---

## Costo estimado mensual (Railway)

| Componente | Costo |
|-----------|-------|
| API (512MB RAM) | $5-7 |
| PostgreSQL (1GB) | $7 |
| Bandwidth | $0-2 (incluidos 10GB) |
| **Total** | **~$15/mes** |

*(Gratis si usas < 5GB créditos mensuales de Railway)*

---

## Siguientes pasos

1. ✅ Desplegar backend (este documento)
2. 📱 Crear frontend React (Vite + TailwindCSS)
3. 🔗 Conectar frontend a backend
4. 📊 Configurar monitoreo y logs
5. 🎯 Lanzar MVP a usuarios beta

---

**Tiempo estimado para despliegue:** 30 minutos (Railway)
