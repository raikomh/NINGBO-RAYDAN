import { useState } from 'react';
import {
  Box, Card, CardContent, Typography, Button, TextField, MenuItem, Stack, Grid, Divider,
  Chip, Table, TableBody, TableCell, TableHead, TableRow, TableContainer, Alert,
  Dialog, DialogTitle, DialogContent, DialogActions, ToggleButtonGroup, ToggleButton, CircularProgress,
} from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { LockOpen, Lock, TrendingUp, TrendingDown, FactCheck, Print } from '@mui/icons-material';
import {
  useCurrentCashRegister, useOpenRegister, useCloseRegister, useAddCashMovement,
  useCashMovements, useWarehouses, useOpsSales, useBusinessInfo,
} from '@/hooks/useOps';
import { useAuth } from '@/context/AuthContext';
import { computeCashRegisterSummary } from '@/lib/cashRegisterSummary';
import type { OpsCashRegister, OpsCashMovement, OpsSale } from '@/lib/opsTypes';
import CashRegisterReceipt from './CashRegisterReceipt';
import CashDenominationBreakdown from './CashDenominationBreakdown';

export default function CashRegisterPage() {
  const navigate = useNavigate();
  const { data: register, isLoading } = useCurrentCashRegister();
  const { data: warehouses } = useWarehouses();
  const { data: movements } = useCashMovements(register?.id);
  const { data: sales } = useOpsSales(register ? { registerId: register.id } : undefined);
  const { data: business } = useBusinessInfo();
  const { user } = useAuth();

  const open = useOpenRegister();
  const close = useCloseRegister();
  const addMov = useAddCashMovement();

  const [initial, setInitial] = useState<number | ''>('');
  const [initialUsd, setInitialUsd] = useState<number | ''>('');
  const [warehouseId, setWarehouseId] = useState('');
  const [closeDialog, setCloseDialog] = useState(false);
  const [movDialog, setMovDialog] = useState(false);

  if (isLoading) return <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>;

  const expected = register
    ? register.initialAmount + register.totalSales + register.totalCashIn - register.totalCashOut - register.totalExpenses
    : 0;

  return (
    <Box>
      <Typography variant="h5" fontWeight={700} mb={3}>Caja / Arqueo</Typography>

      {!register ? (
        <Card variant="outlined" sx={{ maxWidth: 460 }}>
          <CardContent>
            <Typography variant="h6" fontWeight={600} mb={1}>Abrir caja</Typography>
            <Typography variant="body2" color="text.secondary" mb={2}>
              Registra el monto inicial en efectivo y el almacén del turno. El almacén es obligatorio: es lo que permite
              exigir el conteo físico de inventario antes de poder cerrar la caja.
            </Typography>
            <Stack spacing={2}>
              <TextField size="small" type="number" label="Monto inicial (CUP)" value={initial}
                onChange={(e) => setInitial(e.target.value === '' ? '' : Number(e.target.value))} />
              <CashDenominationBreakdown currency="CUP" onApply={(t) => setInitial(t)} />
              <TextField size="small" type="number" label="Monto inicial (USD)" value={initialUsd}
                onChange={(e) => setInitialUsd(e.target.value === '' ? '' : Number(e.target.value))} />
              <CashDenominationBreakdown currency="USD" onApply={(t) => setInitialUsd(t)} />
              <TextField size="small" select required label="Almacén" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
                {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
                {(warehouses ?? []).length === 0 && <MenuItem value="" disabled>No hay almacenes creados</MenuItem>}
              </TextField>
              {open.isError && <Alert severity="error">No se pudo abrir la caja.</Alert>}
              <Button variant="contained" startIcon={<LockOpen />} disabled={open.isPending || !warehouseId}
                onClick={() => open.mutate({
                  initialAmount: initial === '' ? 0 : initial,
                  initialAmountUSD: initialUsd === '' ? undefined : initialUsd,
                  warehouseId,
                })}>Abrir caja</Button>
            </Stack>
          </CardContent>
        </Card>
      ) : (
        <>
          <Grid container spacing={2} mb={2}>
            <Metric label="Monto inicial" value={register.initialAmount} valueUsd={register.initialAmountUSD} />
            <Metric label="Ventas (efectivo)" value={register.totalSales} />
            <Metric label="Entradas" value={register.totalCashIn} />
            <Metric label="Salidas + gastos" value={register.totalCashOut + register.totalExpenses} />
            <Metric label="Esperado en caja" value={expected} highlight />
          </Grid>

          <Stack direction="row" spacing={1.5} mb={3} flexWrap="wrap">
            <Chip color="success" icon={<LockOpen />} label={`Caja abierta · ${register.salesCount} ventas`} />
            {register.warehouseId && (
              <Chip
                color={register.inventoryCountCompleted ? 'success' : 'warning'}
                variant="outlined"
                icon={<FactCheck fontSize="small" />}
                label={register.inventoryCountCompleted ? 'Conteo del turno hecho' : 'Sin conteo este turno'}
                onClick={register.inventoryCountCompleted ? undefined : () => navigate('/dashboard/ops/counts')}
              />
            )}
            <Button size="small" variant="outlined" startIcon={<TrendingUp />} onClick={() => setMovDialog(true)}>Movimiento</Button>
            <Button size="small" variant="contained" color="error" startIcon={<Lock />} onClick={() => setCloseDialog(true)}>Cerrar caja</Button>
          </Stack>

          <Grid container spacing={2}>
            <Grid item xs={12} md={6}>
              <Typography variant="subtitle2" fontWeight={700} mb={1}>Movimientos</Typography>
              <TableContainer component={Card} variant="outlined">
                <Table size="small">
                  <TableHead><TableRow><TableCell>Tipo</TableCell><TableCell>Descripción</TableCell><TableCell align="right">Monto</TableCell></TableRow></TableHead>
                  <TableBody>
                    {(movements ?? []).map((m) => (
                      <TableRow key={m.id}>
                        <TableCell><Chip size="small" label={m.type} icon={m.type === 'In' ? <TrendingUp fontSize="small" /> : <TrendingDown fontSize="small" />} /></TableCell>
                        <TableCell>{m.description ?? '—'}</TableCell>
                        <TableCell align="right">{m.amount.toFixed(2)} {m.currency}</TableCell>
                      </TableRow>
                    ))}
                    {(movements ?? []).length === 0 && <TableRow><TableCell colSpan={3} align="center" sx={{ py: 3, color: 'text.secondary' }}>Sin movimientos</TableCell></TableRow>}
                  </TableBody>
                </Table>
              </TableContainer>
            </Grid>
            <Grid item xs={12} md={6}>
              <Typography variant="subtitle2" fontWeight={700} mb={1}>Ventas del turno</Typography>
              <TableContainer component={Card} variant="outlined">
                <Table size="small">
                  <TableHead><TableRow><TableCell>Hora</TableCell><TableCell>Pago</TableCell><TableCell align="right">Total</TableCell></TableRow></TableHead>
                  <TableBody>
                    {(sales ?? []).map((s) => (
                      <TableRow key={s.id}>
                        <TableCell>{new Date(s.date).toLocaleTimeString()}</TableCell>
                        <TableCell>{s.paymentMethod}{s.status === 'Refunded' && <Chip size="small" color="error" label="Reemb." sx={{ ml: 0.5 }} />}</TableCell>
                        <TableCell align="right">{s.total.toFixed(2)} {s.paymentCurrency}</TableCell>
                      </TableRow>
                    ))}
                    {(sales ?? []).length === 0 && <TableRow><TableCell colSpan={3} align="center" sx={{ py: 3, color: 'text.secondary' }}>Sin ventas</TableCell></TableRow>}
                  </TableBody>
                </Table>
              </TableContainer>
            </Grid>
          </Grid>
        </>
      )}

      {closeDialog && register && (
        <CloseDialog expected={expected} pending={close.isPending} error={close.isError}
          hasWarehouse={!!register.warehouseId} inventoryCountCompleted={register.inventoryCountCompleted}
          register={register} movements={movements ?? []} sales={sales ?? []}
          businessName={business?.name} cashierName={user?.name}
          onClose={() => setCloseDialog(false)}
          onConfirm={(actual, actualUsd) => close.mutate({ id: register.id, actualAmount: actual, actualAmountUSD: actualUsd }, { onSuccess: () => setCloseDialog(false) })} />
      )}
      {movDialog && register && (
        <MovementDialog pending={addMov.isPending}
          onClose={() => setMovDialog(false)}
          onConfirm={(type, amount, description) =>
            addMov.mutate({ registerId: register.id, type, amount, description }, { onSuccess: () => setMovDialog(false) })} />
      )}
    </Box>
  );
}

function Metric({ label, value, valueUsd, highlight }: { label: string; value: number; valueUsd?: number; highlight?: boolean }) {
  return (
    <Grid item xs={6} sm={4} md={2.4}>
      <Card variant="outlined" sx={{ bgcolor: highlight ? 'primary.main' : undefined, color: highlight ? 'primary.contrastText' : undefined }}>
        <CardContent sx={{ py: 1.5 }}>
          <Typography variant="caption" sx={{ opacity: 0.85 }}>{label}</Typography>
          <Typography variant="h6" fontWeight={800}>{value.toFixed(2)} CUP</Typography>
          {valueUsd != null && <Typography variant="caption" sx={{ opacity: 0.85 }} display="block">${valueUsd.toFixed(2)} USD</Typography>}
        </CardContent>
      </Card>
    </Grid>
  );
}

function CloseDialog({ expected, onClose, onConfirm, pending, error, hasWarehouse, inventoryCountCompleted, register, movements, sales, businessName, cashierName }: {
  expected: number; onClose: () => void; onConfirm: (actual: number, actualUsd?: number) => void; pending: boolean; error: boolean;
  hasWarehouse: boolean; inventoryCountCompleted: boolean;
  register: OpsCashRegister; movements: OpsCashMovement[]; sales: OpsSale[];
  businessName?: string; cashierName?: string;
}) {
  // Vacío a propósito: si arrancara igual a "esperado" el cierre cuadraría solo, sin que el
  // cajero haya contado nada de verdad. Debe escribir el monto real para poder cerrar.
  const [actual, setActual] = useState<number | ''>('');
  const [actualUsd, setActualUsd] = useState<number | ''>('');
  const hasEntered = actual !== '';
  const diff = hasEntered ? Math.round((Number(actual) - expected) * 100) / 100 : null;
  const cashBlocked = !hasEntered || diff !== 0;
  const navigate = useNavigate();
  const countBlocked = hasWarehouse && !inventoryCountCompleted;
  const { cashIn, cashOut, changeGiven, changeGivenUsd, cashSalesUsd, expectedUsd, transfers, cards, products } =
    computeCashRegisterSummary(register, movements, sales);
  const diffUsd = actualUsd !== '' && expectedUsd != null ? Math.round((Number(actualUsd) - expectedUsd) * 100) / 100 : null;

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        Cierre de {businessName ?? 'caja'} (Z)
        {cashierName && <Typography variant="caption" display="block" color="text.secondary">Cajero: {cashierName} · Apertura: {new Date(register.openDate).toLocaleString('es-ES')}</Typography>}
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {countBlocked && (
            <Alert severity="error" icon={<FactCheck fontSize="small" />}
              action={<Button size="small" color="inherit" onClick={() => navigate('/dashboard/ops/counts')}>Hacer conteo</Button>}>
              Debes cerrar y validar (auditar todos los productos) el conteo físico de inventario del almacén antes de poder cerrar la caja.
            </Alert>
          )}

          <Box>
            <Row label="Monto inicial" value={register.initialAmount} />
            <Row label="Ventas en efectivo" value={register.totalSales} sign="+" />
            <Row label="Cambio entregado" value={changeGiven} sign="-" />
            <Row label="Ingresos manuales" value={cashIn} sign="+" />
            <Row label="Retiros de efectivo" value={cashOut} sign="-" />
            <Row label="Gastos registrados" value={register.totalExpenses} sign="-" />
            <Divider sx={{ my: 1 }} />
            <Box display="flex" justifyContent="space-between"><Typography fontWeight={700}>Efectivo esperado (CUP)</Typography><Typography fontWeight={700}>{expected.toFixed(2)}</Typography></Box>
          </Box>

          {(register.initialAmountUSD != null || cashSalesUsd > 0) && (
            <Box>
              <Typography variant="caption" color="text.secondary" display="block" mb={0.5}>EN USD (informativo)</Typography>
              <Row label="Monto inicial" value={register.initialAmountUSD ?? 0} />
              <Row label="Ventas en efectivo" value={cashSalesUsd} sign="+" />
              {changeGivenUsd > 0 && <Row label="Cambio entregado" value={changeGivenUsd} sign="-" />}
              <Divider sx={{ my: 1 }} />
              <Box display="flex" justifyContent="space-between">
                <Typography fontWeight={700}>Efectivo esperado (USD)</Typography>
                <Typography fontWeight={700}>{(expectedUsd ?? 0).toFixed(2)}</Typography>
              </Box>
            </Box>
          )}

          {(transfers.length > 0 || cards.length > 0) && (
            <Grid container spacing={1.5}>
              <Grid item xs={6}>
                <Card variant="outlined"><CardContent sx={{ py: 1 }}>
                  <Typography variant="caption" color="text.secondary" display="block" mb={0.5}>TRANSFERENCIAS</Typography>
                  {transfers.length === 0 && <Typography variant="body2" color="text.secondary">0 operaciones</Typography>}
                  {transfers.map((t) => <Row key={t.currency} label={`${t.currency}`} value={t.amount} caption={`${t.count} op.`} />)}
                </CardContent></Card>
              </Grid>
              <Grid item xs={6}>
                <Card variant="outlined"><CardContent sx={{ py: 1 }}>
                  <Typography variant="caption" color="text.secondary" display="block" mb={0.5}>TARJETAS</Typography>
                  {cards.length === 0 && <Typography variant="body2" color="text.secondary">0 operaciones</Typography>}
                  {cards.map((t) => <Row key={t.currency} label={`${t.currency}`} value={t.amount} caption={`${t.count} op.`} />)}
                </CardContent></Card>
              </Grid>
            </Grid>
          )}

          {products.length > 0 && (
            <Box>
              <Typography variant="subtitle2" fontWeight={700} mb={0.5}>Resumen de productos vendidos</Typography>
              <TableContainer component={Card} variant="outlined" sx={{ maxHeight: 220 }}>
                <Table size="small" stickyHeader>
                  <TableHead><TableRow><TableCell>Producto</TableCell><TableCell align="right">Cant.</TableCell><TableCell align="right">Total</TableCell></TableRow></TableHead>
                  <TableBody>
                    {products.map((p) => (
                      <TableRow key={p.productId}>
                        <TableCell>{p.name}</TableCell>
                        <TableCell align="right">{p.quantity}</TableCell>
                        <TableCell align="right">{p.total.toFixed(2)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </Box>
          )}

          <Divider />

          <TextField size="small" type="number" label="Efectivo real contado (CUP)" placeholder="Cuenta el efectivo e ingresa el monto"
            value={actual} onChange={(e) => setActual(e.target.value === '' ? '' : Number(e.target.value))} disabled={countBlocked} />
          {!countBlocked && (
            <CashDenominationBreakdown currency="CUP" defaultOpen expected={expected} onApply={(t) => setActual(t)} />
          )}
          {hasEntered ? (
            <Alert severity={diff === 0 ? 'success' : 'error'}>
              Diferencia: {diff!.toFixed(2)} {diff! > 0 ? '(sobrante)' : diff! < 0 ? '(faltante)' : ''}
              {cashBlocked && ' — la caja debe cuadrar (diferencia = 0.00) para poder cerrarla. Recuenta el efectivo o registra un movimiento que explique la diferencia.'}
            </Alert>
          ) : (
            <Alert severity="info">Cuenta el efectivo físico de la caja e ingresa el monto para poder cerrarla.</Alert>
          )}

          <TextField size="small" type="number" label="Efectivo real contado (USD)" placeholder="Opcional"
            value={actualUsd} onChange={(e) => setActualUsd(e.target.value === '' ? '' : Number(e.target.value))} disabled={countBlocked} />
          {!countBlocked && (register.initialAmountUSD != null || cashSalesUsd > 0) && (
            <CashDenominationBreakdown currency="USD" defaultOpen expected={expectedUsd} onApply={(t) => setActualUsd(t)} />
          )}
          {diffUsd !== null && (
            <Alert severity={diffUsd === 0 ? 'success' : 'warning'}>
              Diferencia USD: {diffUsd.toFixed(2)} {diffUsd > 0 ? '(sobrante)' : diffUsd < 0 ? '(faltante)' : ''} — informativo, no bloquea el cierre.
            </Alert>
          )}
          {error && <Alert severity="error">No se pudo cerrar la caja.</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button startIcon={<Print />} onClick={() => window.print()}>Imprimir</Button>
        <Box flexGrow={1} />
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" color="error" disabled={pending || countBlocked || cashBlocked}
          onClick={() => onConfirm(Number(actual), actualUsd === '' ? undefined : Number(actualUsd))}>Confirmar cierre</Button>
      </DialogActions>
      <CashRegisterReceipt register={register} movements={movements} sales={sales} cashierName={cashierName}
        actualAmount={hasEntered ? Number(actual) : undefined} actualAmountUsd={actualUsd === '' ? undefined : Number(actualUsd)} />
    </Dialog>
  );
}

function Row({ label, value, sign, caption }: { label: string; value: number; sign?: '+' | '-'; caption?: string }) {
  return (
    <Box display="flex" justifyContent="space-between" alignItems="baseline">
      <Typography variant="body2">{label}{caption && <Typography component="span" variant="caption" color="text.secondary"> · {caption}</Typography>}</Typography>
      <Typography variant="body2">{sign ?? ''}{value.toFixed(2)}</Typography>
    </Box>
  );
}

function MovementDialog({ onClose, onConfirm, pending }: {
  onClose: () => void; onConfirm: (type: string, amount: number, description: string) => void; pending: boolean;
}) {
  const [type, setType] = useState('In');
  const [amount, setAmount] = useState<number | ''>('');
  const [description, setDescription] = useState('');
  const amountValue = amount === '' ? 0 : amount;
  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Movimiento de efectivo</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <ToggleButtonGroup exclusive fullWidth size="small" value={type} onChange={(_, v) => v && setType(v)}>
            <ToggleButton value="In">Entrada</ToggleButton>
            <ToggleButton value="Out">Salida</ToggleButton>
            <ToggleButton value="Expense">Gasto</ToggleButton>
          </ToggleButtonGroup>
          <TextField size="small" type="number" label="Monto" value={amount}
            onChange={(e) => setAmount(e.target.value === '' ? '' : Number(e.target.value))} />
          <TextField size="small" label="Descripción" value={description} onChange={(e) => setDescription(e.target.value)} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" disabled={pending || amountValue <= 0} onClick={() => onConfirm(type, amountValue, description)}>Registrar</Button>
      </DialogActions>
    </Dialog>
  );
}
