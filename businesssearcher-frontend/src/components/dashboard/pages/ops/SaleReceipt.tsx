import { Box, Typography, Divider } from '@mui/material';
import { useBusinessInfo } from '@/hooks/useOps';
import type { OpsSale } from '@/lib/opsTypes';

const METHOD_LABEL: Record<string, string> = { Cash: 'Efectivo', Card: 'Tarjeta', Transfer: 'Transferencia', Mixed: 'Mixto' };

/** Contenido imprimible de un ticket de venta. Invisible en pantalla; solo se muestra al imprimir
 * (ver regla .receipt-print-area en index.css), para no interferir con el diálogo de detalle. */
export default function SaleReceipt({ sale, className }: { sale: OpsSale; className?: string }) {
  const { data: business } = useBusinessInfo();

  return (
    <Box className={className} sx={{ p: 2, fontFamily: 'monospace', maxWidth: 320, mx: 'auto' }}>
      <Typography align="center" fontWeight={700} sx={{ fontFamily: 'monospace' }}>
        {business?.name ?? 'Recibo de venta'}
      </Typography>
      {business?.address && <Typography align="center" variant="caption" display="block" sx={{ fontFamily: 'monospace' }}>{business.address}</Typography>}
      {business?.phone && <Typography align="center" variant="caption" display="block" sx={{ fontFamily: 'monospace' }}>{business.phone}</Typography>}
      {business?.taxId && <Typography align="center" variant="caption" display="block" sx={{ fontFamily: 'monospace' }}>NIT: {business.taxId}</Typography>}

      <Divider sx={{ my: 1, borderStyle: 'dashed' }} />
      <Typography variant="caption" display="block">{new Date(sale.date).toLocaleString('es-ES')}</Typography>
      {sale.warehouseName && <Typography variant="caption" display="block">Almacén: {sale.warehouseName}</Typography>}
      <Typography variant="caption" display="block">Venta #{sale.id.slice(0, 8)}</Typography>
      <Divider sx={{ my: 1, borderStyle: 'dashed' }} />

      {sale.items.map((it, i) => (
        <Box key={i} sx={{ mb: 0.5 }}>
          <Typography variant="body2" sx={{ fontFamily: 'monospace' }}>{it.productName}</Typography>
          <Box display="flex" justifyContent="space-between">
            <Typography variant="caption" sx={{ fontFamily: 'monospace' }}>{it.quantity} x {it.unitPrice.toFixed(2)}</Typography>
            <Typography variant="caption" sx={{ fontFamily: 'monospace' }}>{it.lineTotal.toFixed(2)}</Typography>
          </Box>
        </Box>
      ))}

      <Divider sx={{ my: 1, borderStyle: 'dashed' }} />
      <Box display="flex" justifyContent="space-between"><Typography variant="body2">Subtotal</Typography><Typography variant="body2">{sale.subtotal.toFixed(2)}</Typography></Box>
      {sale.discount > 0 && (
        <Box display="flex" justifyContent="space-between"><Typography variant="body2">Descuento</Typography><Typography variant="body2">-{sale.discount.toFixed(2)}</Typography></Box>
      )}
      <Box display="flex" justifyContent="space-between" mt={0.5}>
        <Typography variant="body1" fontWeight={700}>TOTAL</Typography>
        <Typography variant="body1" fontWeight={700}>{sale.total.toFixed(2)} {sale.paymentCurrency}</Typography>
      </Box>
      {sale.totalUSD != null && sale.paymentCurrency !== 'USD' && (
        <Box display="flex" justifyContent="space-between">
          <Typography variant="caption" color="text.secondary">Total USD</Typography>
          <Typography variant="caption" color="text.secondary">${sale.totalUSD.toFixed(2)}</Typography>
        </Box>
      )}

      <Divider sx={{ my: 1, borderStyle: 'dashed' }} />
      {sale.payments.map((p, i) => (
        <Box key={i} display="flex" justifyContent="space-between">
          <Typography variant="caption">{METHOD_LABEL[p.method] ?? p.method}</Typography>
          <Typography variant="caption">{p.amount.toFixed(2)} {p.currency}</Typography>
        </Box>
      ))}

      {sale.status === 'Refunded' && (
        <Typography align="center" fontWeight={700} sx={{ mt: 1 }}>*** REEMBOLSADA ***</Typography>
      )}

      <Divider sx={{ my: 1, borderStyle: 'dashed' }} />
      <Typography align="center" variant="caption" display="block">¡Gracias por su compra!</Typography>
    </Box>
  );
}
