import { useState } from 'react';
import {
  Box, Card, CardContent, Typography, Button, TextField, MenuItem, Stack, Grid, Divider,
  Chip, Table, TableBody, TableCell, TableHead, TableRow, TableContainer, Alert,
  Dialog, DialogTitle, DialogContent, DialogActions, ToggleButtonGroup, ToggleButton, CircularProgress,
} from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { LockOpen, Lock, TrendingUp, TrendingDown, FactCheck } from '@mui/icons-material';
import {
  useCurrentCashRegister, useOpenRegister, useCloseRegister, useAddCashMovement,
  useCashMovements, useWarehouses, useOpsSales,
} from '@/hooks/useOps';

export default function CashRegisterPage() {
  const { data: register, isLoading } = useCurrentCashRegister();
  const { data: warehouses } = useWarehouses();
  const { data: movements } = useCashMovements(register?.id);
  const { data: sales } = useOpsSales(register ? { registerId: register.id } : undefined);

  const open = useOpenRegister();
  const close = useCloseRegister();
  const addMov = useAddCashMovement();

  const [initial, setInitial] = useState(0);
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
            <Typography variant="body2" color="text.secondary" mb={2}>Registra el monto inicial en efectivo para comenzar el turno.</Typography>
            <Stack spacing={2}>
              <TextField size="small" type="number" label="Monto inicial (CUP)" value={initial} onChange={(e) => setInitial(Number(e.target.value))} />
              <TextField size="small" select label="Almacén (opcional)" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
                <MenuItem value="">Sin asignar</MenuItem>
                {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
              </TextField>
              {open.isError && <Alert severity="error">No se pudo abrir la caja.</Alert>}
              <Button variant="contained" startIcon={<LockOpen />} disabled={open.isPending}
                onClick={() => open.mutate({ initialAmount: initial, warehouseId: warehouseId || undefined })}>Abrir caja</Button>
            </Stack>
          </CardContent>
        </Card>
      ) : (
        <>
          <Grid container spacing={2} mb={2}>
            <Metric label="Monto inicial" value={register.initialAmount} />
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
          onClose={() => setCloseDialog(false)}
          onConfirm={(actual) => close.mutate({ id: register.id, actualAmount: actual }, { onSuccess: () => setCloseDialog(false) })} />
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

function Metric({ label, value, highlight }: { label: string; value: number; highlight?: boolean }) {
  return (
    <Grid item xs={6} sm={4} md={2.4}>
      <Card variant="outlined" sx={{ bgcolor: highlight ? 'primary.main' : undefined, color: highlight ? 'primary.contrastText' : undefined }}>
        <CardContent sx={{ py: 1.5 }}>
          <Typography variant="caption" sx={{ opacity: 0.85 }}>{label}</Typography>
          <Typography variant="h6" fontWeight={800}>{value.toFixed(2)}</Typography>
        </CardContent>
      </Card>
    </Grid>
  );
}

function CloseDialog({ expected, onClose, onConfirm, pending, error, hasWarehouse, inventoryCountCompleted }: {
  expected: number; onClose: () => void; onConfirm: (actual: number) => void; pending: boolean; error: boolean;
  hasWarehouse: boolean; inventoryCountCompleted: boolean;
}) {
  const [actual, setActual] = useState(expected);
  const diff = actual - expected;
  const navigate = useNavigate();
  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Cerrar caja</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {hasWarehouse && !inventoryCountCompleted && (
            <Alert severity="warning" icon={<FactCheck fontSize="small" />}
              action={<Button size="small" onClick={() => navigate('/dashboard/ops/counts')}>Hacer conteo</Button>}>
              No se ha registrado un conteo físico de inventario del almacén en este turno.
            </Alert>
          )}
          <Box display="flex" justifyContent="space-between"><Typography>Esperado</Typography><Typography fontWeight={700}>{expected.toFixed(2)}</Typography></Box>
          <TextField size="small" type="number" label="Efectivo real contado" value={actual} onChange={(e) => setActual(Number(e.target.value))} />
          <Alert severity={diff === 0 ? 'success' : Math.abs(diff) < 0.01 ? 'success' : diff > 0 ? 'info' : 'warning'}>
            Diferencia: {diff.toFixed(2)} {diff > 0 ? '(sobrante)' : diff < 0 ? '(faltante)' : ''}
          </Alert>
          {error && <Alert severity="error">No se pudo cerrar la caja.</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" color="error" disabled={pending} onClick={() => onConfirm(actual)}>Confirmar cierre</Button>
      </DialogActions>
    </Dialog>
  );
}

function MovementDialog({ onClose, onConfirm, pending }: {
  onClose: () => void; onConfirm: (type: string, amount: number, description: string) => void; pending: boolean;
}) {
  const [type, setType] = useState('In');
  const [amount, setAmount] = useState(0);
  const [description, setDescription] = useState('');
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
          <TextField size="small" type="number" label="Monto" value={amount} onChange={(e) => setAmount(Number(e.target.value))} />
          <TextField size="small" label="Descripción" value={description} onChange={(e) => setDescription(e.target.value)} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" disabled={pending || amount <= 0} onClick={() => onConfirm(type, amount, description)}>Registrar</Button>
      </DialogActions>
    </Dialog>
  );
}
