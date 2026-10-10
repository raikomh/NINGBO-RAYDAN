import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/apiClient';
import type {
  OpsWarehouse, OpsCategory, OpsProduct, CreateOpsProduct,
  OpsSale, CreateOpsSale, UpdateOpsSale, OpsCashRegister, OpsCashMovement, OpsExchangeRate,
  OpsUser, CreateOpsUser, UpdateOpsUser, OpsManager, SaveOpsManager,
  OpsPurchaseRequest, OpsPurchase, CreateOpsPurchase, UpdateOpsPurchase, OpsInventoryMovement, CreateOpsMovement,
  ConvertInventoryRequest,
  OpsInventoryCount, CreateOpsCount, OpsSupplier,
  OpsExpense, CreateOpsExpense, OpsRole,
  OpsRoleSalaryConfig, SaveOpsRoleSalaryConfig, OpsPayrollPreview, RegisterOpsPayrollExpense,
  OpsBusinessInfo, SaveOpsBusinessInfo, OpsAuditLog, SalesReport, InventoryReport, ExpensesReport,
  OpsSetting, OpsNotification, MonthlyDashboard, ImportProductsResult, ImportPriceDecision, DashboardSummary,
  SyncPushResult, SyncPullResult, SyncHistoryEntry, SyncApiKeyResult, SyncConnectionStatus,
} from '@/lib/opsTypes';

const unwrap = (res: { data: unknown }) => {
  const d = res.data as { data?: unknown };
  return (d?.data ?? res.data ?? []) as never;
};

const safeGet = async <T>(url: string, params?: Record<string, unknown>): Promise<T> => {
  try {
    const res = await api.get(url, { params });
    return unwrap(res);
  } catch (err: unknown) {
    if ((err as { response?: { status: number } }).response?.status === 404) return [] as unknown as T;
    throw err;
  }
};

// ── Almacenes ──
export function useWarehouses() {
  return useQuery<OpsWarehouse[]>({ queryKey: ['ops', 'warehouses'], queryFn: () => safeGet('/api/v1/ops/warehouses') });
}
export function useSaveWarehouse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...dto }: Partial<OpsWarehouse> & { name: string }) =>
      id ? api.put(`/api/v1/ops/warehouses/${id}`, dto) : api.post('/api/v1/ops/warehouses', dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'warehouses'] }),
  });
}

// ── Proveedores ──
export function useSuppliers(search?: string) {
  return useQuery<OpsSupplier[]>({
    queryKey: ['ops', 'suppliers', search],
    queryFn: () => safeGet('/api/v1/ops/suppliers', search ? { search } : undefined),
  });
}
export function useSaveSupplier() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...dto }: Partial<OpsSupplier> & { name: string; phone: string }) =>
      id ? api.put(`/api/v1/ops/suppliers/${id}`, dto) : api.post('/api/v1/ops/suppliers', dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'suppliers'] }),
  });
}
export function useDeleteSupplier() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/api/v1/ops/suppliers/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'suppliers'] }),
  });
}

// ── Gastos ──
export function useExpenses(params?: { from?: string; to?: string }) {
  return useQuery<OpsExpense[]>({
    queryKey: ['ops', 'expenses', params],
    queryFn: () => safeGet('/api/v1/ops/expenses', params as Record<string, unknown>),
  });
}
export function useCreateExpense() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateOpsExpense) => api.post('/api/v1/ops/expenses', dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ops', 'expenses'] });
      qc.invalidateQueries({ queryKey: ['ops', 'cash'] });
    },
  });
}

// ── Roles y salarios (solo Administrador puede modificar/eliminar) ──
export function useRoleSalaryConfigs() {
  return useQuery<OpsRoleSalaryConfig[]>({
    queryKey: ['ops', 'role-salary-configs'],
    queryFn: () => safeGet('/api/v1/ops/role-salary-configs'),
  });
}
export function useSaveRoleSalaryConfig() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ role, dto }: { role: OpsRole; dto: SaveOpsRoleSalaryConfig }) =>
      api.put(`/api/v1/ops/role-salary-configs/${role}`, dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'role-salary-configs'] }),
  });
}
export function useDeleteRoleSalaryConfig() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (role: OpsRole) => api.delete(`/api/v1/ops/role-salary-configs/${role}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'role-salary-configs'] }),
  });
}

// ── Nómina (gasto de salario calculado a partir de los roles) ──
export function usePayrollPreview(from?: string, to?: string) {
  return useQuery<OpsPayrollPreview>({
    queryKey: ['ops', 'payroll', 'preview', from, to],
    queryFn: () => safeGet('/api/v1/ops/payroll/preview', { from, to }),
    enabled: !!from && !!to,
  });
}
export function useRegisterPayrollExpense() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: RegisterOpsPayrollExpense) => api.post('/api/v1/ops/payroll/register', dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ops', 'expenses'] });
      qc.invalidateQueries({ queryKey: ['ops', 'payroll'] });
      qc.invalidateQueries({ queryKey: ['ops', 'cash'] });
    },
  });
}

// ── Categorías ──
export function useOpsCategories() {
  return useQuery<OpsCategory[]>({ queryKey: ['ops', 'categories'], queryFn: () => safeGet('/api/v1/ops/categories') });
}
export function useSaveOpsCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...dto }: Partial<OpsCategory> & { name: string }) =>
      id ? api.put(`/api/v1/ops/categories/${id}`, dto) : api.post('/api/v1/ops/categories', dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'categories'] }),
  });
}

// ── Productos ──
export function useOpsProducts(params?: { search?: string; categoryId?: string; warehouseId?: string; lowStockOnly?: boolean }) {
  return useQuery<OpsProduct[]>({
    queryKey: ['ops', 'products', params],
    queryFn: () => safeGet('/api/v1/ops/products', params as Record<string, unknown>),
  });
}
/** Búsqueda puntual por código de barras exacto (lectora de pistola en el POS). */
export async function fetchProductByBarcode(barcode: string): Promise<OpsProduct | null> {
  const res = await api.get(`/api/v1/ops/products/barcode/${encodeURIComponent(barcode)}`);
  return (res.data?.data ?? null) as OpsProduct | null;
}
export function useSaveOpsProduct() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id?: string; dto: CreateOpsProduct }) =>
      id ? api.put(`/api/v1/ops/products/${id}`, dto) : api.post('/api/v1/ops/products', dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'products'] }),
  });
}
export function useDeleteOpsProduct() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/api/v1/ops/products/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'products'] }),
  });
}
export function useAdjustStock() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, warehouseId, delta }: { id: string; warehouseId: string; delta: number }) =>
      api.post(`/api/v1/ops/products/${id}/adjust-stock`, { warehouseId, delta }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'products'] }),
  });
}
export function useSetProductPublicVisibility() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isPubliclyVisible }: { id: string; isPubliclyVisible: boolean }) =>
      api.patch(`/api/v1/ops/products/${id}/public-visibility`, { isPubliclyVisible }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'products'] }),
  });
}
/** Fija Costo/Venta de un producto SOLO para un almacén (no toca el precio general que usan
 * las demás tiendas que también tengan ese mismo producto en stock). */
export function useSetWarehousePrice() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, warehouseId, costPrice, costPriceUSD, sellPrice, sellPriceUSD }: {
      id: string; warehouseId: string; costPrice: number; costPriceUSD?: number; sellPrice: number; sellPriceUSD?: number;
    }) => api.patch(`/api/v1/ops/products/${id}/warehouse-price`, { costPrice, costPriceUSD, sellPrice, sellPriceUSD }, { params: { warehouseId } }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'products'] }),
  });
}
export function useUploadOpsProductImage() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, file }: { id: string; file: File }) => {
      const form = new FormData();
      form.append('file', file);
      return api.post(`/api/v1/ops/products/${id}/image`, form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'products'] }),
  });
}
/** Importa productos desde el Excel de la plaza (Código, Producto, Categoría, Cant. disponible, Precio x unidad (USD)). */
export function useImportOpsProducts() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ file, warehouseId, exchangeRate, catalogDate, priceDecisions }: {
      file: File;
      warehouseId: string;
      exchangeRate?: number;
      /** Fecha del catálogo (yyyy-MM-dd). Identifica el catálogo: no se sube dos veces al mismo almacén. */
      catalogDate: string;
      /** Decisiones por código de barras, solo para los productos con conflicto de precio. */
      priceDecisions?: Record<string, ImportPriceDecision>;
    }) => {
      const form = new FormData();
      form.append('file', file);
      form.append('warehouseId', warehouseId);
      form.append('catalogDate', catalogDate);
      if (exchangeRate !== undefined) form.append('exchangeRate', String(exchangeRate));
      if (priceDecisions && Object.keys(priceDecisions).length > 0) form.append('priceDecisions', JSON.stringify(priceDecisions));
      const res = await api.post('/api/v1/ops/products/import-excel', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      return (res.data?.data ?? res.data) as ImportProductsResult;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ops', 'products'] });
      qc.invalidateQueries({ queryKey: ['ops', 'categories'] });
    },
  });
}

// ── Ventas ──
export function useOpsSales(params?: { from?: string; to?: string; registerId?: string; cashierId?: string; warehouseId?: string }) {
  return useQuery<OpsSale[]>({
    queryKey: ['ops', 'sales', params],
    queryFn: () => safeGet('/api/v1/ops/sales', params as Record<string, unknown>),
  });
}
export function useCreateSale() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: CreateOpsSale) => {
      const res = await api.post('/api/v1/ops/sales', dto);
      return unwrap(res) as OpsSale;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ops', 'sales'] });
      qc.invalidateQueries({ queryKey: ['ops', 'products'] });
      qc.invalidateQueries({ queryKey: ['ops', 'cash', 'current'] });
    },
  });
}
export function useRefundSale() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post(`/api/v1/ops/sales/${id}/refund`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ops', 'sales'] });
      qc.invalidateQueries({ queryKey: ['ops', 'products'] });
    },
  });
}
export function useUpdateSale() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: string; dto: UpdateOpsSale }) => api.put(`/api/v1/ops/sales/${id}`, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ops', 'sales'] });
      qc.invalidateQueries({ queryKey: ['ops', 'products'] });
    },
  });
}
/**
 * Importa ventas desde Excel (Código, Producto, Cantidad, Precio). Cuando el Excel trae precios
 * distintos a los del sistema y `acceptNewPrices` viene undefined, el backend no registra nada:
 * devuelve `needsPriceConfirmation` + `priceDifferences` para que el usuario decida y se reenvíe
 * la misma importación con `acceptNewPrices` en true/false.
 */
export function useImportSales() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ file, acceptNewPrices }: { file: File; acceptNewPrices?: boolean }) => {
      const form = new FormData();
      form.append('file', file);
      if (acceptNewPrices !== undefined) form.append('acceptNewPrices', String(acceptNewPrices));
      const res = await api.post('/api/v1/ops/sales/import', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      return (res.data?.data ?? res.data) as import('@/lib/opsTypes').ImportSalesResult;
    },
    onSuccess: (result) => {
      if (result.importedCount > 0) {
        qc.invalidateQueries({ queryKey: ['ops', 'sales'] });
        qc.invalidateQueries({ queryKey: ['ops', 'products'] });
        qc.invalidateQueries({ queryKey: ['ops', 'cash', 'current'] });
      }
    },
  });
}

// ── Caja ──
/** La caja "actual" es la de la tienda activa: cada almacén tiene su propia caja, así que el
 * warehouseId es obligatorio para no mezclar la caja de una tienda con la de otra. */
export function useCurrentCashRegister(warehouseId?: string | null) {
  return useQuery<OpsCashRegister | null>({
    queryKey: ['ops', 'cash', 'current', warehouseId],
    queryFn: async () => {
      const res = await api.get('/api/v1/ops/cash-registers/current', { params: { warehouseId } });
      return (res.data?.data ?? null) as OpsCashRegister | null;
    },
    enabled: !!warehouseId,
  });
}
export function useCashRegisters(params?: { from?: string; to?: string }) {
  return useQuery<OpsCashRegister[]>({
    queryKey: ['ops', 'cash', 'list', params],
    queryFn: () => safeGet('/api/v1/ops/cash-registers', params as Record<string, unknown>),
  });
}
export function useCashMovements(registerId?: string) {
  return useQuery<OpsCashMovement[]>({
    queryKey: ['ops', 'cash', 'movements', registerId],
    queryFn: () => safeGet(`/api/v1/ops/cash-registers/${registerId}/movements`),
    enabled: !!registerId,
  });
}
export function useOpenRegister() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: { initialAmount: number; initialAmountUSD?: number; warehouseId?: string }) =>
      api.post('/api/v1/ops/cash-registers/open', dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'cash'] }),
  });
}
export function useCloseRegister() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, actualAmount, actualAmountUSD }: { id: string; actualAmount: number; actualAmountUSD?: number }) =>
      api.post(`/api/v1/ops/cash-registers/${id}/close`, { actualAmount, actualAmountUSD }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'cash'] }),
  });
}
export function useAddCashMovement() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: { registerId: string; type: string; amount: number; currency?: string; description?: string }) =>
      api.post('/api/v1/ops/cash-registers/movements', dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'cash'] }),
  });
}


// ── Sub-usuarios (solo Administrador) ──
export function useOpsUsers() {
  return useQuery<OpsUser[]>({ queryKey: ['ops', 'users'], queryFn: () => safeGet('/api/v1/ops/users') });
}
export function useSaveOpsUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id?: string; dto: CreateOpsUser | UpdateOpsUser }) =>
      id ? api.put(`/api/v1/ops/users/${id}`, dto) : api.post('/api/v1/ops/users', dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'users'] }),
  });
}

// ── Solicitudes de compra ──
// Gestores (consulta: todos; alta/edición: Administrador y Observador)
export function useOpsManagers() {
  return useQuery<OpsManager[]>({ queryKey: ['ops', 'managers'], queryFn: () => safeGet('/api/v1/ops/managers') });
}
export function useSaveOpsManager() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id?: string; dto: SaveOpsManager }) =>
      id ? api.put(`/api/v1/ops/managers/${id}`, dto) : api.post('/api/v1/ops/managers', dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'managers'] }),
  });
}

export function usePurchaseRequests(status?: string) {
  return useQuery<OpsPurchaseRequest[]>({
    queryKey: ['ops', 'purchase-requests', status],
    queryFn: () => safeGet('/api/v1/ops/purchase-requests', status ? { status } : undefined),
  });
}
export function useGenerateLowStockRequests() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.post('/api/v1/ops/purchase-requests/generate-low-stock'),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'purchase-requests'] }),
  });
}
export function usePurchaseRequestAction() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, action, requestedQuantity }: { id: string; action: 'approve' | 'reject' | 'quantity'; requestedQuantity?: number }) =>
      action === 'quantity'
        ? api.put(`/api/v1/ops/purchase-requests/${id}/quantity`, { requestedQuantity })
        : api.post(`/api/v1/ops/purchase-requests/${id}/${action}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'purchase-requests'] }),
  });
}

// ── Compras ──
export function usePurchases(params?: { from?: string; to?: string; supplierId?: string }) {
  return useQuery<OpsPurchase[]>({
    queryKey: ['ops', 'purchases', params],
    queryFn: () => safeGet('/api/v1/ops/purchases', params as Record<string, unknown>),
  });
}
export function useCreatePurchase() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateOpsPurchase) => api.post('/api/v1/ops/purchases', dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ops', 'purchases'] });
      qc.invalidateQueries({ queryKey: ['ops', 'products'] });
      qc.invalidateQueries({ queryKey: ['ops', 'purchase-requests'] });
    },
  });
}
export function useUpdatePurchase() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: string; dto: UpdateOpsPurchase }) => api.put(`/api/v1/ops/purchases/${id}`, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ops', 'purchases'] });
      qc.invalidateQueries({ queryKey: ['ops', 'products'] });
    },
  });
}
export function useCancelPurchase() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => api.post(`/api/v1/ops/purchases/${id}/cancel`, { reason }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ops', 'purchases'] });
      qc.invalidateQueries({ queryKey: ['ops', 'products'] });
    },
  });
}

// ── Movimientos de inventario ──
export function useInventoryMovements(params?: { from?: string; to?: string; productId?: string; warehouseId?: string }) {
  return useQuery<OpsInventoryMovement[]>({
    queryKey: ['ops', 'movements', params],
    queryFn: () => safeGet('/api/v1/ops/inventory/movements', params as Record<string, unknown>),
  });
}
export function useCreateMovement() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateOpsMovement) => api.post('/api/v1/ops/inventory/movements', dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ops', 'movements'] });
      qc.invalidateQueries({ queryKey: ['ops', 'products'] });
    },
  });
}
export function useConvertInventory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: ConvertInventoryRequest) => api.post('/api/v1/ops/inventory/movements/convert', dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ops', 'movements'] });
      qc.invalidateQueries({ queryKey: ['ops', 'products'] });
    },
  });
}

// ── Conteo de inventario ──
export function useInventoryCounts(warehouseId?: string) {
  return useQuery<OpsInventoryCount[]>({
    queryKey: ['ops', 'counts', warehouseId],
    queryFn: () => safeGet('/api/v1/ops/inventory/counts', warehouseId ? { warehouseId } : undefined),
  });
}
export function useCreateCount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: CreateOpsCount) => {
      const res = await api.post('/api/v1/ops/inventory/counts', dto);
      return unwrap(res) as OpsInventoryCount;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'counts'] }),
  });
}
export function useCloseCount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, applyAdjustments }: { id: string; applyAdjustments: boolean }) =>
      api.post(`/api/v1/ops/inventory/counts/${id}/close`, { applyAdjustments }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ops', 'counts'] });
      qc.invalidateQueries({ queryKey: ['ops', 'products'] });
    },
  });
}
export function useSetCountItemAudited() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, productId, audited }: { id: string; productId: string; audited: boolean }) =>
      api.patch(`/api/v1/ops/inventory/counts/${id}/items/${productId}/audit`, { audited }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'counts'] }),
  });
}

// ── Tasa de cambio ──
export function useExchangeRate() {
  return useQuery<OpsExchangeRate | null>({
    queryKey: ['ops', 'exchange-rate'],
    queryFn: async () => {
      const res = await api.get('/api/v1/ops/exchange-rate');
      return (res.data?.data ?? null) as OpsExchangeRate | null;
    },
  });
}
export function useExchangeRateHistory(limit = 30) {
  return useQuery<OpsExchangeRate[]>({
    queryKey: ['ops', 'exchange-rate', 'history', limit],
    queryFn: () => safeGet('/api/v1/ops/exchange-rate/history', { limit }),
  });
}
export function useSetExchangeRate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (rate: number) => api.post('/api/v1/ops/exchange-rate', { rate }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'exchange-rate'] }),
  });
}

// ── Datos del negocio ──
export function useBusinessInfo() {
  return useQuery<OpsBusinessInfo | null>({
    queryKey: ['ops', 'business-info'],
    queryFn: async () => {
      const res = await api.get('/api/v1/ops/business-info');
      return (res.data?.data ?? null) as OpsBusinessInfo | null;
    },
  });
}
export function useSaveBusinessInfo() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: SaveOpsBusinessInfo) => api.put('/api/v1/ops/business-info', dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'business-info'] }),
  });
}

// ── Configuración clave-valor genérica ──
export function useSettings() {
  return useQuery<OpsSetting[]>({ queryKey: ['ops', 'settings'], queryFn: () => safeGet('/api/v1/ops/settings') });
}
export function useSaveSetting() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ key, value }: { key: string; value: string }) => api.put(`/api/v1/ops/settings/${encodeURIComponent(key)}`, { value }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'settings'] }),
  });
}

// ── Feed de notificaciones operativas (stock bajo + cambios de precio) ──
export function useOperationalNotifications() {
  return useQuery<OpsNotification[]>({
    queryKey: ['ops', 'notifications'],
    queryFn: () => safeGet('/api/v1/ops/notifications'),
    refetchInterval: 60_000,
  });
}

export function useNotificationsUnreadCount() {
  return useQuery<number>({
    queryKey: ['ops', 'notifications', 'unread-count'],
    queryFn: async () => {
      try {
        const res = await api.get('/api/v1/ops/notifications/unread-count');
        const raw = (res.data as { data?: unknown })?.data ?? res.data;
        const n = Number(raw);
        return Number.isFinite(n) ? n : 0;
      } catch {
        return 0; // nunca [] — un valor no numérico deja el Badge de MUI visible para siempre
      }
    },
    refetchInterval: 60_000,
  });
}

export function useMarkNotificationsSeen() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.post('/api/v1/ops/notifications/mark-seen'),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'notifications', 'unread-count'] }),
  });
}

// ── Auditoría (solo Auditor/Administrador) ──
export function useAuditLogs(params?: { from?: string; to?: string; userId?: string; action?: string; limit?: number }) {
  return useQuery<OpsAuditLog[]>({
    queryKey: ['ops', 'audit-log', params],
    queryFn: () => safeGet('/api/v1/ops/audit-log', params as Record<string, unknown>),
  });
}

// ── Reportes (Auditor/Jefe de Turno/Administrador) ──
const downloadBlob = async (url: string, params: Record<string, unknown> | undefined, filename: string) => {
  const res = await api.get(url, { params, responseType: 'blob' });
  const blobUrl = window.URL.createObjectURL(new Blob([res.data]));
  const a = document.createElement('a');
  a.href = blobUrl; a.download = filename;
  document.body.appendChild(a); a.click(); a.remove();
  window.URL.revokeObjectURL(blobUrl);
};
const downloadBlobPost = async (url: string, body: unknown, filename: string) => {
  const res = await api.post(url, body, { responseType: 'blob' });
  const blobUrl = window.URL.createObjectURL(new Blob([res.data]));
  const a = document.createElement('a');
  a.href = blobUrl; a.download = filename;
  document.body.appendChild(a); a.click(); a.remove();
  window.URL.revokeObjectURL(blobUrl);
};

// ── Orden de entrega (una o varias ventas seleccionadas, descarga .xlsx) ──
export function useGenerateOrdenEntrega() {
  return useMutation({
    mutationFn: (dto: { saleIds: string[]; noOrden?: string; cliente?: string; ci?: string; telefono?: string; direccion?: string; domicilio?: number }) =>
      downloadBlobPost('/api/v1/ops/sales/orden-entrega', dto, 'orden-de-entrega.xlsx'),
  });
}

export function useMonthlyDashboard(year?: number, warehouseId?: string | null) {
  return useQuery<MonthlyDashboard>({
    queryKey: ['ops', 'reports', 'dashboard', year, warehouseId],
    queryFn: async () => {
      const res = await api.get('/api/v1/ops/reports/dashboard', { params: { year, warehouseId: warehouseId || undefined } });
      return (res.data?.data ?? res.data) as MonthlyDashboard;
    },
  });
}
export function useDashboardSummary(params: { from?: string; to?: string }) {
  return useQuery<DashboardSummary>({
    queryKey: ['ops', 'reports', 'dashboard', 'summary', params],
    queryFn: async () => {
      const res = await api.get('/api/v1/ops/reports/dashboard/summary', { params });
      return (res.data?.data ?? res.data) as DashboardSummary;
    },
  });
}
export function useSalesReport(params?: { from?: string; to?: string; warehouseId?: string | null }) {
  return useQuery<SalesReport>({
    queryKey: ['ops', 'reports', 'sales', params],
    queryFn: async () => {
      const res = await api.get('/api/v1/ops/reports/sales', { params: { ...params, warehouseId: params?.warehouseId || undefined } });
      return (res.data?.data ?? res.data) as SalesReport;
    },
  });
}
export function useExportSalesReport() {
  return useMutation({ mutationFn: (params?: { from?: string; to?: string; warehouseId?: string | null }) => downloadBlob('/api/v1/ops/reports/sales/export', { ...params, warehouseId: params?.warehouseId || undefined }, 'reporte-ventas.xlsx') });
}

export function useInventoryReport(lowStockOnly?: boolean, warehouseId?: string | null) {
  return useQuery<InventoryReport>({
    queryKey: ['ops', 'reports', 'inventory', lowStockOnly, warehouseId],
    queryFn: async () => {
      const res = await api.get('/api/v1/ops/reports/inventory', { params: { lowStockOnly: lowStockOnly || undefined, warehouseId: warehouseId || undefined } });
      return (res.data?.data ?? res.data) as InventoryReport;
    },
  });
}
export function useExportInventoryReport() {
  return useMutation({
    mutationFn: ({ lowStockOnly, warehouseId }: { lowStockOnly?: boolean; warehouseId?: string | null } = {}) =>
      downloadBlob('/api/v1/ops/reports/inventory/export', { lowStockOnly: lowStockOnly || undefined, warehouseId: warehouseId || undefined }, 'reporte-inventario.xlsx'),
  });
}

export function useExpensesReport(params?: { from?: string; to?: string }) {
  return useQuery<ExpensesReport>({
    queryKey: ['ops', 'reports', 'expenses', params],
    queryFn: async () => {
      const res = await api.get('/api/v1/ops/reports/expenses', { params });
      return (res.data?.data ?? res.data) as ExpensesReport;
    },
  });
}
export function useExportExpensesReport() {
  return useMutation({ mutationFn: (params?: { from?: string; to?: string }) => downloadBlob('/api/v1/ops/reports/expenses/export', params, 'reporte-gastos.xlsx') });
}

// ── Sincronización Local ↔ Online ──
export function useSyncHistory(take = 20) {
  return useQuery<SyncHistoryEntry[]>({
    queryKey: ['ops', 'sync', 'history', take],
    queryFn: () => safeGet('/api/v1/sync/history', { take }),
  });
}
export function useSyncPush() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const res = await api.post('/api/v1/sync/push');
      return (res.data?.data ?? res.data) as SyncPushResult;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'sync', 'history'] }),
  });
}
export function useSyncPull() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const res = await api.post('/api/v1/sync/pull');
      return (res.data?.data ?? res.data) as SyncPullResult;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'sync', 'history'] }),
  });
}
// Solo backend Online: genera/revoca la API key que el dueño del negocio copia a
// Sync__ApiKey en su instalación Local.
export function useGenerateSyncApiKey() {
  return useMutation({
    mutationFn: async () => {
      const res = await api.post('/api/v1/sync/api-key');
      return (res.data?.data ?? res.data) as SyncApiKeyResult;
    },
  });
}
export function useRevokeSyncApiKey() {
  return useMutation({ mutationFn: () => api.delete('/api/v1/sync/api-key') });
}
// Emparejamiento automático (Local): el dueño entrega URL + email/contraseña de su cuenta
// online; el backend hace login y genera la key por su cuenta, sin que nadie la copie.
export function useSyncConnectionStatus() {
  return useQuery<SyncConnectionStatus>({
    queryKey: ['ops', 'sync', 'connection'],
    queryFn: () => safeGet('/api/v1/sync/connection'),
  });
}
export function useConnectSync() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: { email: string; password: string }) => {
      const res = await api.post('/api/v1/sync/connect', dto);
      return (res.data?.data ?? res.data) as SyncConnectionStatus;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'sync', 'connection'] }),
  });
}
export function useDisconnectSync() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.delete('/api/v1/sync/connect'),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'sync', 'connection'] }),
  });
}
// Emparejamiento manual (Local): el dueño genera la clave en su cuenta online y la pega aquí,
// en vez de escribir su contraseña. El backend la valida contra el online antes de guardarla.
export function useConnectSyncWithApiKey() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: { apiKey: string }) => {
      const res = await api.post('/api/v1/sync/connect/api-key', dto);
      return (res.data?.data ?? res.data) as SyncConnectionStatus;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ops', 'sync', 'connection'] }),
  });
}
