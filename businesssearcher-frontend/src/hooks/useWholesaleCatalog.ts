import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/apiClient';
import type { WholesaleCatalogItem } from '@/lib/types';

export interface WholesaleCatalogParams {
  q?: string;
  city?: string;
  page?: number;
  pageSize?: number;
}

export function useWholesaleCatalog(params: WholesaleCatalogParams, enabled: boolean) {
  return useQuery<{ items: WholesaleCatalogItem[]; total: number }>({
    queryKey: ['wholesale-catalog', params],
    queryFn: async () => {
      const res = await api.get('/api/v1/wholesale-catalog', { params });
      const payload = res.data?.data ?? res.data;
      const items: WholesaleCatalogItem[] = payload?.items ?? (Array.isArray(payload) ? payload : []);
      return { items, total: payload?.totalCount ?? items.length };
    },
    enabled,
  });
}
