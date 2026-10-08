import { useMemo, useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, Chip, CircularProgress, Checkbox, IconButton, Tooltip,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, Grid, Stack, Alert, ToggleButton, ToggleButtonGroup,
} from '@mui/material';
import { Add, Payments, Visibility } from '@mui/icons-material';
import { useExpenses, useCreateExpense, usePayrollPreview, useRegisterPayrollExpense, useOpsSales } from '@/hooks/useOps';
import type { CreateOpsExpense, ExpenseType, OpsRole } from '@/lib/opsTypes';

const firstDayOfMonth = () => { const d = new Date(); d.setDate(1); return d.toISOString().slice(0, 10); };
const today = () => new Date().toISOString().slice(0, 10);

const ROLE_OPTIONS: OpsRole[] = ['Administrador', 'Cajero', 'JefeDeTurno', 'Almacenero', 'Comercial', 'Auditor'];
const ROLE_LABEL: Record<OpsRole, string> = {
  Administrador: 'Administrador', Cajero: 'Cajero', JefeDeTurno: 'Jefe de Turno',
  Almacenero: 'Almacenero', Comercial: 'Comercial', Auditor: 'Auditor', Observador: 'Observador',
};

const TYPE_LABEL: Record<string, string> = {
  Rent: 'Alquiler', Salary: 'Salario', Utilities: 'Servicios',
  Marketing: 'Marketing', Other: 'Otro', CashOut: 'Retiro de caja',
};
const TYPE_COLOR: Record<string, 'primary' | 'secondary' | 'info' | 'warning' | 'error' | 'default'> = {
  Rent: 'primary', Salary: 'secondary', Utilities: 'info',
  Marketing: 'warning', Other: 'default', CashOut: 'error',
};
const TYPES: ExpenseType[] = ['Rent', 'Salary', 'Utilities', 'Marketing', 'Other', 'CashOut'];
// "Salario" no se ofrece aquí: ese tipo de gasto se registra desde la pestaña "Gasto de salario"
// (calculado a partir de los roles), no como un tipo suelto en el formulario general.
const GENERAL_TYPES: ExpenseType[] = TYPES.filter((t) => t !== 'Salary');

export default function ExpensesPage() {
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const { data: expenses, isLoading } = useExpenses({ from: from || undefined, to: to || undefined });
  const [dialog, setDialog] = useState(false);

  const total = (expenses ?? []).reduce((sum, e) => sum + e.amount, 0);

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" flexWrap="wrap" gap={2} mb={3}>
        <Typography variant="h5" fontWeight={700}>Gastos</Typography>
        <Button startIcon={<Add />} variant="contained" onClick={() => setDialog(true)}>Registrar gasto</Button>
      </Box>

      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
          <TextField size="small" type="date" label="Desde" InputLabelProps={{ shrink: true }}
            value={from} onChange={(e) => setFrom(e.target.value)} />
          <TextField size="small" type="date" label="Hasta" InputLabelProps={{ shrink: true }}
            value={to} onChange={(e) => setTo(e.target.value)} />
          <Box flexGrow={1} />
          <Chip color="primary" label={`Total: ${total.toFixed(2)} CUP`} />
        </CardContent>
      </Card>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Fecha</TableCell>
                <TableCell>Tipo</TableCell>
                <TableCell>Descripción</TableCell>
                <TableCell align="right">Monto</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(expenses ?? []).map((e) => (
                <TableRow key={e.id} hover>
                  <TableCell>{new Date(e.date).toLocaleDateString()}</TableCell>
                  <TableCell><Chip size="small" color={TYPE_COLOR[e.type] ?? 'default'} label={TYPE_LABEL[e.type] ?? e.type} /></TableCell>
                  <TableCell>{e.description}</TableCell>
                  <TableCell align="right">
                    {e.amount.toFixed(2)}
                    {e.amountUSD != null && (
                      <Typography variant="caption" color="text.secondary" display="block">${e.amountUSD.toFixed(2)}</Typography>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {(expenses ?? []).length === 0 && (
                <TableRow><TableCell colSpan={4} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin gastos</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {dialog && <ExpenseDialog onClose={() => setDialog(false)} />}
    </Box>
  );
}

function ExpenseDialog({ onClose }: { onClose: () => void }) {
  const [mode, setMode] = useState<'general' | 'payroll'>('general');

  return (
    <Dialog open onClose={onClose} maxWidth={mode === 'payroll' ? 'md' : 'sm'} fullWidth>
      <DialogTitle>
        <Stack direction="row" spacing={1} alignItems="center">
          <Payments fontSize="small" />
          <span>Registrar gasto</span>
        </Stack>
      </DialogTitle>
      <DialogContent>
        <ToggleButtonGroup exclusive fullWidth size="small" value={mode} onChange={(_, v) => v && setMode(v)} sx={{ mt: 1, mb: 2 }}>
          <ToggleButton value="general">Gasto distinto al salario</ToggleButton>
          <ToggleButton value="payroll">Gasto de salario</ToggleButton>
        </ToggleButtonGroup>
      </DialogContent>
      {mode === 'general' ? <GeneralExpenseForm onClose={onClose} /> : <PayrollExpenseForm onClose={onClose} />}
    </Dialog>
  );
}

function GeneralExpenseForm({ onClose }: { onClose: () => void }) {
  const create = useCreateExpense();
  const [form, setForm] = useState<CreateOpsExpense>({
    type: 'Other',
    amount: 0,
    description: '',
    amountUSD: undefined,
  });

  const submit = () => {
    create.mutate({
      ...form,
      amountUSD: form.amountUSD || undefined,
    }, { onSuccess: onClose });
  };

  return (
    <>
      <DialogContent sx={{ pt: 0 }}>
        <Grid container spacing={2}>
          <Grid item xs={12} sm={6}>
            <TextField fullWidth size="small" select label="Tipo" value={form.type}
              onChange={(e) => setForm((f) => ({ ...f, type: e.target.value as ExpenseType }))}>
              {GENERAL_TYPES.map((t) => <MenuItem key={t} value={t}>{TYPE_LABEL[t]}</MenuItem>)}
            </TextField>
          </Grid>
          <Grid item xs={6} sm={3}><TextField fullWidth size="small" type="number" label="Monto CUP" value={form.amount || ''} onChange={(e) => setForm((f) => ({ ...f, amount: e.target.value === '' ? 0 : Number(e.target.value) }))} /></Grid>
          <Grid item xs={6} sm={3}><TextField fullWidth size="small" type="number" label="Monto USD" value={form.amountUSD ?? ''} onChange={(e) => setForm((f) => ({ ...f, amountUSD: e.target.value === '' ? undefined : Number(e.target.value) }))} /></Grid>
          <Grid item xs={12}><TextField fullWidth size="small" label="Descripción" value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} /></Grid>
        </Grid>
        {create.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo registrar el gasto.</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" onClick={submit} disabled={!form.description || form.amount <= 0 || create.isPending}>Guardar</Button>
      </DialogActions>
    </>
  );
}

// Gasto de nómina: salario fijo + % de venta (por encima del mínimo exento) de cada trabajador,
// calculado en el servidor a partir de los roles configurados en Configuración.
function PayrollExpenseForm({ onClose }: { onClose: () => void }) {
  const [from, setFrom] = useState(firstDayOfMonth());
  const [to, setTo] = useState(today());
  const { data: preview, isLoading, isError } = usePayrollPreview(from, to);
  const register = useRegisterPayrollExpense();

  const [excluded, setExcluded] = useState<Set<string>>(new Set());
  const [description, setDescription] = useState('');
  const [amountUSD, setAmountUSD] = useState<number | ''>('');
  const [roleFilter, setRoleFilter] = useState<OpsRole | ''>('');
  const [detailWorker, setDetailWorker] = useState<{ id: string; name: string } | null>(null);

  const toggleWorker = (id: string) =>
    setExcluded((ex) => { const n = new Set(ex); if (n.has(id)) n.delete(id); else n.add(id); return n; });

  // El filtro de rol solo acota qué filas se muestran; la selección (checkbox) sigue
  // aplicando sobre todos los trabajadores, se vean o no en este momento.
  const visibleWorkers = useMemo(
    () => (preview?.workers ?? []).filter((w) => !roleFilter || w.role === roleFilter),
    [preview, roleFilter],
  );
  const selectedWorkers = useMemo(
    () => (preview?.workers ?? []).filter((w) => !excluded.has(w.workerId)),
    [preview, excluded],
  );
  const totalCup = selectedWorkers.reduce((sum, w) => sum + w.salarioACobrar, 0);

  const submit = () => {
    register.mutate({
      from, to,
      workerIds: selectedWorkers.map((w) => w.workerId),
      description: description || undefined,
      amountUSD: amountUSD === '' ? undefined : amountUSD,
    }, { onSuccess: onClose });
  };

  return (
    <>
      <DialogContent sx={{ pt: 0 }}>
        <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap" sx={{ mb: 2 }}>
          <TextField size="small" select label="Rol / trabajador" value={roleFilter}
            onChange={(e) => setRoleFilter(e.target.value as OpsRole | '')} sx={{ minWidth: 200 }}>
            <MenuItem value="">Todos los roles</MenuItem>
            {ROLE_OPTIONS.map((r) => <MenuItem key={r} value={r}>{ROLE_LABEL[r]}</MenuItem>)}
          </TextField>
          {roleFilter && (
            <Typography variant="caption" color="text.secondary">
              {visibleWorkers.length === 0
                ? 'Sin trabajador activo con este rol.'
                : `Usuario(s) asociado(s): ${visibleWorkers.map((w) => w.workerName).join(', ')}`}
            </Typography>
          )}
        </Stack>
        <Grid container spacing={2} sx={{ mb: 2 }}>
          <Grid item xs={6} sm={3}>
            <TextField fullWidth size="small" type="date" label="Desde" InputLabelProps={{ shrink: true }}
              value={from} onChange={(e) => setFrom(e.target.value)} />
          </Grid>
          <Grid item xs={6} sm={3}>
            <TextField fullWidth size="small" type="date" label="Hasta" InputLabelProps={{ shrink: true }}
              value={to} onChange={(e) => setTo(e.target.value)} />
          </Grid>
          <Grid item xs={6} sm={3}>
            <TextField fullWidth size="small" disabled label="Importe CUP" value={totalCup.toFixed(2)} />
          </Grid>
          <Grid item xs={6} sm={3}>
            <TextField fullWidth size="small" type="number" label="Importe USD (opcional)" value={amountUSD}
              onChange={(e) => setAmountUSD(e.target.value === '' ? '' : Number(e.target.value))} />
          </Grid>
          <Grid item xs={12}>
            <TextField fullWidth size="small" label="Descripción (opcional)" placeholder="Sumatoria de todos los salarios a cobrar"
              value={description} onChange={(e) => setDescription(e.target.value)} />
          </Grid>
        </Grid>

        {preview && (
          <Typography variant="caption" color="text.secondary" display="block" mb={1}>
            Mínimo exento vigente: {preview.minimoExento.toFixed(2)} CUP (configurable en Configuración).
          </Typography>
        )}

        {isError && <Alert severity="warning" sx={{ mb: 2 }}>No se pudo calcular la nómina para el periodo seleccionado.</Alert>}

        {isLoading ? (
          <Box display="flex" justifyContent="center" py={4}><CircularProgress /></Box>
        ) : (
          <TableContainer component={Card} variant="outlined">
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell padding="checkbox" />
                  <TableCell>Trabajador</TableCell>
                  <TableCell>Rol</TableCell>
                  <TableCell align="right">Salario básico</TableCell>
                  <TableCell align="right">Mínimo exento</TableCell>
                  <TableCell align="right">Ventas</TableCell>
                  <TableCell align="right">% de venta (comisión)</TableCell>
                  <TableCell align="right">Salario a cobrar</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {visibleWorkers.map((w) => (
                  <TableRow key={w.workerId} hover selected={!excluded.has(w.workerId)}>
                    <TableCell padding="checkbox">
                      <Checkbox size="small" checked={!excluded.has(w.workerId)} onChange={() => toggleWorker(w.workerId)} />
                    </TableCell>
                    <TableCell>{w.workerName}</TableCell>
                    <TableCell>{ROLE_LABEL[w.role] ?? w.role}</TableCell>
                    <TableCell align="right">{w.baseSalary.toFixed(2)}</TableCell>
                    <TableCell align="right">{w.minimoExento.toFixed(2)}</TableCell>
                    <TableCell align="right">
                      <Stack direction="row" spacing={0.5} justifyContent="flex-end" alignItems="center">
                        {w.ventas.toFixed(2)}
                        <Tooltip title="Ver ventas y productos">
                          <IconButton size="small" onClick={() => setDetailWorker({ id: w.workerId, name: w.workerName })}>
                            <Visibility fontSize="inherit" />
                          </IconButton>
                        </Tooltip>
                      </Stack>
                    </TableCell>
                    <TableCell align="right">
                      <Typography variant="body2">{w.comision.toFixed(2)}</Typography>
                      <Typography variant="caption" color="text.secondary">{w.salesPercentage}%</Typography>
                    </TableCell>
                    <TableCell align="right"><strong>{w.salarioACobrar.toFixed(2)}</strong></TableCell>
                  </TableRow>
                ))}
                {visibleWorkers.length === 0 && (
                  <TableRow><TableCell colSpan={8} align="center" sx={{ py: 3, color: 'text.secondary' }}>
                    {roleFilter ? 'Sin trabajadores activos con este rol' : 'Sin trabajadores activos'}
                  </TableCell></TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        )}
        {register.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo registrar el gasto de nómina.</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" onClick={submit} disabled={selectedWorkers.length === 0 || totalCup <= 0 || register.isPending}>
          Registrar
        </Button>
      </DialogActions>

      {detailWorker && (
        <WorkerSalesDialog worker={detailWorker} from={from} to={to} onClose={() => setDetailWorker(null)} />
      )}
    </>
  );
}

const SALE_STATUS_LABEL: Record<string, string> = { Completed: 'Completada', Refunded: 'Reembolsada' };

// Detalle de las ventas (y productos) de un trabajador en el periodo de la nómina: responde
// "de cuánto es la venta y qué productos" sin tener que ir a buscarlo a otra pantalla.
function WorkerSalesDialog({ worker, from, to, onClose }: {
  worker: { id: string; name: string }; from: string; to: string; onClose: () => void;
}) {
  const { data: sales, isLoading } = useOpsSales({ from, to, cashierId: worker.id });

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Ventas de {worker.name} ({from} a {to})</DialogTitle>
      <DialogContent>
        <Typography variant="caption" color="text.secondary" display="block" mb={2}>
          Solo las ventas "Completada" cuentan para el cálculo de la nómina; las reembolsadas se muestran
          aquí solo como referencia.
        </Typography>

        {isLoading ? (
          <Box display="flex" justifyContent="center" py={4}><CircularProgress /></Box>
        ) : (sales ?? []).length === 0 ? (
          <Typography color="text.secondary" align="center" sx={{ py: 3 }}>Sin ventas en este periodo.</Typography>
        ) : (
          <Stack spacing={1.5}>
            {(sales ?? []).map((s) => (
              <Card key={s.id} variant="outlined">
                <CardContent sx={{ py: 1.5 }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center" mb={0.5} flexWrap="wrap" gap={0.5}>
                    <Typography variant="body2" color="text.secondary">{new Date(s.date).toLocaleString()}</Typography>
                    <Stack direction="row" spacing={1} alignItems="center">
                      <Chip size="small" color={s.status === 'Refunded' ? 'error' : 'success'}
                        label={SALE_STATUS_LABEL[s.status] ?? s.status} />
                      <Typography variant="subtitle2" fontWeight={700}>{s.total.toFixed(2)} CUP</Typography>
                    </Stack>
                  </Stack>
                  {s.items.map((it, idx) => (
                    <Typography key={idx} variant="body2" color="text.secondary">
                      {it.quantity} × {it.productName} ({it.unitPrice.toFixed(2)} c/u) = {(it.quantity * it.unitPrice - it.lineDiscount).toFixed(2)}
                    </Typography>
                  ))}
                </CardContent>
              </Card>
            ))}
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cerrar</Button>
      </DialogActions>
    </Dialog>
  );
}
