import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/apiClient';
import type { SearchResult, SearchProduct } from '@/lib/types';

export interface SearchParams {
  q?: string;
  city?: string;
  categoryId?: string;
  available?: boolean;
  minPrice?: number;
  maxPrice?: number;
  page?: number;
  pageSize?: number;
}

// Fila plana que devuelve el backend (SearchProductDto dentro de PagedResult)
interface SearchProductRow {
  productId: string;
  productName: string;
  productDescription?: string;
  price: number;
  currency?: string;
  imageUrl?: string;
  isAvailable: boolean;
  categoryName?: string;
  storeId: string;
  storeName: string;
  storeAddress?: string;
  city?: string;
  latitude?: number;
  longitude?: number;
  storePhone?: string;
  isStoreOpen?: boolean;
  storeLogoUrl?: string;
}

export function useSearch(params: SearchParams, enabled: boolean) {
  return useQuery<SearchResult[]>({
    queryKey: ['search', params],
    queryFn: async () => {
      const res = await api.get('/api/v1/search', { params });
      const payload = res.data.data ?? res.data;
      const rows: SearchProductRow[] = payload?.items ?? (Array.isArray(payload) ? payload : []);

      // Agrupar los productos planos por tienda para la UI
      const byStore = new Map<string, SearchResult>();
      for (const row of rows) {
        let store = byStore.get(row.storeId);
        if (!store) {
          store = {
            storeId: row.storeId,
            storeName: row.storeName,
            storeAddress: { street: row.storeAddress, city: row.city },
            isOpen: row.isStoreOpen,
            phone: row.storePhone ?? undefined,
            products: [],
          };
          byStore.set(row.storeId, store);
        }
        const product: SearchProduct = {
          id: row.productId,
          name: row.productName,
          price: row.price,
          currency: row.currency,
          imageUrl: row.imageUrl ?? undefined,
          isAvailable: row.isAvailable,
          categoryName: row.categoryName ?? undefined,
        };
        store.products!.push(product);
      }
      return Array.from(byStore.values());
    },
    enabled: enabled && !!(params.q || params.city),
  });
}
