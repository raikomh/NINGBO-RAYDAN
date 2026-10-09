import { useLocation, useNavigate } from 'react-router-dom';
import { Chip } from '@mui/material';
import { Storefront, SwapHoriz } from '@mui/icons-material';
import { useWarehouses } from '@/hooks/useOps';
import { useActiveStore } from '@/context/StoreContext';

/**
 * Tienda activa + salida a la pantalla de selección. Cada tienda (almacén) tiene sus propios
 * datos, sin agregados entre tiendas: no existe un modo "todas las tiendas", así que esto no es
 * un dropdown — es un botón que te saca de la tienda actual para entrar a otra.
 * Oculto si el negocio solo tiene una tienda (no hay a dónde cambiar).
 */
export default function StoreSwitcher() {
  const { data: warehouses } = useWarehouses();
  const { storeId } = useActiveStore();
  const navigate = useNavigate();
  const { pathname } = useLocation();

  if (!warehouses || warehouses.length < 2) return null;

  const current = warehouses.find((w) => w.id === storeId);

  return (
    <Chip
      icon={<Storefront fontSize="small" />}
      label={current?.name ?? 'Elegir tienda'}
      onClick={() => navigate(`/dashboard/select-store?next=${encodeURIComponent(pathname)}`)}
      onDelete={() => navigate(`/dashboard/select-store?next=${encodeURIComponent(pathname)}`)}
      deleteIcon={<SwapHoriz fontSize="small" />}
      variant="outlined"
      sx={{ fontWeight: 600 }}
    />
  );
}
