import type { AxiosAdapter, InternalAxiosRequestConfig, AxiosResponse } from 'axios';

// ---------------------------------------------------------------------------
// Mock data
// ---------------------------------------------------------------------------

const MOCK_TOKEN = 'mock-jwt-token-for-testing';
const MOCK_TOKEN_2 = 'mock-jwt-token-tenant-user';

const MOCK_USER = {
  id: '007339b8-d6a2-4de1-a804-d7c99478cf82',
  businessName: 'Admin Test',
  email: 'admin@businesssearcher.dev',
  plan: 'Enterprise',
  role: 'admin',
  status: 'Active',
  isSubscriptionActive: true,
  createdAt: '2026-06-24T00:14:57.119541Z',
  nextPaymentDate: '2026-07-24T00:14:57.125936Z',
  totalPaid: 0,
  tenantType: 'Retail',
};

const MOCK_USER_2 = {
  id: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
  businessName: 'Tienda El Rincón',
  email: 'tienda@businesssearcher.dev',
  plan: 'Basic',
  role: 'tenant',
  status: 'Active',
  isSubscriptionActive: true,
  createdAt: '2026-06-01T10:00:00.000000Z',
  nextPaymentDate: '2026-07-01T10:00:00.000000Z',
  totalPaid: 29.99,
  tenantType: 'Retail',
};

const MOCK_WHOLESALE_CATALOG = [
  {
    storeId: 'store-003', storeName: 'Distribuidora Mayorista Sur', storeAddress: 'Polígono Industrial 4', city: 'Madrid', storePhone: '+34 600 000 099',
    productId: 'wprod-001', productName: 'Caja de Coca-Cola 24u', productDescription: 'Caja cerrada de 24 unidades', price: 18.5, currency: 'EUR', stock: 500, minOrderQuantity: 10, categoryName: 'Bebidas',
  },
  {
    storeId: 'store-003', storeName: 'Distribuidora Mayorista Sur', storeAddress: 'Polígono Industrial 4', city: 'Madrid', storePhone: '+34 600 000 099',
    productId: 'wprod-002', productName: 'Saco de Papas 25kg', productDescription: 'Papa fresca a granel', price: 12.0, currency: 'EUR', stock: 200, minOrderQuantity: 5, categoryName: 'Alimentos',
  },
];

const MOCK_STORES = [
  {
    id: 'store-001',
    name: 'Tienda Centro',
    description: 'Nuestra tienda principal en el centro de la ciudad.',
    address: { street: 'Calle Mayor 10', city: 'Madrid', state: 'Madrid', country: 'España', latitude: 40.4168, longitude: -3.7038 },
    phone: '+34 600 000 001',
    logoUrl: '',
    isOpen: true,
    schedules: [
      { id: 's1', dayOfWeek: 'Monday', openTime: '09:00', closeTime: '20:00', isClosed: false },
      { id: 's2', dayOfWeek: 'Tuesday', openTime: '09:00', closeTime: '20:00', isClosed: false },
      { id: 's3', dayOfWeek: 'Sunday', openTime: '', closeTime: '', isClosed: true },
    ],
  },
  {
    id: 'store-002',
    name: 'Tienda Norte',
    description: 'Sucursal norte con amplio estacionamiento.',
    address: { street: 'Av. Norte 55', city: 'Madrid', state: 'Madrid', country: 'España', latitude: 40.4500, longitude: -3.6900 },
    phone: '+34 600 000 002',
    logoUrl: '',
    isOpen: false,
    schedules: [
      { id: 's4', dayOfWeek: 'Monday', openTime: '10:00', closeTime: '19:00', isClosed: false },
    ],
  },
];

type MockRecord = { id: string; [key: string]: unknown };

const MOCK_TRANSACTIONS: Record<string, MockRecord[]> = {
  'store-001': [
    { id: 'tx-001', type: 'Sale', productId: 'prod-001', productName: 'Coca-Cola 500ml', quantity: 3, unitPrice: 1.5, totalAmount: 4.5, notes: '', createdAt: new Date(Date.now() - 86400000).toISOString() },
    { id: 'tx-002', type: 'Entry', productId: 'prod-002', productName: 'Agua Mineral 1L', quantity: 100, unitPrice: 0.4, totalAmount: 40, notes: 'Reposición semanal', createdAt: new Date(Date.now() - 172800000).toISOString() },
    { id: 'tx-003', type: 'Spoilage', productId: 'prod-004', productName: 'Brownie de Chocolate', quantity: 2, unitPrice: undefined, totalAmount: undefined, notes: 'Vencidos', createdAt: new Date(Date.now() - 259200000).toISOString() },
  ],
  'store-002': [],
};

const MOCK_CATEGORIES: Record<string, MockRecord[]> = {
  'store-001': [
    { id: 'cat-001', name: 'Bebidas', description: 'Refrescos, jugos y más', sortOrder: 1, isActive: true },
    { id: 'cat-002', name: 'Snacks', description: 'Papas, galletas y aperitivos', sortOrder: 2, isActive: true },
    { id: 'cat-003', name: 'Postres', description: 'Dulces y pasteles', sortOrder: 3, isActive: false },
  ],
  'store-002': [
    { id: 'cat-004', name: 'Electrónica', description: 'Accesorios y gadgets', sortOrder: 1, isActive: true },
  ],
};

const MOCK_PRODUCTS: Record<string, MockRecord[]> = {
  'store-001': [
    { id: 'prod-001', name: 'Coca-Cola 500ml', description: 'Refresco clásico', price: 1.5, currency: 'EUR', categoryId: 'cat-001', categoryName: 'Bebidas', imageUrl: '', stock: 200, isAvailable: true, isFeatured: true },
    { id: 'prod-002', name: 'Agua Mineral 1L', description: 'Agua sin gas', price: 0.8, currency: 'EUR', categoryId: 'cat-001', categoryName: 'Bebidas', imageUrl: '', stock: 350, isAvailable: true, isFeatured: false },
    { id: 'prod-003', name: 'Papas Fritas', description: 'Papas sabor original 150g', price: 1.2, currency: 'EUR', categoryId: 'cat-002', categoryName: 'Snacks', imageUrl: '', stock: 80, isAvailable: true, isFeatured: false },
    { id: 'prod-004', name: 'Brownie de Chocolate', description: 'Casero, 100g', price: 2.5, currency: 'EUR', categoryId: 'cat-003', categoryName: 'Postres', imageUrl: '', stock: 0, isAvailable: false, isFeatured: false },
  ],
  'store-002': [
    { id: 'prod-005', name: 'Cable USB-C', description: '1 metro, carga rápida', price: 9.99, currency: 'EUR', categoryId: 'cat-004', categoryName: 'Electrónica', imageUrl: '', stock: 45, isAvailable: true, isFeatured: true },
  ],
};

// ---------------------------------------------------------------------------
// Helper to build an AxiosResponse
// ---------------------------------------------------------------------------

function ok(config: InternalAxiosRequestConfig, data: unknown, status = 200): AxiosResponse {
  return { data, status, statusText: 'OK', headers: {}, config };
}

function unauthorized(config: InternalAxiosRequestConfig): Promise<never> {
  const err: { response: AxiosResponse; isAxiosError: boolean; message: string } = {
    response: { data: { success: false, message: 'No autorizado' }, status: 401, statusText: 'Unauthorized', headers: {}, config },
    isAxiosError: true,
    message: 'Request failed with status code 401',
  };
  return Promise.reject(err);
}

// ---------------------------------------------------------------------------
// Route matcher
// ---------------------------------------------------------------------------

function match(url: string, pattern: string): Record<string, string> | null {
  const patParts = pattern.split('/');
  const urlParts = url.split('?')[0].split('/');
  if (patParts.length !== urlParts.length) return null;
  const params: Record<string, string> = {};
  for (let i = 0; i < patParts.length; i++) {
    if (patParts[i].startsWith(':')) {
      params[patParts[i].slice(1)] = urlParts[i];
    } else if (patParts[i] !== urlParts[i]) {
      return null;
    }
  }
  return params;
}

// ---------------------------------------------------------------------------
// Mock adapter
// ---------------------------------------------------------------------------

export const mockAdapter: AxiosAdapter = (config) => {
  const url = config.url ?? '';
  const method = (config.method ?? 'get').toLowerCase();

  // Auth
  if (method === 'post' && match(url, '/api/v1/auth/register')) {
    return Promise.resolve(ok(config, { success: true, message: 'Cuenta creada. Verifica tu email.' }, 200));
  }

  if (method === 'post' && match(url, '/api/v1/auth/login')) {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    if (body.email === 'admin@businesssearcher.dev' && body.password === 'Admin@12345') {
      return Promise.resolve(ok(config, { success: true, message: 'Login exitoso.', data: { token: MOCK_TOKEN, tenant: MOCK_USER } }));
    }
    if (body.email === 'tienda@businesssearcher.dev' && body.password === 'Tienda@12345') {
      return Promise.resolve(ok(config, { success: true, message: 'Login exitoso.', data: { token: MOCK_TOKEN_2, tenant: MOCK_USER_2 } }));
    }
    return unauthorized(config);
  }

  if (method === 'get' && match(url, '/api/v1/auth/profile')) {
    const token = (config.headers?.['Authorization'] as string)?.replace('Bearer ', '');
    if (token === MOCK_TOKEN) return Promise.resolve(ok(config, { success: true, data: MOCK_USER }));
    if (token === MOCK_TOKEN_2) return Promise.resolve(ok(config, { success: true, data: MOCK_USER_2 }));
    return unauthorized(config);
  }

  if (method === 'post' && match(url, '/api/v1/auth/logout')) {
    return Promise.resolve(ok(config, { success: true }));
  }

  if (method === 'post' && match(url, '/api/v1/auth/refresh-token')) {
    return unauthorized(config);
  }

  // Single-store endpoints (tenant's own store)
  if (method === 'get' && match(url, '/api/v1/store')) {
    return Promise.resolve(ok(config, { success: true, data: MOCK_STORES[0] }));
  }

  if (method === 'put' && match(url, '/api/v1/store')) {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    Object.assign(MOCK_STORES[0], body);
    return Promise.resolve(ok(config, { success: true, data: MOCK_STORES[0] }));
  }

  if (method === 'patch' && match(url, '/api/v1/store/activate')) {
    (MOCK_STORES[0] as Record<string, unknown>).isActive = true;
    return Promise.resolve(ok(config, { success: true }));
  }

  if (method === 'patch' && match(url, '/api/v1/store/deactivate')) {
    (MOCK_STORES[0] as Record<string, unknown>).isActive = false;
    return Promise.resolve(ok(config, { success: true }));
  }

  // Stores (plural) — kept for sub-resource routes like /stores/{id}/products
  if (method === 'get' && match(url, '/api/v1/stores')) {
    return Promise.resolve(ok(config, MOCK_STORES));
  }

  const storeById = match(url, '/api/v1/stores/:id');
  if (method === 'get' && storeById) {
    const store = MOCK_STORES.find((s) => s.id === storeById.id);
    if (!store) return Promise.resolve(ok(config, null, 404));
    return Promise.resolve(ok(config, store));
  }

  if (method === 'post' && match(url, '/api/v1/stores')) {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    const newStore = { id: `store-${Date.now()}`, isOpen: false, schedules: [], ...body };
    MOCK_STORES.push(newStore);
    return Promise.resolve(ok(config, newStore, 201));
  }

  const storeUpdate = match(url, '/api/v1/stores/:id');
  if (method === 'put' && storeUpdate) {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    const idx = MOCK_STORES.findIndex((s) => s.id === storeUpdate.id);
    if (idx >= 0) Object.assign(MOCK_STORES[idx], body);
    return Promise.resolve(ok(config, MOCK_STORES[idx]));
  }

  // Categories
  const catsRoute = match(url, '/api/v1/stores/:storeId/categories');
  if (method === 'get' && catsRoute) {
    return Promise.resolve(ok(config, MOCK_CATEGORIES[catsRoute.storeId] ?? []));
  }

  if (method === 'post' && catsRoute) {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    const cat = { id: `cat-${Date.now()}`, isActive: true, ...body };
    (MOCK_CATEGORIES[catsRoute.storeId] ??= []).push(cat);
    return Promise.resolve(ok(config, cat, 201));
  }

  const catById = match(url, '/api/v1/stores/:storeId/categories/:id');
  if ((method === 'put' || method === 'delete') && catById) {
    const cats = MOCK_CATEGORIES[catById.storeId] ?? [];
    const idx = cats.findIndex((c: { id: string }) => c.id === catById.id);
    if (method === 'delete') {
      if (idx >= 0) cats.splice(idx, 1);
      return Promise.resolve(ok(config, { success: true }));
    }
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    if (idx >= 0) Object.assign(cats[idx], body);
    return Promise.resolve(ok(config, cats[idx]));
  }

  // Products
  const prodsRoute = match(url, '/api/v1/stores/:storeId/products');
  if (method === 'get' && prodsRoute) {
    return Promise.resolve(ok(config, MOCK_PRODUCTS[prodsRoute.storeId] ?? []));
  }

  if (method === 'post' && prodsRoute) {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    const prod = { id: `prod-${Date.now()}`, isAvailable: true, isFeatured: false, currency: 'EUR', imageUrl: '', ...body };
    (MOCK_PRODUCTS[prodsRoute.storeId] ??= []).push(prod);
    return Promise.resolve(ok(config, prod, 201));
  }

  const prodById = match(url, '/api/v1/stores/:storeId/products/:id');
  if (prodById) {
    const prods = MOCK_PRODUCTS[prodById.storeId] ?? [];
    const idx = prods.findIndex((p: { id: string }) => p.id === prodById.id);
    if (method === 'put') {
      const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
      if (idx >= 0) Object.assign(prods[idx], body);
      return Promise.resolve(ok(config, prods[idx]));
    }
    if (method === 'delete') {
      if (idx >= 0) prods.splice(idx, 1);
      return Promise.resolve(ok(config, { success: true }));
    }
  }

  const toggleRoute = match(url, '/api/v1/stores/:storeId/products/:id/toggle');
  if (method === 'patch' && toggleRoute) {
    const prods = MOCK_PRODUCTS[toggleRoute.storeId] ?? [];
    const prod = prods.find((p: { id: string }) => p.id === toggleRoute.id) as { isAvailable: boolean } | undefined;
    if (prod) prod.isAvailable = !prod.isAvailable;
    return Promise.resolve(ok(config, prod));
  }

  // Uploads (devuelve URL vacía en mock)
  if (method === 'post' && url.includes('/api/v1/uploads/')) {
    return Promise.resolve(ok(config, { url: '' }));
  }

  // Schedules
  const schedRoute = match(url, '/api/v1/stores/:storeId/schedules');
  if (schedRoute) {
    if (method === 'get') {
      const store = MOCK_STORES.find((s) => s.id === schedRoute.storeId);
      return Promise.resolve(ok(config, store?.schedules ?? []));
    }
    if (method === 'post') {
      const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
      const sched = { id: `sched-${Date.now()}`, ...body };
      const store = MOCK_STORES.find((s) => s.id === schedRoute.storeId);
      store?.schedules?.push(sched);
      return Promise.resolve(ok(config, sched, 201));
    }
  }

  // Chat messages: solo se guardan para el equipo de soporte (sin respuesta automática)
  if (method === 'post' && match(url, '/api/v1/chat/messages')) {
    return Promise.resolve(ok(config, { success: true }, 201));
  }

  // Chat messages GET (history)
  if (method === 'get' && match(url, '/api/v1/chat/messages')) {
    return Promise.resolve(ok(config, { success: true, data: [] }));
  }

  // Chat unread count
  if (method === 'get' && match(url, '/api/v1/chat/unread')) {
    return Promise.resolve(ok(config, { success: true, data: { count: 0 } }));
  }

  // Mark chat messages as read
  if (method === 'patch' && match(url, '/api/v1/chat/messages/read')) {
    return Promise.resolve(ok(config, { success: true }));
  }

  // Dashboard stats per store
  const dashRoute = match(url, '/api/v1/dashboard/:storeId');
  if (method === 'get' && dashRoute) {
    const prods = MOCK_PRODUCTS[dashRoute.storeId] ?? [];
    return Promise.resolve(ok(config, {
      success: true,
      data: {
        totalProducts: prods.length,
        availableProducts: prods.filter((p: MockRecord) => p.isAvailable).length,
        totalCategories: (MOCK_CATEGORIES[dashRoute.storeId] ?? []).length,
        totalTransactions: (MOCK_TRANSACTIONS[dashRoute.storeId] ?? []).length,
        totalSales: (MOCK_TRANSACTIONS[dashRoute.storeId] ?? []).filter((t: MockRecord) => t.type === 'Sale').length,
        totalEntries: (MOCK_TRANSACTIONS[dashRoute.storeId] ?? []).filter((t: MockRecord) => t.type === 'Entry').length,
        totalSpoilages: (MOCK_TRANSACTIONS[dashRoute.storeId] ?? []).filter((t: MockRecord) => t.type === 'Spoilage').length,
        lowStockProducts: prods.filter((p: MockRecord) => (p.stock as number) < 10 && p.stock !== undefined).length,
        revenue: (MOCK_TRANSACTIONS[dashRoute.storeId] ?? [])
          .filter((t: MockRecord) => t.type === 'Sale')
          .reduce((acc: number, t: MockRecord) => acc + ((t.totalAmount as number) ?? 0), 0),
      },
    }));
  }

  // Monthly report
  const monthlyRoute = match(url, '/api/v1/dashboard/:storeId/monthly-report');
  if (method === 'get' && monthlyRoute) {
    const now = new Date();
    const months = Array.from({ length: 6 }, (_, i) => {
      const d = new Date(now.getFullYear(), now.getMonth() - (5 - i), 1);
      return {
        month: d.getMonth() + 1,
        year: d.getFullYear(),
        totalSales: Math.floor(Math.random() * 20),
        totalEntries: Math.floor(Math.random() * 10),
        totalSpoilages: Math.floor(Math.random() * 5),
        revenue: parseFloat((Math.random() * 500).toFixed(2)),
      };
    });
    return Promise.resolve(ok(config, { success: true, data: months }));
  }

  // Transactions GET
  const txListRoute = match(url, '/api/v1/stores/:storeId/transactions');
  if (method === 'get' && txListRoute) {
    return Promise.resolve(ok(config, { success: true, data: MOCK_TRANSACTIONS[txListRoute.storeId] ?? [] }));
  }

  // Transaction GET by id
  const txByIdRoute = match(url, '/api/v1/stores/:storeId/transactions/:txId');
  if (method === 'get' && txByIdRoute) {
    const tx = (MOCK_TRANSACTIONS[txByIdRoute.storeId] ?? []).find((t: MockRecord) => t.id === txByIdRoute.txId);
    return Promise.resolve(ok(config, { success: true, data: tx ?? null }));
  }

  // Transactions POST (sale)
  const saleRoute = match(url, '/api/v1/stores/:storeId/transactions/sale');
  if (method === 'post' && saleRoute) {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    const prods = MOCK_PRODUCTS[saleRoute.storeId] ?? [];
    const prod = prods.find((p: MockRecord) => p.id === body.productId) as { price: number; stock: number; name: string; id: string } | undefined;
    if (prod) prod.stock = Math.max(0, prod.stock - body.quantity);
    const tx = {
      id: `tx-${Date.now()}`,
      type: 'Sale',
      productId: body.productId,
      productName: prod?.name,
      quantity: body.quantity,
      unitPrice: prod?.price,
      totalAmount: prod ? parseFloat((prod.price * body.quantity).toFixed(2)) : 0,
      notes: body.notes,
      createdAt: new Date().toISOString(),
    };
    (MOCK_TRANSACTIONS[saleRoute.storeId] ??= []).unshift(tx);
    return Promise.resolve(ok(config, { success: true, data: tx }, 201));
  }

  // Transactions POST (entry)
  const entryRoute = match(url, '/api/v1/stores/:storeId/transactions/entry');
  if (method === 'post' && entryRoute) {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    const prods = MOCK_PRODUCTS[entryRoute.storeId] ?? [];
    const prod = prods.find((p: MockRecord) => p.id === body.productId) as { name: string; stock: number } | undefined;
    if (prod) prod.stock += body.quantity;
    const tx = {
      id: `tx-${Date.now()}`,
      type: 'Entry',
      productId: body.productId,
      productName: prod?.name,
      quantity: body.quantity,
      unitPrice: body.unitPrice,
      totalAmount: parseFloat((body.unitPrice * body.quantity).toFixed(2)),
      notes: body.notes,
      createdAt: new Date().toISOString(),
    };
    (MOCK_TRANSACTIONS[entryRoute.storeId] ??= []).unshift(tx);
    return Promise.resolve(ok(config, { success: true, data: tx }, 201));
  }

  // Transactions POST (spoilage)
  const spoilageRoute = match(url, '/api/v1/stores/:storeId/transactions/spoilage');
  if (method === 'post' && spoilageRoute) {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    const prods = MOCK_PRODUCTS[spoilageRoute.storeId] ?? [];
    const prod = prods.find((p: MockRecord) => p.id === body.productId) as { name: string; stock: number } | undefined;
    if (prod) prod.stock = Math.max(0, prod.stock - body.quantity);
    const tx = {
      id: `tx-${Date.now()}`,
      type: 'Spoilage',
      productId: body.productId,
      productName: prod?.name,
      quantity: body.quantity,
      notes: body.notes,
      createdAt: new Date().toISOString(),
    };
    (MOCK_TRANSACTIONS[spoilageRoute.storeId] ??= []).unshift(tx);
    return Promise.resolve(ok(config, { success: true, data: tx }, 201));
  }

  // Products featured
  const featuredRoute = match(url, '/api/v1/stores/:storeId/products/:id/featured');
  if (method === 'patch' && featuredRoute) {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    const prods = MOCK_PRODUCTS[featuredRoute.storeId] ?? [];
    const prod = prods.find((p: MockRecord) => p.id === featuredRoute.id) as { isFeatured: boolean } | undefined;
    if (prod) prod.isFeatured = body.isFeatured;
    return Promise.resolve(ok(config, { success: true, data: prod }));
  }

  // Store activate / deactivate
  const storeActivate = match(url, '/api/v1/stores/:id/activate');
  if (method === 'patch' && storeActivate) {
    const store = MOCK_STORES.find((s) => s.id === storeActivate.id);
    if (store) (store as Record<string, unknown>).isActive = true;
    return Promise.resolve(ok(config, { success: true }));
  }

  const storeDeactivate = match(url, '/api/v1/stores/:id/deactivate');
  if (method === 'patch' && storeDeactivate) {
    const store = MOCK_STORES.find((s) => s.id === storeDeactivate.id);
    if (store) (store as Record<string, unknown>).isActive = false;
    return Promise.resolve(ok(config, { success: true }));
  }

  // Auth profile PATCH
  if (method === 'patch' && match(url, '/api/v1/auth/profile')) {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    if (body.businessName) MOCK_USER.businessName = body.businessName;
    return Promise.resolve(ok(config, { success: true, data: MOCK_USER }));
  }

  // Auth logout-all
  if (method === 'post' && match(url, '/api/v1/auth/logout-all')) {
    return Promise.resolve(ok(config, { success: true }));
  }

  // Auth resend-verification
  if (method === 'post' && match(url, '/api/v1/auth/resend-verification')) {
    return Promise.resolve(ok(config, { success: true, message: 'Email de verificación enviado.' }));
  }

  // Auth verify-email
  if (method === 'post' && match(url, '/api/v1/auth/verify-email')) {
    return Promise.resolve(ok(config, { success: true }));
  }

  // Auth FCM token
  if (method === 'patch' && match(url, '/api/v1/auth/fcm-token')) {
    return Promise.resolve(ok(config, { success: true }));
  }

  // Search
  if (method === 'get' && match(url, '/api/v1/search')) {
    const params = new URLSearchParams(url.split('?')[1] ?? '');
    const q = (params.get('q') ?? '').toLowerCase();
    const city = (params.get('city') ?? '').toLowerCase();
    const results = MOCK_STORES
      .filter((s) => {
        const matchQ = !q || s.name.toLowerCase().includes(q);
        const matchCity = !city || (s.address?.city ?? '').toLowerCase().includes(city);
        return matchQ || matchCity;
      })
      .map((s) => ({
        storeId: s.id,
        storeName: s.name,
        storeAddress: s.address,
        isOpen: s.isOpen,
        phone: s.phone,
        products: (MOCK_PRODUCTS[s.id] ?? []).filter(
          (p: MockRecord) => !q || (p.name as string).toLowerCase().includes(q)
        ),
      }));
    return Promise.resolve(ok(config, { success: true, data: results }));
  }

  // Wholesale catalog directory (solo tenants Retail)
  if (method === 'get' && match(url, '/api/v1/wholesale-catalog')) {
    const params = new URLSearchParams(url.split('?')[1] ?? '');
    const q = (params.get('q') ?? '').toLowerCase();
    const city = (params.get('city') ?? '').toLowerCase();
    const items = MOCK_WHOLESALE_CATALOG.filter((i) => {
      const matchQ = !q || i.productName.toLowerCase().includes(q) || i.storeName.toLowerCase().includes(q);
      const matchCity = !city || i.city.toLowerCase().includes(city);
      return matchQ && matchCity;
    });
    return Promise.resolve(ok(config, { success: true, data: { items, totalCount: items.length } }));
  }

  // Catalog import (Excel) — solo tenants Wholesale
  const catalogImportRoute = match(url, '/api/v1/stores/:storeId/catalog/import');
  if (method === 'post' && catalogImportRoute) {
    return Promise.resolve(ok(config, {
      success: true,
      data: { success: true, importedCount: 3, errors: [] },
    }));
  }

  // Payments checkout mock
  if (method === 'post' && match(url, '/api/v1/payments/checkout')) {
    return Promise.resolve(ok(config, { success: true, url: '' }));
  }

  // Product delete
  const prodDelete = match(url, '/api/v1/stores/:storeId/products/:id');
  if (method === 'delete' && prodDelete) {
    const prods = MOCK_PRODUCTS[prodDelete.storeId] ?? [];
    const idx = prods.findIndex((p: MockRecord) => p.id === prodDelete.id);
    if (idx >= 0) prods.splice(idx, 1);
    return Promise.resolve(ok(config, { success: true }));
  }

  // Admin tenants stats
  if (method === 'get' && match(url, '/api/v1/admin/tenants/stats')) {
    return Promise.resolve(ok(config, {
      success: true,
      data: { totalTenants: 1, activeTenants: 1, suspendedTenants: 0, planDistribution: { Enterprise: 1 } },
    }));
  }

  // Admin tenants list
  if (method === 'get' && match(url, '/api/v1/admin/tenants')) {
    return Promise.resolve(ok(config, { success: true, data: [MOCK_USER] }));
  }

  // Admin tenant by id
  const adminTenantById = match(url, '/api/v1/admin/tenants/:tenantId');
  if (method === 'get' && adminTenantById) {
    return Promise.resolve(ok(config, { success: true, data: MOCK_USER }));
  }

  // Admin suspend/activate/delete/plan
  const adminTenantSuspend = match(url, '/api/v1/admin/tenants/:tenantId/suspend');
  if (method === 'post' && adminTenantSuspend) {
    return Promise.resolve(ok(config, { success: true }));
  }

  const adminTenantActivate = match(url, '/api/v1/admin/tenants/:tenantId/activate');
  if (method === 'post' && adminTenantActivate) {
    return Promise.resolve(ok(config, { success: true }));
  }

  const adminTenantPlan = match(url, '/api/v1/admin/tenants/:tenantId/plan');
  if (method === 'patch' && adminTenantPlan) {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
    MOCK_USER.plan = body.newPlan ?? MOCK_USER.plan;
    return Promise.resolve(ok(config, { success: true }));
  }

  const adminTenantDelete = match(url, '/api/v1/admin/tenants/:tenantId');
  if (method === 'delete' && adminTenantDelete) {
    return Promise.resolve(ok(config, { success: true }));
  }

  // Admin chat conversations
  if (method === 'get' && match(url, '/api/v1/admin/chat/conversations')) {
    return Promise.resolve(ok(config, { success: true, data: [] }));
  }

  // Fallback: endpoint no mockeado
  console.warn('[mockApi] No mock for:', method.toUpperCase(), url);
  return Promise.resolve(ok(config, null, 404));
};
