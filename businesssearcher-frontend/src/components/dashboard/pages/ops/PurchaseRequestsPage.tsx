import { useState } from 'react';
import {
  Box, Button, Card, Typography, Chip, CircularProgress, IconButton, Tooltip, Stack,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, MenuItem, Alert,
} from '@mui/material';
import { AutoFixHigh, Check, Close, Edit } from '@mui/icons-material';
import { usePurchaseRequests, useGenerateLowStockRequests, usePurchaseRequestAction } from '@/hooks/useOps';
import { useIsOpsAdmin } from '@/hooks/useOpsRole';

const STATUS_COLOR: Record<string, 'warning' | 'success' | 'error' | 'default'> = {
  Pending: 'warning', Approved: 'success', Rejected: 'error', Completed: 'default',
};
const STATUS_LABEL: Record<string, string> = {
  Pending: 'Pendiente', Approved: 'Aprobada', Rejected: 'Rechazada', Completed: 'Finalizada',
};

export default function PurchaseRequestsPage() {
  const [status, setStatus] = useState<string>('');
  const { data: requests, isLoading } = usePurchaseRequests(status || undefined);
  const generate = useGenerateLowStockRequests();
  const action = usePurchaseRequestAction();
  const [editId, setEditId] = useState<string | null>(null);
  const [editQty, setEditQty] = useState(0);
  const isAdmin = useIsOpsAdmin();

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" flexWrap="wrap" gap={2} mb={3}>
        <Typography variant="h5" fontWeight={700}>Solicitudes de compra</Typography>
        <Button startIcon={<AutoFixHigh />} variant="contained" disabled={generate.isPending}
          onClick={() => generate.mutate()}>Generar por bajo stock</Button>
      </Box>

      <Alert severity="info" sx={{ mb: 2 }}>
        Solo el <strong>Administrador</strong> puede ajustar cantidades y aprobar/rechazar. Al registrar la
        compra asociada, la solicitud pasa a <em>Finalizada</em>.
      </Alert>

      <TextField size="small" select label="Estado" value={status} onChange={(e) => setStatus(e.target.value)} sx={{ minWidth: 200, mb: 2 }}>
        <MenuItem value="">Todas</MenuItem>
        {['Pending', 'Approved', 'Rejected', 'Completed'].map((s) => <MenuItem key={s} value={s}>{STATUS_LABEL[s]}</MenuItem>)}
      </TextField>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Producto</TableCell><TableCell align="right">Stock actual</TableCell>
                <TableCell align="right">Mínimo</TableCell><TableCell align="right">Solicitado</TableCell>
                <TableCell>Estado</TableCell><TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(requests ?? []).map((r) => (
                <TableRow key={r.id} hover>
                  <TableCell><Typography variant="body2" fontWeight={600}>{r.productName}</Typography></TableCell>
                  <TableCell align="right">{r.currentStock}</TableCell>
                  <TableCell align="right">{r.minStock}</TableCell>
                  <TableCell align="right">
                    {editId === r.id ? (
                      <TextField size="small" type="number" value={editQty || ''} onChange={(e) => setEditQty(e.target.value === '' ? 0 : Number(e.target.value))} sx={{ width: 90 }} />
                    ) : r.requestedQuantity}
                  </TableCell>
                  <TableCell><Chip size="small" color={STATUS_COLOR[r.status]} label={STATUS_LABEL[r.status] ?? r.status} /></TableCell>
                  <TableCell align="right">
                    {r.status === 'Pending' && isAdmin && (
                      <Stack direction="row" spacing={0.5} justifyContent="flex-end">
                        {editId === r.id ? (
                          <>
                            <Button size="small" onClick={() => { action.mutate({ id: r.id, action: 'quantity', requestedQuantity: editQty }); setEditId(null); }}>Guardar</Button>
                            <Button size="small" onClick={() => setEditId(null)}>Cancelar</Button>
                          </>
                        ) : (
                          <>
                            <Tooltip title="Editar cantidad"><IconButton size="small" onClick={() => { setEditId(r.id); setEditQty(r.requestedQuantity); }}><Edit fontSize="small" /></IconButton></Tooltip>
                            <Tooltip title="Aprobar"><IconButton size="small" color="success" onClick={() => action.mutate({ id: r.id, action: 'approve' })}><Check fontSize="small" /></IconButton></Tooltip>
                            <Tooltip title="Rechazar"><IconButton size="small" color="error" onClick={() => action.mutate({ id: r.id, action: 'reject' })}><Close fontSize="small" /></IconButton></Tooltip>
                          </>
                        )}
                      </Stack>
                    )}
                    {r.status === 'Pending' && !isAdmin && (
                      <Typography variant="caption" color="text.disabled">Solo Administrador</Typography>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {(requests ?? []).length === 0 && <TableRow><TableCell colSpan={6} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin solicitudes</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
