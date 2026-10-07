# RF del TPV adaptados a BusinessSearcher

Adaptación de los Requisitos Funcionales del **Sistema de Punto de Venta (TPV/ERP)** de
`H:\sistema\spv` (React/TS + Node) al backend **BusinessSearcher** (.NET 8, DDD).

## Estrategia de arquitectura
Los módulos del TPV son **por-comercio** (cada Tenant tiene sus proveedores, almacenes,
inventario, ventas, caja…). Para evitar la DDL frágil por-schema del `StoreManagement`, se
implementan en un **bounded context nuevo `Operations`** aislado, en el schema `public`,
con **migraciones EF propias** (`__ef_migrations_operations`) — mismo patrón seguro que el
`Radar`. Cada entidad lleva `TenantId` y se filtra por el tenant autenticado
(`ICurrentUserService.TenantId`).

## Roles (RBAC) — adaptación
TPV: Administrador, Cajero, Jefe de Turno, Almacenero, Comercial, Auditor.
BusinessSearcher hoy: `AccountRole` = Client, Store, Admin. Se añade un **rol operativo por
usuario dentro del tenant** (`OperationsRole`) para no romper el JWT actual: el Tenant
(dueño) es Administrador; se crean sub-usuarios del tenant con rol operativo. Claim `opsRole`.

| Rol TPV | Permisos clave |
|---|---|
| Administrador | todo (config, usuarios, precios, aprobar solicitudes) |
| Cajero | POS, apertura/cierre de caja, conteo |
| Jefe de Turno | ventas, inventario, caja |
| Almacenero | productos, almacenes, movimientos, solicitudes |
| Comercial | compras, proveedores, gastos |
| Auditor | solo lectura de reportes/auditoría |

## Módulos y RF adaptados

### 1. Usuarios y seguridad (RBAC)
- Sub-usuarios del tenant con rol operativo, avatar, caja/almacén asignados.
- Login ya existe (Tenant). Se añade gestión de sub-usuarios + claim `opsRole`.
- Entidades nuevas: `OperationsUser`.

### 2. Ventas / POS
- Carrito, descuentos (monto/%), multi-pago (efectivo/tarjeta/transferencia/mixto),
  moneda CUP/USD con tasa del día, cambio (vuelto), descuento de stock, comprobante.
- **Reutiliza** parcialmente `StoreManagement.ProductTransaction` (venta), pero el POS
  completo va en `Operations`: `Sale`, `SalePayment`, `CartItem`.
- Endpoints: `POST /api/v1/ops/sales`, `GET /api/v1/ops/sales` (filtros), refund.

### 3. Inventario multi-almacén
- `Warehouse`, stock por almacén (`WarehouseStock`), bajo stock resaltado.
- Movimientos: `InventoryMovement` (Entrada/Salida/Traslado/Merma) con autor y fecha.
- Conteo de inventario + reporte de discrepancias (`InventoryCount`, `Discrepancy`).

### 4. Compras y reabastecimiento
- `PurchaseRequest` (auto al caer bajo mínimo; solo Admin modifica cantidades; estados
  PENDING/APPROVED/REJECTED/COMPLETED).
- `Purchase` + `PurchaseItem` (aumenta stock, asocia solicitudes, gastos asociados, factura).
- `Supplier` (contacto, teléfono, email, dirección, geo, productos que surte).

### 5. Finanzas
- `Expense` (RENT/SALARY/UTILITIES/MARKETING/OTHER/CASH_OUT), CUP/USD.
- Divisas: `ExchangeRateLog` (tasa diaria; recalcula todo). `BusinessInfo` (datos fiscales).

### 6. Caja (arqueo)
- `CashRegister` (apertura con monto inicial, cierre con esperado vs real, diferencia,
  CUP/USD, ligada a terminal/almacén, totales de ventas/gastos/entradas/salidas).
- `CashMovement` (IN/OUT/EXPENSE/REFUND).
- `Terminal` (nombre, almacén, estado).

### 7. Reportes y auditoría
- `AuditLog` (usuario, rol, acción, entidad, IP, método, path, status) — vía middleware.
- Reportes con exportación (Excel ya hay `ClosedXML`; PDF pendiente).

### 8. Offline (app)
- Cola local + sync — es del lado app (React/MAUI), no del backend. Se documenta como
  requisito de cliente; el backend expone endpoints idempotentes para sync.

## Estado de implementación (se actualiza por tanda)
- [x] Operations bounded context + `OperationsDbContext` + migración base
- [x] Suppliers · Warehouses · Expenses · ExchangeRate (CRUD) — falta BusinessInfo
- [x] **Núcleo POS** (esta tanda):
  - [x] Catálogo: `Category`, `Product` (doble moneda CUP/USD, código de barras),
        `ProductStock` (multi-almacén), `ProductPriceHistory` (historial al cambiar precio)
  - [x] Ventas/POS: `Sale` + `SaleItem` + `SalePayment` (descuentos monto/%, multi-pago,
        descuento de stock atómico) · refund (restaura stock + movimiento de caja)
  - [x] Caja: `CashRegister` (apertura/cierre con esperado vs real), `CashMovement`
        (IN/OUT/EXPENSE/REFUND), `Terminal`
  - [x] Migración EF `AddPosCore_Operations` (10 tablas `op_*`) + DI + build verde
  - [x] **Frontend** (React/MUI, `businesssearcher-frontend`): pantallas POS, Inventario
        (productos/stock/categorías/almacenes) y Caja/arqueo + hooks `useOps` + rutas + menú
- [x] **Sub-usuarios + roles (RBAC)** (esta tanda):
  - [x] `OperationsUser` (dominio + repo + config + migración `AddOperationsUsers` → `op_users`)
  - [x] JWT del sub-usuario: `sub`=id usuario, claim `tenantId`=negocio, claim `opsRole`.
        `ICurrentUserService.OpsRole` (dueño del negocio = Administrador implícito). Retrocompatible.
  - [x] Login de empleado `POST /api/v1/ops/auth/login` (anónimo) + gestión `/api/v1/ops/users`
        (solo Administrador).
  - [x] Atributo `[OpsRoles(...)]` aplicado a los controllers `/ops/*` (403 si el rol no permite).
        Cierra el hueco: antes cualquier negocio autenticado escribía en todo.
  - [x] Frontend: página **Usuarios (TPV)** (alta/edición de empleados con rol) + toggle
        **Dueño / Empleado (TPV)** en el login (sesión de empleado persistida, sin `/auth/profile`).
- [x] **Compras + Inventario avanzado** (esta tanda):
  - [x] `PurchaseRequest` (auto por bajo stock vía `generate-low-stock`; solo Admin ajusta
        cantidades/aprueba; PENDING→APPROVED/REJECTED→COMPLETED al recibir la compra)
  - [x] `Purchase` + `PurchaseItem` (aumenta stock, actualiza costo/precio, marca solicitudes
        finalizadas; `purchase_request_ids` en jsonb)
  - [x] `InventoryMovement` (Entrada/Salida/Traslado/Merma; ajusta stock por tipo)
  - [x] `InventoryCount` + `InventoryCountItem` (snapshot sistema vs contado → discrepancias;
        cierre con ajuste opcional del stock)
  - [x] Migración `AddPurchasingInventory` (6 tablas `op_*`) + `[OpsRoles]` + build verde
  - [x] Frontend: Solicitudes, Compras, Movimientos, Conteo + hooks + rutas + menú
- [x] **Frontend de módulos base** (esta tanda): pantallas **Proveedores**, **Gastos** y
      **Configuración** (tasa de cambio + historial), con sus hooks y entradas de menú.
- [x] **AuditLog, Reportes/export y BusinessInfo** (esta tanda):
  - [x] `BusinessInfo` (datos fiscales, uno por negocio) — `GET/PUT /api/v1/ops/business-info`
        (lectura abierta, escritura solo Administrador). Integrado en Configuración.
  - [x] `AuditLog` + `AuditLogMiddleware` (registra mutaciones POST/PUT/DELETE/PATCH bajo
        `/ops/*` y todo 401/403, con usuario/rol/IP/método/ruta/resultado) — corre después de
        Auth para leer `ICurrentUserService.OpsRole` ya poblado. `GET /api/v1/ops/audit-log`
        (solo rol Auditor o Administrador). Página **Auditoría** en el frontend.
  - [x] Reportes de **Ventas/Inventario/Gastos** (`/api/v1/ops/reports/{sales,inventory,expenses}`)
        + export a **Excel** (ClosedXML, `IOperationsReportExporter`) vía `/export`
        (Auditor/Jefe de Turno/Administrador). Página **Reportes** con 3 pestañas y descarga.
  - [x] Migración `AddBusinessInfoAndAuditLog` (`op_business_info`, `op_audit_logs`).
  - [x] **Verificado en vivo**: backend arrancado contra Postgres real (BD limpia), migraciones
        de las 4 tandas aplicadas sin error, seed admin OK. Smoke test end-to-end por HTTP:
        login → BusinessInfo → almacén/categoría/producto con stock → reporte de inventario
        (valor calculado correcto) → alta de sub-usuario Cajero → login de empleado (JWT con
        `opsRole`) → intento bloqueado por rol (403) → auditoría mostrando el DENIED y todos
        los SUCCESS con atribución correcta de usuario/rol.

### Matriz de permisos aplicada (`[OpsRoles]`; Administrador siempre pasa)
| Operación | Roles permitidos |
|---|---|
| Productos/Categorías (escritura) | Almacenero, Jefe de Turno |
| Ajuste de stock | Almacenero, Jefe de Turno, Cajero |
| Ventas (crear) | Cajero, Jefe de Turno · refund → Jefe de Turno |
| Caja (abrir/cerrar/movimientos) | Cajero, Jefe de Turno |
| Almacenes | Almacenero, Jefe de Turno |
| Proveedores | Comercial · Gastos → Comercial, Jefe de Turno |
| Compras | Comercial, Jefe de Turno |
| Solicitudes: crear/generar | Almacenero, Jefe de Turno |
| Solicitudes: ajustar cantidad/aprobar/rechazar | solo Administrador |
| Movimientos de inventario | Almacenero, Jefe de Turno |
| Conteo: crear | Cajero, Almacenero, Jefe de Turno · cerrar → Almacenero, Jefe de Turno |
| Terminales · Tasa de cambio · Usuarios · BusinessInfo (escritura) | solo Administrador |
| Auditoría (lectura) | Auditor (Administrador siempre pasa) |
| Reportes (lectura + export) | Auditor, Jefe de Turno |
| Lecturas (GET) generales | cualquier sub-usuario autenticado |

### Endpoints (`/api/v1/ops/*`)
POS: `categories` · `products` (+`/barcode/{c}`, `/{id}/adjust-stock`, `/{id}/price-history`) ·
`sales` (+`/{id}/refund`) · `terminals` · `cash-registers` (`/current`, `/open`, `/{id}/close`,
`/{id}/movements`, `/movements`).
Compras/Inventario: `purchase-requests` (+`/generate-low-stock`, `/{id}/quantity`, `/{id}/approve`,
`/{id}/reject`) · `purchases` · `inventory/movements` · `inventory/counts` (+`/{id}/close`).
Base: `suppliers` · `warehouses` · `expenses` · `exchange-rate` (+`/history`) · `business-info`.
Auth/usuarios: `auth/login` (empleado, anónimo) · `users` (gestión, solo Admin).
Auditoría/Reportes: `audit-log` · `reports/{sales,inventory,expenses}` (+`/export` → .xlsx).
