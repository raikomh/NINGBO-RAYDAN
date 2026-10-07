import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/apiClient';
import type { Store, UpdateStoreDto } from '@/lib/types';

// Single-store model: each tenant owns exactly one store.
// GET  /api/v1/store            → fetch tenant's store
// PUT  /api/v1/store            → create / update the store (SetupStoreDto)
// PATCH /api/v1/store/activate  → make store visible in search
// PATCH /api/v1/store/deactivate

const QUERY_KEY = ['store'] as const;

export function useStore(_id?: string) {
  return useQuery<Store | null>({
    queryKey: QUERY_KEY,
    queryFn: async () => {
      try {
        const res = await api.get('/api/v1/store');
        // Sin tienda el backend responde 200 con data: null — devolver null, no {success:true}
        const data = res.data?.data;
        if (!data || !data.id) return null;
        // Backend expone isOpenNow / isActive; el front usa isOpen
        return { ...data, isOpen: data.isOpenNow ?? data.isActive } as Store;
      } catch (err: unknown) {
        // 404 means tenant has no store yet — return null (not an error)
        if ((err as { response?: { status: number } }).response?.status === 404) {
          return null;
        }
        throw err;
      }
    },
  });
}

// Alias kept for OverviewPage compatibility
export { useStore as useStores };

export function useSetupStore() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: UpdateStoreDto) => api.put('/api/v1/store', dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEY }),
  });
}

// useCreateStore and useUpdateStore both map to PUT /api/v1/store
export function useCreateStore() {
  return useSetupStore();
}

export function useUpdateStore(_id?: string) {
  return useSetupStore();
}

export function useUploadStoreLogo(storeId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData();
      form.append('file', file);
      return api.post(`/api/v1/uploads/stores/${storeId}/logo`, form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEY }),
  });
}

export function useActivateStore(_id?: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.patch('/api/v1/store/activate'),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEY }),
  });
}

export function useDeactivateStore(_id?: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.patch('/api/v1/store/deactivate'),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEY }),
  });
}
