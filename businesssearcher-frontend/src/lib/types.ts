export type TenantType = 'Wholesale' | 'Retail';

export interface User {
  id: string;
  email: string;
  businessName: string;
  plan: string;
  role?: 'admin' | 'tenant';
  tenantType?: TenantType;
  // Sesión de sub-usuario del TPV (empleado). Vacío para el dueño del negocio.
  isOpsUser?: boolean;
  opsRole?: string;
  name?: string;
}

export interface Address {
  street?: string;
  city?: string;
  state?: string;
  country?: string;
  latitude?: number;
  longitude?: number;
}

export interface Schedule {
  id?: string;
  dayOfWeek: string;
  openTime?: string;
  closeTime?: string;
  isClosed: boolean;
}

export interface Store {
  id: string;
  name: string;
  description?: string;
  address?: Address;
  phone?: string;
  logoUrl?: string;
  isOpen?: boolean;
  schedules?: Schedule[];
}

export interface Category {
  id: string;
  name: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
}

export interface Product {
  id: string;
  name: string;
  description?: string;
  price: number;
  currency?: string;
  categoryId?: string;
  categoryName?: string;
  imageUrl?: string;
  stock: number;
  isAvailable: boolean;
  isFeatured?: boolean;
  minOrderQuantity?: number;
}

export interface CreateStoreDto {
  name: string;
  description?: string;
  address?: Address;
  phone?: string;
  logoUrl?: string;
}

export interface UpdateStoreDto extends CreateStoreDto {}

export interface CreateProductDto {
  name: string;
  description?: string;
  price: number;
  currency?: string;
  categoryId: string;
  imageUrl?: string;
  stock: number;
  minOrderQuantity?: number;
}

export interface UpdateProductDto {
  name?: string;
  description?: string;
  price?: number;
  currency?: string;
  categoryId?: string;
  imageUrl?: string;
}

export interface CreateCategoryDto {
  name: string;
  description?: string;
  sortOrder: number;
}

export interface UpdateCategoryDto extends CreateCategoryDto {}

export interface AddScheduleDto {
  dayOfWeek: string;
  openTime?: string;
  closeTime?: string;
  isClosed: boolean;
}

export interface LoginDto {
  email: string;
  password: string;
}

export interface RegisterDto {
  businessName: string;
  email: string;
  password: string;
  tenantType: TenantType;
  plan?: string;
}

export interface AuthResponse {
  token: string;
  refreshToken?: string;
}

// Transactions
export interface Transaction {
  id: string;
  type: 'Sale' | 'Entry' | 'Spoilage';
  productId: string;
  productName?: string;
  quantity: number;
  unitPrice?: number;
  totalAmount?: number;
  notes?: string;
  createdAt: string;
}

export interface RecordSaleDto {
  productId: string;
  quantity: number;
  notes?: string;
}

export interface RecordEntryDto {
  productId: string;
  quantity: number;
  unitPrice: number;
  notes?: string;
}

export interface RecordSpoilageDto {
  productId: string;
  quantity: number;
  notes?: string;
}

// Dashboard
export interface DashboardStats {
  totalProducts?: number;
  availableProducts?: number;
  totalCategories?: number;
  totalTransactions?: number;
  totalSales?: number;
  totalEntries?: number;
  totalSpoilages?: number;
  lowStockProducts?: number;
  revenue?: number;
  [key: string]: unknown;
}

export interface MonthlyReport {
  month: number;
  year: number;
  totalSales?: number;
  totalEntries?: number;
  totalSpoilages?: number;
  revenue?: number;
  [key: string]: unknown;
}

// Search
export interface SearchProduct {
  id: string;
  name: string;
  price: number;
  currency?: string;
  imageUrl?: string;
  isAvailable: boolean;
  categoryName?: string;
  minOrderQuantity?: number;
}

// Wholesale catalog (directorio de mayoristas, visible para tenants Retail)
export interface WholesaleCatalogItem {
  storeId: string;
  storeName: string;
  storeAddress?: string;
  city?: string;
  storePhone?: string;
  productId: string;
  productName: string;
  productDescription?: string;
  price: number;
  currency: string;
  stock: number;
  minOrderQuantity: number;
  categoryName?: string;
}

export interface ImportCatalogResult {
  success: boolean;
  importedCount: number;
  errors: string[];
}

export interface SearchResult {
  storeId: string;
  storeName: string;
  storeAddress?: Address;
  isOpen?: boolean;
  phone?: string;
  products?: SearchProduct[];
  [key: string]: unknown;
}

// Chat
export interface ChatMessage {
  id: string;
  role?: string;
  text: string;
  isRead?: boolean;
  createdAt?: string;
  isFromAdmin?: boolean;
}

// Admin
export interface TenantAdmin {
  id: string;
  businessName: string;
  email: string;
  plan: string;
  status: string;
  isEmailVerified: boolean;
  isApproved: boolean;
  isSubscriptionActive: boolean;
  createdAt: string;
}

export interface ClientAdmin {
  id: string;
  fullName: string;
  email: string;
  plan: string;
  isEmailVerified: boolean;
  isApproved: boolean;
  reputationScore: number;
  reportsSubmitted: number;
  createdAt: string;
  premiumRequested: boolean;
}

export interface AdminStats {
  totalTenants?: number;
  activeTenants?: number;
  suspendedTenants?: number;
  planDistribution?: Record<string, number>;
  [key: string]: unknown;
}

// ── Referidos con recompensa en CUP ──
export interface ReferralPayoutAdmin {
  clientId: string;
  fullName: string;
  email: string;
  cupBalance: number;
  cupPaidTotal: number;
  mipymeReferralsTotal: number;
  mipymeBonusTotal: number;
}

export interface ReferralLeaderboardEntry {
  rank: number;
  name: string;
  validReferralsThisMonth: number;
  isMe: boolean;
}

export interface ReferralLeaderboard {
  year: number;
  month: number;
  top: ReferralLeaderboardEntry[];
  myRank?: number | null;
  myCount: number;
}

// ── MiPymes referidas por un cliente ──
export interface MipymeReferralLeaderboardEntry {
  rank: number;
  name: string;
  mipymeReferralsThisMonth: number;
  isMe: boolean;
}

export interface MipymeReferralLeaderboard {
  year: number;
  month: number;
  top: MipymeReferralLeaderboardEntry[];
  myRank?: number | null;
  myCount: number;
}

export interface MonthlySettlementReceipt {
  year: number;
  month: number;
  winnerClientId?: string | null;
  winnerName?: string | null;
  prizeAmount: number;
  totalValidReferrals: number;
  alreadySettled: boolean;
}

export interface MarketingVisitSummary {
  source: string;
  visitCount: number;
  lastVisitAt: string;
}

export interface OverdueTenant {
  tenantId: string;
  businessName: string;
  email: string;
  status: string;
  lastPaymentDate: string;
  daysOverdue: number;
}
