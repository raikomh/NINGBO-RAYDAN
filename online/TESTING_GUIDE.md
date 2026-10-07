# 🧪 Guía de Testing y Compilación — BusinessSearcher MVP

---

## Paso 1 — Compilar la solución

```bash
cd BusinessSearcher.sln

# En Visual Studio: Ctrl+Shift+B (Compilar solución)
# O desde línea de comandos:
dotnet clean
dotnet build --configuration Release
```

**Esperado:** Sin errores. Si hay errores, son problemas de compilación que deben corregirse antes de continuar.

---

## Paso 2 — Ejecutar tests unitarios

```bash
# Ejecutar todos los tests
dotnet test BusinessSearcher.Tests --verbosity normal

# O con más detalles
dotnet test BusinessSearcher.Tests --verbosity detailed

# Tests de un proyecto específico
dotnet test BusinessSearcher.Tests --filter "FullyQualifiedName~TenantTests"
dotnet test BusinessSearcher.Tests --filter "FullyQualifiedName~MoneyTests"
```

**Tests incluidos:**

### Domain Tests (22 total)
- **TenantTests** (7 tests)
  - ✓ Create with valid data
  - ✓ Create with invalid businessName
  - ✓ Activate, Deactivate, RegisterPayment
  - ✓ IssueRefreshToken, RotateRefreshToken
  - ✓ RequestPasswordReset, ResetPassword

- **StoreTests** (8 tests)
  - ✓ Create, AddCategory, AddProduct
  - ✓ ToggleProductAvailability
  - ✓ AddOrUpdateSchedule
  - ✓ Deactivate

- **ValueObjectTests** (22 tests)
  - **MoneyTests** (9) — Operaciones aritméticas, validaciones, igualdad
  - **AddressTests** (5) — Validación de campos, coordenadas
  - **PhoneNumberTests** (3) — Formatos válidos/inválidos
  - **ScheduleTimeTests** (5) — Creación, IsOpenAt, duración

### Application Tests (6 total)
- **RegisterTenantCommandHandlerTests** (5)
  - ✓ Register with valid data
  - ✓ Weak password validation
  - ✓ Invalid email validation
  - ✓ Invalid plan validation

- **LoginTenantCommandHandlerTests** (5)
  - ✓ Login with valid credentials
  - ✓ Nonexistent email → error
  - ✓ Wrong password → error
  - ✓ Suspended account → error

---

## Paso 3 — Setup local con Docker (más rápido para probar)

```bash
# Levanta API, PostgreSQL y MailHog
docker compose up -d

# Verifica que todo está corriendo
docker ps

# API: http://localhost:5000
# Swagger: http://localhost:5000/swagger
# MailHog (emails): http://localhost:8025
# Health check: curl http://localhost:5000/health
```

**Si algo falla:**
```bash
# Ver logs
docker compose logs api
docker compose logs postgres

# Limpiar todo
docker compose down -v
```

---

## Paso 4 — Pruebas manuales (API endpoints)

### 1. Registrar un negocio
```bash
curl -X POST http://localhost:5000/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "businessName": "Mi Restaurante",
    "email": "dueno@restaurante.com",
    "password": "SecurePass123!@",
    "plan": "Starter"
  }'
```

**Respuesta esperada:**
```json
{
  "success": true,
  "message": "Registro exitoso.",
  "data": {
    "token": "eyJ...",
    "tenant": {
      "id": "uuid",
      "businessName": "Mi Restaurante",
      "email": "dueno@restaurante.com",
      "plan": "Starter",
      "status": "Trial",
      ...
    }
  }
}
```

### 2. Login
```bash
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "dueno@restaurante.com",
    "password": "SecurePass123!@"
  }' \
  -c cookies.txt  # Guarda cookies (refresh token)
```

### 3. Obtener perfil (requiere JWT)
```bash
JWT_TOKEN="eyJ..."  # Del response anterior

curl http://localhost:5000/api/v1/auth/profile \
  -H "Authorization: Bearer $JWT_TOKEN"
```

### 4. Crear tienda
```bash
curl -X POST http://localhost:5000/api/v1/stores \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Empanadas Doña Rosa",
    "description": "Las mejores empanadas de Bogotá",
    "address": {
      "street": "Calle 10 #5-20",
      "city": "Bogotá",
      "state": "Cundinamarca",
      "country": "Colombia",
      "latitude": 4.7110,
      "longitude": -74.0721
    },
    "phone": "+57 300 1234567",
    "logoUrl": null
  }'
```

### 5. Crear categoría
```bash
STORE_ID="uuid"  # Del response anterior

curl -X POST http://localhost:5000/api/v1/stores/$STORE_ID/categories \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Empanadas Saladas",
    "description": "Todo tipo de empanadas saladas",
    "sortOrder": 1
  }'
```

### 6. Crear producto
```bash
CATEGORY_ID="uuid"

curl -X POST http://localhost:5000/api/v1/stores/$STORE_ID/products \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Empanada de pollo",
    "description": "Con pollo deshebrado y salsa",
    "price": 2500,
    "currency": "COP",
    "categoryId": "'$CATEGORY_ID'",
    "stock": 50
  }'
```

### 7. Cambiar disponibilidad (dispara SignalR)
```bash
PRODUCT_ID="uuid"

curl -X PATCH http://localhost:5000/api/v1/stores/$STORE_ID/products/$PRODUCT_ID/toggle \
  -H "Authorization: Bearer $JWT_TOKEN"
```

### 8. Búsqueda pública (sin auth)
```bash
curl "http://localhost:5000/api/v1/search?q=empanada&city=Bogotá&available=true&page=1&pageSize=10"
```

### 9. Health check
```bash
curl http://localhost:5000/health
```

---

## Paso 5 — Verificación de cobertura de tests

```bash
# Genera reporte de cobertura (requiere: dotnet tool install -g dotnet-reportgenerator-globaltool)
dotnet test /p:CollectCoverage=true /p:CoverageFormat=cobertura

# Abre el reporte en browser
reportgenerator -reports:"coverage.cobertura.xml" -targetdir:"coverage-report"
open coverage-report/index.html  # macOS
# o explorer coverage-report\index.html  # Windows
```

---

## Checklist pre-deployment

- [ ] Solución compila sin errores (`dotnet build --configuration Release`)
- [ ] Todos los tests pasan (`dotnet test`)
- [ ] `docker compose up -d` levanta sin errores
- [ ] Endpoint `/health` responde `200 OK`
- [ ] Registro de negocio funciona → recibe JWT
- [ ] Login funciona → refresh token guardado en cookie
- [ ] Crear tienda funciona
- [ ] Búsqueda pública funciona
- [ ] Cambio de disponibilidad notifica vía SignalR (conexión WebSocket)
- [ ] Logs se escriben en `logs/` (desarrollo) o vía Serilog
- [ ] Emails se ven en MailHog (`http://localhost:8025`)

---

## Problemas comunes y soluciones

### Error: "Metadata file could not be found"
```bash
# Solución: limpiar y reconstruir
dotnet clean
dotnet build
```

### Error: "Connection refused" (PostgreSQL)
```bash
# Si usas Docker Compose:
docker compose restart postgres

# Si usas PostgreSQL local:
# Verifica que esté corriendo en localhost:5432
psql -U postgres -d business_searcher_dev
```

### Error: "JWT token invalid"
```bash
# Causa: el secret en appsettings.json no coincide con el usado para generar el token
# Solución: asegúrate que usas el mismo secret en ambos lugares
```

### Tests fallan con "DbContext is null"
```bash
# Causa: los tests usan Moq, no una BD real
# Esto es correcto para tests unitarios. Los tests de integración
# con BD real requieren setup de prueba adicional (migraciones, seeds).
```

---

## Testing en CI/CD (GitHub Actions - recomendado)

Crea `.github/workflows/test.yml`:

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_PASSWORD: postgres123
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 5432:5432

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - run: dotnet build
      - run: dotnet test --verbosity normal
```

Esto ejecuta tests automáticamente en cada push.

---

## Próximos pasos después de tests verdes

1. ✅ Tests pasan → Backend listo para MVP
2. Crear frontend React (Vite + TailwindCSS)
3. Integración frontend ↔ backend
4. Despliegue a Railway o Render
5. Configurar dominio personalizado
6. Monitoring y logging en producción
