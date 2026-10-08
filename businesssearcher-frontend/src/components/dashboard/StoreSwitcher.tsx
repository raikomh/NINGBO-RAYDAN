import { TextField, MenuItem } from '@mui/material';
import { Storefront } from '@mui/icons-material';
import { useWarehouses } from '@/hooks/useOps';
import { useActiveStore } from '@/context/StoreContext';

/**
 * Selector de tienda activa: una MiPyme puede tener varias tiendas (almacenes) con datos que no
 * se comparten entre sí. "Todas las tiendas" agrega los datos de todas (comportamiento previo).
 * Oculto si el negocio solo tiene una tienda (no hay nada que elegir).
 */
export default function StoreSwitcher() {
  const { data: warehouses } = useWarehouses();
  const { storeId, setStoreId } = useActiveStore();

  if (!warehouses || warehouses.length < 2) return null;

  return (
    <TextField
      size="small"
      select
      value={storeId ?? ''}
      onChange={(e) => setStoreId(e.target.value || null)}
      InputProps={{ startAdornment: <Storefront fontSize="small" sx={{ mr: 0.5, color: 'text.secondary' }} /> }}
      sx={{ minWidth: 170 }}
    >
      <MenuItem value="">Todas las tiendas</MenuItem>
      {warehouses.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
    </TextField>
  );
}
