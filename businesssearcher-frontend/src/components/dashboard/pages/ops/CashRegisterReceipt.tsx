import { Box, Typography, Divider } from '@mui/material';
import { useBusinessInfo } from '@/hooks/useOps';
import { computeCashRegisterSummary } from '@/lib/cashRegisterSummary';
import type { OpsCashRegister, OpsCashMovement, OpsSale } from '@/lib/opsTypes';

interface Props {
  register: OpsCashRegister;
  movements: OpsCashMovement[];
  sales: OpsSale[];
  cashierName?: string;
  actualAmount?: number;
  actualAmountUsd?: number;
}

/** Contenido imprimible del cierre de caja (Z). Invisible en pantalla; solo se muestra al
 * imprimir (ver regla .receipt-print-area en index.css), igual que SaleReceipt. */
export default function CashRegisterReceipt({ register, movements, sales, cashierName, actualAmount, actualAmountUsd }: Props) {
  const { data: business } = useBusinessInfo();
  const { expected, expectedUsd, cashIn, cashOut, changeGiven, cashSalesUsd, transfers, cards, products } =
    computeCashRegisterSummary(register, movements, sales);

  return (
    <Box className="receipt-print-area" sx={{ p: 2, fontFamily: 'monospace', maxWidth: 320, mx: 'auto' }}>
      <Typography align="center" fontWeight={700} sx={{ fontFamily: 'monospace' }}>
        {business?.name ?? 'Cierre de caja'} (Z)
      </Typography>
      {business?.address && <Typography align="center" variant="caption" display="block" sx={{ fontFamily: 'monospace' }}>{business.address}</Typography>}

      <Divider sx={{ my: 1, borderStyle: 'dashed' }} />
      {cashierName && <Typography variant="caption" display="block">Cajero: {cashierName}</Typography>}
      <Typography variant="caption" display="block">Apertura: {new Date(register.openDate).toLocaleString('es-ES')}</Typography>
      <Typography variant="caption" display="block">Cierre: {new Date().toLocaleString('es-ES')}</Typography>
      <Divider sx={{ my: 1, borderStyle: 'dashed' }} />

      <Row label="Monto inicial" value={register.initialAmount} />
      <Row label="Ventas en efectivo" value={register.totalSales} sign="+" />
      <Row label="Cambio entregado" value={changeGiven} sign="-" />
      <Row label="Ingresos manuales" value={cashIn} sign="+" />
      <Row label="Retiros de efectivo" value={cashOut} sign="-" />
      <Row label="Gastos registrados" value={register.totalExpenses} sign="-" />

      <Divider sx={{ my: 1, borderStyle: 'dashed' }} />
      <Box display="flex" justifyContent="space-between">
        <Typography variant="body1" fontWeight={700}>Efectivo esperado</Typography>
        <Typography variant="body1" fontWeight={700}>{expected.toFixed(2)}</Typography>
      </Box>
      {actualAmount != null && (
        <>
          <Row label="Efectivo contado" value={actualAmount} />
          <Row label="Diferencia" value={Math.round((actualAmount - expected) * 100) / 100} />
        </>
      )}

      {(register.initialAmountUSD != null || cashSalesUsd > 0) && (
        <>
          <Divider sx={{ my: 1, borderStyle: 'dashed' }} />
          <Typography variant="caption" fontWeight={700} display="block">EN USD (informativo)</Typography>
          <Row label="Monto inicial" value={register.initialAmountUSD ?? 0} />
          <Row label="Ventas en efectivo" value={cashSalesUsd} sign="+" />
          <Box display="flex" justifyContent="space-between">
            <Typography variant="body1" fontWeight={700}>Esperado</Typography>
            <Typography variant="body1" fontWeight={700}>{(expectedUsd ?? 0).toFixed(2)}</Typography>
          </Box>
          {actualAmountUsd != null && (
            <>
              <Row label="Contado" value={actualAmountUsd} />
              <Row label="Diferencia" value={Math.round((actualAmountUsd - (expectedUsd ?? 0)) * 100) / 100} />
            </>
          )}
        </>
      )}

      {(transfers.length > 0 || cards.length > 0) && (
        <>
          <Divider sx={{ my: 1, borderStyle: 'dashed' }} />
          <Typography variant="caption" fontWeight={700} display="block">TRANSFERENCIAS</Typography>
          {transfers.length === 0 && <Typography variant="caption" display="block">0 operaciones</Typography>}
          {transfers.map((t) => (
            <Row key={t.currency} label={`${t.currency} (${t.count} op.)`} value={t.amount} />
          ))}
          <Typography variant="caption" fontWeight={700} display="block" sx={{ mt: 0.5 }}>TARJETAS</Typography>
          {cards.length === 0 && <Typography variant="caption" display="block">0 operaciones</Typography>}
          {cards.map((t) => (
            <Row key={t.currency} label={`${t.currency} (${t.count} op.)`} value={t.amount} />
          ))}
        </>
      )}

      {products.length > 0 && (
        <>
          <Divider sx={{ my: 1, borderStyle: 'dashed' }} />
          <Typography variant="caption" fontWeight={700} display="block">RESUMEN DE PRODUCTOS VENDIDOS</Typography>
          {products.map((p) => (
            <Box key={p.productId} display="flex" justifyContent="space-between">
              <Typography variant="caption" sx={{ fontFamily: 'monospace' }}>{p.quantity}x {p.name}</Typography>
              <Typography variant="caption" sx={{ fontFamily: 'monospace' }}>{p.total.toFixed(2)}</Typography>
            </Box>
          ))}
        </>
      )}

      <Divider sx={{ my: 1, borderStyle: 'dashed' }} />
      <Typography align="center" variant="caption" display="block">
        {products.reduce((s, p) => s + p.quantity, 0)} artículos · {sales.filter((s) => s.status !== 'Refunded').length} ventas
      </Typography>
    </Box>
  );
}

function Row({ label, value, sign }: { label: string; value: number; sign?: '+' | '-' }) {
  return (
    <Box display="flex" justifyContent="space-between">
      <Typography variant="body2" sx={{ fontFamily: 'monospace' }}>{label}</Typography>
      <Typography variant="body2" sx={{ fontFamily: 'monospace' }}>{sign ?? ''}{value.toFixed(2)}</Typography>
    </Box>
  );
}
