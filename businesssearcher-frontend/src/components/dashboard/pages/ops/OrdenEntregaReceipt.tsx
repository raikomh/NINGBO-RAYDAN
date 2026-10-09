import { Box, Typography, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material';
import { useBusinessInfo, useWarehouses } from '@/hooks/useOps';
import type { OpsSale } from '@/lib/opsTypes';

export interface OrdenEntregaDeliveryInfo {
  noOrden?: string;
  cliente?: string;
  ci?: string;
  telefono?: string;
  direccion?: string;
  domicilio?: number;
}

/**
 * Orden de entrega imprimible, modelada sobre "modelo de orden de entrega.xlsx" que dio el
 * negocio como referencia. Primera versión funcional: el cliente/CI/teléfono/dirección/domicilio
 * no se persisten (no hay ese dato en el sistema todavía), se capturan solo para esta impresión.
 * Pendiente de reestructuración según indique el negocio más adelante.
 */
export default function OrdenEntregaReceipt({
  sale, delivery, codeByProductId, className,
}: {
  sale: OpsSale;
  delivery?: OrdenEntregaDeliveryInfo | null;
  codeByProductId?: Record<string, string | undefined>;
  className?: string;
}) {
  const { data: business } = useBusinessInfo();
  const { data: warehouses } = useWarehouses();
  const storeWarehouseId = sale.items[0]?.warehouseId;
  const storeAddress = warehouses?.find((w) => w.id === storeWarehouseId)?.location;
  const domicilio = delivery?.domicilio ?? 0;
  const importeTotal = sale.subtotal - sale.discount + domicilio;

  return (
    <Box className={className} sx={{ p: 2, maxWidth: 420, mx: 'auto', fontFamily: 'Arial, sans-serif' }}>
      <Typography align="center" variant="h6" fontWeight={800}>ORDEN DE ENTREGA</Typography>
      {(sale.warehouseName || business?.name) && (
        <Typography align="center" fontWeight={700} sx={{ mt: 0.5 }}>{sale.warehouseName || business?.name}</Typography>
      )}

      <Box display="flex" justifyContent="space-between" mt={1.5}>
        <Typography variant="body2"><strong>Cliente:</strong> {delivery?.cliente || '—'}</Typography>
        <Typography variant="body2"><strong>No.Orden:</strong> {delivery?.noOrden || sale.id.slice(0, 8).toUpperCase()}</Typography>
      </Box>
      <Box display="flex" justifyContent="space-between">
        <Typography variant="body2">
          <strong>ID:</strong> {delivery?.ci || '—'}&nbsp;&nbsp;&nbsp;<strong>Telf:</strong> {delivery?.telefono || '—'}
        </Typography>
        <Typography variant="body2"><strong>Fecha:</strong> {new Date(sale.date).toLocaleDateString('es-ES')}</Typography>
      </Box>
      <Box display="flex" justifyContent="space-between">
        <Typography variant="body2" sx={{ mt: 0.5 }}>
          <strong>Dirección:</strong> {delivery?.direccion || '—'}
        </Typography>
        <Typography variant="body2"><strong>Código:</strong> {sale.managerCode || '—'}</Typography>
      </Box>

      <Table size="small" sx={{ mt: 1.5 }}>
        <TableHead>
          <TableRow>
            <TableCell sx={{ fontWeight: 700 }}>No</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Código</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Producto</TableCell>
            <TableCell sx={{ fontWeight: 700 }} align="right">Cantidad</TableCell>
            <TableCell sx={{ fontWeight: 700 }} align="right">P. Unitario</TableCell>
            <TableCell sx={{ fontWeight: 700 }} align="right">Importe</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {sale.items.map((it, i) => (
            <TableRow key={i}>
              <TableCell>{i + 1}</TableCell>
              <TableCell>{codeByProductId?.[it.productId] || '—'}</TableCell>
              <TableCell>{it.productName}</TableCell>
              <TableCell align="right">{it.quantity}</TableCell>
              <TableCell align="right">{it.unitPrice.toFixed(2)}</TableCell>
              <TableCell align="right">{it.lineTotal.toFixed(2)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <Box sx={{ mt: 1.5, ml: 'auto', width: 220 }}>
        <Box display="flex" justifyContent="space-between">
          <Typography variant="body2">Subtotal</Typography>
          <Typography variant="body2">{(sale.subtotal - sale.discount).toFixed(2)} {sale.paymentCurrency}</Typography>
        </Box>
        <Box display="flex" justifyContent="space-between">
          <Typography variant="body2">Domicilio</Typography>
          <Typography variant="body2">{domicilio.toFixed(2)} {sale.paymentCurrency}</Typography>
        </Box>
        <Box display="flex" justifyContent="space-between" mt={0.5}>
          <Typography variant="body1" fontWeight={700}>Importe Total</Typography>
          <Typography variant="body1" fontWeight={700}>{importeTotal.toFixed(2)} {sale.paymentCurrency}</Typography>
        </Box>
      </Box>

      <Typography align="center" fontWeight={700} sx={{ mt: 3 }}>GRACIAS POR ELEGIRNOS</Typography>
      {storeAddress && (
        <Typography align="center" variant="caption" display="block" color="text.secondary" sx={{ mt: 0.5 }}>
          Dirección: {storeAddress}
        </Typography>
      )}
    </Box>
  );
}
