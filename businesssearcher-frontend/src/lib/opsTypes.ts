// Tipos del módulo Operaciones (TPV/ERP) — espejo de los DTOs del backend /api/v1/ops/*

export interface OpsWarehouse {
  id: string;
  name: string;
  location?: string;
  description?: string;
}

export interface OpsSupplier {
  id: string;
  name: string;
  contact?: string;
  phone: string;
  email?: string;
  address?: string;
  category?: string;
}

export interface OpsCategory {
  id: string;
  name: string;
  description?: string;
  code?: string;
}

export interface OpsProductStock {
  warehouseId: string;
  quantity: number;
  averageCost?: number;
  averageCostUSD?: number;
  /** Precio propio de este almacén (punto de venta). Ausente = usa el precio general del producto. */
  sellPrice?: number;
  sellPriceUSD?: number;
}

export interface OpsProduct {
  id: string;
  barcode?: string;
  name: string;
  description?: string;
  unit: string;
  categoryId?: string;
  costPrice: number;
  sellPrice: number;
  costPriceUSD?: number;
  sellPriceUSD?: number;
  minStock: number;
  taxRate?: number;
  batchNumber?: string;
  expirationDate?: string;
  forSale: boolean;
  totalStock: number;
  stocks: OpsProductStock[];
  imageUrl?: string;
  isPubliclyVisible: boolean;
  minOrderQuantity: number;
}

export interface CreateOpsProduct {
  name: string;
  costPrice: number;
  sellPrice: number;
  barcode?: string;
  description?: string;
  unit?: string;
  categoryId?: string;
  costPriceUSD?: number;
  sellPriceUSD?: number;
  minStock?: number;
  taxRate?: number;
  forSale?: boolean;
  initialWarehouseId?: string;
  initialStock?: number;
  priceChangeReason?: string;
  imageUrl?: string;
  minOrderQuantity?: number;
  isPubliclyVisible?: boolean;
}

/** Producto con precio distinto en el archivo y en el sistema (precios en USD). */
export interface ImportPriceConflict {
  productId: string;
  barcode: string;
  name: string;
  systemPriceUsd: number;
  excelPriceUsd: number;
}

/** Decisión del usuario para cada conflicto de precio: "keep" mantiene el del sistema; "accept" usa el del archivo solo en este almacén. */
export type ImportPriceDecision = 'keep' | 'accept';

/**
 * Resultado de POST /api/v1/ops/products/import-excel.
 * needsDecision = true: hay conflictos de precio sin resolver; no se escribió nada.
 * Un código de barras = un producto; el stock de un producto existente se suma.
 */
export interface ImportProductsResult {
  needsDecision: boolean;
  priceConflicts: ImportPriceConflict[];
  createdCount: number;
  addedCount: number;
  reactivatedCount: number;
  pricesUpdatedCount: number;
  categoriesCreatedCount: number;
  products: {
    barcode: string;
    productId: string;
    status: 'Creado' | 'Sumado' | 'Reactivado' | 'Sin stock';
    previousStock: number;
    newStock: number;
    priceDecision: 'accepted' | 'kept' | null;
  }[];
  warnings: string[];
}

export type OpsCurrency = 'CUP' | 'USD';
export type OpsPaymentMethod = 'Cash' | 'Card' | 'Transfer' | 'Mixed';
export type OpsDiscountType = 'Amount' | 'Percentage';

export interface OpsSaleItem {
  productId: string;
  productName: string;
  warehouseId: string;
  quantity: number;
  unitPrice: number;
  discountType: string;
  discountValue: number;
  lineDiscount: number;
  lineTotal: number;
}

export interface OpsSalePayment {
  method: string;
  amount: number;
  currency: string;
  amountUSD?: number;
  cashTendered?: number;
  change?: number;
  changeCurrency?: string;
}

export interface OpsSale {
  id: string;
  date: string;
  subtotal: number;
  discount: number;
  total: number;
  totalUSD?: number;
  paymentMethod: string;
  paymentCurrency: string;
  status: string;
  cashierId?: string;
  cashierName?: string;
  registerId: string;
  warehouseName?: string;
  managerCode?: string;
  items: OpsSaleItem[];
  payments: OpsSalePayment[];
}

export interface CreateOpsSaleItem {
  productId: string;
  warehouseId: string;
  quantity: number;
  unitPriceOverride?: number;
  discountType?: OpsDiscountType;
  discountValue?: number;
}

export interface CreateOpsSalePayment {
  method: OpsPaymentMethod;
  amount: number;
  currency?: OpsCurrency;
  amountUSD?: number;
  cashTendered?: number;
  change?: number;
  changeCurrency?: OpsCurrency;
}

export interface CreateOpsSale {
  registerId: string;
  paymentMethod: OpsPaymentMethod;
  paymentCurrency: OpsCurrency;
  items: CreateOpsSaleItem[];
  payments: CreateOpsSalePayment[];
  exchangeRate?: number;
  taxAmount?: number;
  managerCode?: string;
}

export interface OpsManager {
  id: string;
  code: string;
  name: string;
  idNumber: string;
  municipality: string;
  province: string;
  phone: string;
  isActive: boolean;
}
export type SaveOpsManager = Omit<OpsManager, 'id' | 'isActive'> & { isActive?: boolean };

export interface UpdateOpsSale {
  items: CreateOpsSaleItem[];
}

export interface SalesImportPriceDiff {
  rowNumber: number;
  productName: string;
  barcode?: string;
  quantity: number;
  systemPrice: number;
  excelPrice: number;
}

export interface ImportSalesResult {
  success: boolean;
  needsPriceConfirmation: boolean;
  importedCount: number;
  pricesUpdatedCount: number;
  priceDifferences: SalesImportPriceDiff[];
  errors: string[];
}

export interface OpsCashRegister {
  id: string;
  warehouseId?: string;
  openDate: string;
  closeDate?: string;
  initialAmount: number;
  initialAmountUSD?: number;
  expectedAmount?: number;
  expectedAmountUSD?: number;
  actualAmount?: number;
  actualAmountUSD?: number;
  difference?: number;
  differenceUSD?: number;
  status: 'Open' | 'Closed';
  salesCount: number;
  totalSales: number;
  totalExpenses: number;
  totalCashIn: number;
  totalCashOut: number;
  inventoryCountCompleted: boolean;
}

export interface OpsCashMovement {
  id: string;
  registerId: string;
  date: string;
  type: string;
  amount: number;
  currency: string;
  description?: string;
}

export interface OpsExchangeRate {
  id: string;
  rate: number;
  date: string;
}

// ── Gastos ──
export type ExpenseType = 'Rent' | 'Salary' | 'Utilities' | 'Marketing' | 'Other' | 'CashOut';

export interface OpsExpense {
  id: string;
  type: string;
  amount: number;
  amountUSD?: number;
  description: string;
  date: string;
}

export interface CreateOpsExpense {
  type: ExpenseType;
  amount: number;
  description: string;
  amountUSD?: number;
}

export type OpsRole = 'Administrador' | 'Cajero' | 'JefeDeTurno' | 'Almacenero' | 'Comercial' | 'Auditor' | 'Observador';

// ── Roles y salarios ──
export interface OpsRoleSalaryConfig {
  role: OpsRole;
  baseSalary: number;
  salesPercentage: number;
  isConfigured: boolean;
}

export interface SaveOpsRoleSalaryConfig {
  baseSalary: number;
  salesPercentage: number;
}

// ── Nómina (gasto de salario) ──
export interface OpsPayrollWorkerLine {
  workerId: string;
  workerName: string;
  role: OpsRole;
  baseSalary: number;
  minimoExento: number;
  salesPercentage: number;
  ventas: number;
  comision: number;
  salarioACobrar: number;
}

export interface OpsPayrollPreview {
  from: string;
  to: string;
  minimoExento: number;
  workers: OpsPayrollWorkerLine[];
  total: number;
}

export interface RegisterOpsPayrollExpense {
  from: string;
  to: string;
  workerIds?: string[];
  description?: string;
  amountUSD?: number;
}

export interface OpsUser {
  id: string;
  name: string;
  email: string;
  role: OpsRole;
  avatarUrl?: string;
  assignedRegisterId?: string;
  assignedWarehouseId?: string;
  isActive: boolean;
}

export interface CreateOpsUser {
  name: string;
  email: string;
  password: string;
  role: OpsRole;
  assignedRegisterId?: string;
  assignedWarehouseId?: string;
}

export interface UpdateOpsUser {
  name: string;
  role: OpsRole;
  assignedRegisterId?: string;
  assignedWarehouseId?: string;
  isActive: boolean;
  newPassword?: string;
}

export interface OpsLoginResult {
  token: string;
  user: OpsUser;
  businessName: string;
}

// ── Compras e inventario avanzado ──
export type PurchaseRequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Completed';

export interface OpsPurchaseRequest {
  id: string;
  productId: string;
  productName: string;
  warehouseId: string;
  currentStock: number;
  minStock: number;
  requestedQuantity: number;
  status: PurchaseRequestStatus;
  notes?: string;
  date: string;
}

export interface OpsPurchaseItem {
  productId: string;
  productName: string;
  quantity: number;
  costPrice: number;
  lineTotal: number;
  batchNumber?: string;
  expirationDate?: string;
}

export interface OpsPurchase {
  id: string;
  supplierId?: string;
  warehouseId: string;
  total: number;
  totalUSD?: number;
  associatedExpenses: number;
  currency: string;
  status: string;
  invoiceUrl?: string;
  date: string;
  items: OpsPurchaseItem[];
  purchaseRequestIds: string[];
  cancellationReason?: string;
  cancelledByUserId?: string;
}

export interface NewPurchaseProduct {
  name: string;
  unit?: string;
  categoryId?: string;
  barcode?: string;
  sellPriceUSD?: number;
  minStock?: number;
  taxRate?: number;
}

export interface CreateOpsPurchaseItem {
  // Si se omite, requiere newProduct (alta de producto al vuelo durante la compra).
  productId?: string;
  quantity: number;
  costPrice: number;
  newSellPrice?: number;
  newProduct?: NewPurchaseProduct;
}

export interface CreateOpsPurchase {
  warehouseId: string;
  items: CreateOpsPurchaseItem[];
  currency?: OpsCurrency;
  supplierId?: string;
  associatedExpenses?: number;
  exchangeRate?: number;
  purchaseRequestIds?: string[];
}

export interface UpdateOpsPurchaseItem {
  productId: string;
  quantity: number;
  costPrice: number;
  batchNumber?: string;
  expirationDate?: string;
  newSellPrice?: number;
}

export interface UpdateOpsPurchase {
  items: UpdateOpsPurchaseItem[];
  associatedExpenses?: number;
  supplierId?: string;
  invoiceUrl?: string;
}

export type MovementType = 'Entrada' | 'Salida' | 'Traslado' | 'Merma';

export interface OpsInventoryMovement {
  id: string;
  productId: string;
  productName: string;
  type: string;
  quantity: number;
  fromWarehouseId?: string;
  toWarehouseId?: string;
  reason?: string;
  date: string;
}

export interface CreateOpsMovement {
  productId: string;
  type: MovementType;
  quantity: number;
  fromWarehouseId?: string;
  toWarehouseId?: string;
  reason?: string;
}

export interface ConvertInventoryItem {
  productId: string;
  quantity: number;
}

export interface ConvertInventoryRequest {
  warehouseId: string;
  sourceItems: ConvertInventoryItem[];
  destinationQuantity: number;
  destinationProductId?: string;
  newDestinationProduct?: NewPurchaseProduct;
  reason?: string;
}

export interface OpsCountItem {
  productId: string;
  productName: string;
  systemQuantity: number;
  countedQuantity: number;
  difference: number;
  audited: boolean;
}

export interface OpsInventoryCount {
  id: string;
  warehouseId: string;
  status: 'Open' | 'Closed';
  adjusted: boolean;
  date: string;
  items: OpsCountItem[];
}

export interface CreateOpsCount {
  warehouseId: string;
  items: { productId: string; countedQuantity: number }[];
}

// ── Datos del negocio ──
export interface OpsBusinessInfo {
  id: string;
  name: string;
  address?: string;
  phone?: string;
  email?: string;
  taxId?: string;
  logoUrl?: string;
}
export interface SaveOpsBusinessInfo {
  name: string;
  address?: string;
  phone?: string;
  email?: string;
  taxId?: string;
  logoUrl?: string;
}

// ── Auditoría ──
export interface OpsAuditLog {
  id: string;
  userId?: string;
  userName?: string;
  userRole?: string;
  action: string;
  targetEntity?: string;
  details?: string;
  timestamp: string;
  ipAddress?: string;
  status: string;
  method?: string;
  path?: string;
}

// ── Reportes ──
export interface SalesReportRow {
  date: string;
  registerId: string;
  paymentMethod: string;
  currency: string;
  total: number;
  totalUsd?: number;
  status: string;
}
export interface SalesReport {
  count: number;
  totalSales: number;
  totalSalesUsd: number;
  totalRefunded: number;
  totalRefundedUsd: number;
  rows: SalesReportRow[];
}

export interface InventoryReportRow {
  productName: string;
  barcode?: string;
  totalStock: number;
  minStock: number;
  sellPrice: number;
  lowStock: boolean;
}
export interface InventoryReport {
  productCount: number;
  lowStockCount: number;
  inventoryValue: number;
  rows: InventoryReportRow[];
}

export interface ExpensesReportRow {
  date: string;
  type: string;
  amount: number;
  description: string;
}
export interface ExpensesReport {
  count: number;
  total: number;
  rows: ExpensesReportRow[];
}

export interface MonthlyDashboardRow {
  month: number;
  monthLabel: string;
  salesTotal: number;
  salesTotalUsd: number;
  refundsTotal: number;
  refundsTotalUsd: number;
  salesCount: number;
  costOfGoodsSold: number;
  costOfGoodsSoldUsd: number;
  purchasesTotal: number;
  purchasesTotalUsd: number;
  purchasesCount: number;
  mermaCount: number;
  mermaValue: number;
  mermaValueUsd: number;
  expensesTotal: number;
  expensesTotalUsd: number;
  profit: number;
  profitUsd: number;
}
export interface MonthlyDashboard {
  year: number;
  months: MonthlyDashboardRow[];
  yearSalesTotal: number;
  yearSalesTotalUsd: number;
  yearCostOfGoodsSold: number;
  yearCostOfGoodsSoldUsd: number;
  yearPurchasesTotal: number;
  yearPurchasesTotalUsd: number;
  yearExpensesTotal: number;
  yearExpensesTotalUsd: number;
  yearMermaValue: number;
  yearMermaValueUsd: number;
  yearProfit: number;
  yearProfitUsd: number;
  previousYearProfit?: number;
  previousYearProfitUsd?: number;
  profitChangePercent?: number;
  exchangeRate?: number;
}

export interface DashboardSummary {
  from: string;
  to: string;
  salesTotal: number;
  salesTotalUsd: number;
  salesCount: number;
  refundsTotal: number;
  refundsTotalUsd: number;
  costOfGoodsSold: number;
  costOfGoodsSoldUsd: number;
  purchasesTotal: number;
  purchasesTotalUsd: number;
  expensesTotal: number;
  expensesTotalUsd: number;
  mermaValue: number;
  mermaValueUsd: number;
  profit: number;
  profitUsd: number;
  exchangeRate?: number;
}

// ── Configuración clave-valor ──
export interface OpsSetting {
  key: string;
  value: string;
}

// ── Feed de notificaciones operativas ──
export interface OpsNotification {
  type: 'LowStock' | 'PriceChange';
  title: string;
  message: string;
  date: string;
  productId?: string;
}

// ── Sincronización Local ↔ Online (solo modo Local; el push lo puede disparar
// cualquier trabajador, el pull es exclusivo del Administrador) ──
export interface SyncPushResult {
  itemsSent: number;
  itemsAccepted: number;
  conflicts: string[];
}
export interface SyncPullResult {
  itemsReceived: number;
  itemsAccepted: number;
  conflicts: string[];
}
export interface SyncHistoryEntry {
  id: string;
  direction: string;
  status: string;
  startedAt: string;
  completedAt?: string;
  itemsSent: number;
  itemsAccepted: number;
  errorMessage?: string;
  counterpartyUrl?: string;
}
// Se genera desde el backend Online (ruta manual/avanzada; el flujo normal es "Conectar" abajo,
// que no requiere copiar ni pegar nada).
export interface SyncApiKeyResult {
  apiKey: string;
}
// Emparejamiento automático: el dueño entrega la URL online + su email/contraseña normales (no
// una API key) y el backend Local hace el resto (login + generar key + guardarla).
export interface SyncConnectionStatus {
  isConnected: boolean;
  onlineBaseUrl?: string;
  businessName?: string;
  // true cuando el registro dejó una contraseña cifrada pendiente de emparejamiento automático
  // (OnlinePairingRetryBackgroundService) y todavía no se obtuvo la API key real.
  autoRetryPending?: boolean;
}
