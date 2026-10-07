import { useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, Chip, CircularProgress,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, Grid, Stack, Alert,
} from '@mui/material';
import { Add, Payments } from '@mui/icons-material';
import { useExpenses, useCreateExpense } from '@/hooks/useOps';
import type { CreateOpsExpense, ExpenseType } from '@/lib/opsTypes';

const TYPE_LABEL: Record<string, string> = {
  Rent: 'Alquiler', Salary: 'Salario', Utilities: 'Servicios',
  Marketing: 'Marketing', Other: 'Otro', CashOut: 'Retiro de caja',
};
const TYPE_COLOR: Record<string, 'primary' | 'secondary' | 'info' | 'warning' | 'error' | 'default'> = {
  Rent: 'primary', Salary: 'secondary', Utilities: 'info',
  Marketing: 'warning', Other: 'default', CashOut: 'error',
};
const TYPES: ExpenseType[] = ['Rent', 'Salary', 'Utilities', 'Marketing', 'Other', 'CashOut'];

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
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        <Stack direction="row" spacing={1} alignItems="center">
          <Payments fontSize="small" />
          <span>Registrar gasto</span>
        </Stack>
      </DialogTitle>
      <DialogContent>
        <Grid container spacing={2} sx={{ mt: 0 }}>
          <Grid item xs={12} sm={6}>
            <TextField fullWidth size="small" select label="Tipo" value={form.type}
              onChange={(e) => setForm((f) => ({ ...f, type: e.target.value as ExpenseType }))}>
              {TYPES.map((t) => <MenuItem key={t} value={t}>{TYPE_LABEL[t]}</MenuItem>)}
            </TextField>
          </Grid>
          <Grid item xs={6} sm={3}><TextField fullWidth size="small" type="number" label="Monto CUP" value={form.amount} onChange={(e) => setForm((f) => ({ ...f, amount: Number(e.target.value) }))} /></Grid>
          <Grid item xs={6} sm={3}><TextField fullWidth size="small" type="number" label="Monto USD" value={form.amountUSD ?? ''} onChange={(e) => setForm((f) => ({ ...f, amountUSD: e.target.value === '' ? undefined : Number(e.target.value) }))} /></Grid>
          <Grid item xs={12}><TextField fullWidth size="small" label="Descripción" value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} /></Grid>
        </Grid>
        {create.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo registrar el gasto.</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" onClick={submit} disabled={!form.description || form.amount <= 0 || create.isPending}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}
