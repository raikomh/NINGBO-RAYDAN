import { useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, Chip, CircularProgress, Stack, Grid, Alert,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, FormControlLabel, Checkbox, IconButton, Tooltip,
} from '@mui/material';
import { Add, FactCheck, Visibility } from '@mui/icons-material';
import {
  useInventoryCounts, useCreateCount, useCloseCount, useWarehouses, useOpsProducts,
} from '@/hooks/useOps';
import type { OpsInventoryCount } from '@/lib/opsTypes';

export default function InventoryCountPage() {
  const { data: counts, isLoading } = useInventoryCounts();
  const { data: warehouses } = useWarehouses();
  const [open, setOpen] = useState(false);
  const [detail, setDetail] = useState<OpsInventoryCount | null>(null);

  const whName = (id: string) => warehouses?.find((w) => w.id === id)?.name ?? '—';

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h5" fontWeight={700}>Conteo de inventario</Typography>
        <Button startIcon={<Add />} variant="contained" onClick={() => setOpen(true)}>Nuevo conteo</Button>
      </Box>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Fecha</TableCell><TableCell>Almacén</TableCell><TableCell align="right">Productos</TableCell>
                <TableCell align="right">Discrepancias</TableCell><TableCell>Estado</TableCell><TableCell align="right">Ver</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(counts ?? []).map((c) => {
                const discrepancies = c.items.filter((i) => i.difference !== 0).length;
                return (
                  <TableRow key={c.id} hover>
                    <TableCell>{new Date(c.date).toLocaleDateString()}</TableCell>
                    <TableCell>{whName(c.warehouseId)}</TableCell>
                    <TableCell align="right">{c.items.length}</TableCell>
                    <TableCell align="right"><Chip size="small" color={discrepancies ? 'warning' : 'success'} label={discrepancies} /></TableCell>
                    <TableCell>
                      <Chip size="small" label={c.status === 'Open' ? 'Abierto' : c.adjusted ? 'Cerrado (ajustado)' : 'Cerrado'}
                        color={c.status === 'Open' ? 'info' : 'default'} />
                    </TableCell>
                    <TableCell align="right">
                      <Tooltip title="Ver detalle"><IconButton size="small" onClick={() => setDetail(c)}><Visibility fontSize="small" /></IconButton></Tooltip>
                    </TableCell>
                  </TableRow>
                );
              })}
              {(counts ?? []).length === 0 && <TableRow><TableCell colSpan={6} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin conteos</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {open && <CountDialog onClose={() => setOpen(false)} />}
      {detail && <CountDetailDialog count={detail} onClose={() => setDetail(null)} />}
    </Box>
  );
}

function CountDialog({ onClose }: { onClose: () => void }) {
  const { data: warehouses } = useWarehouses();
  const create = useCreateCount();
  const [warehouseId, setWarehouseId] = useState('');
  const { data: products } = useOpsProducts({ warehouseId: warehouseId || undefined });
  const [counted, setCounted] = useState<Record<string, number>>({});

  const systemStock = (productId: string) => {
    const p = products?.find((x) => x.id === productId);
    return p?.stocks.find((s) => s.warehouseId === warehouseId)?.quantity ?? 0;
  };

  const submit = () => {
    const items = Object.entries(counted).map(([productId, countedQuantity]) => ({ productId, countedQuantity }));
    create.mutate({ warehouseId, items }, { onSuccess: onClose });
  };

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Nuevo conteo físico</DialogTitle>
      <DialogContent>
        <TextField size="small" select fullWidth label="Almacén" value={warehouseId} onChange={(e) => { setWarehouseId(e.target.value); setCounted({}); }} sx={{ mt: 1, mb: 2 }}>
          {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
        </TextField>

        {warehouseId && (
          <TableContainer component={Card} variant="outlined">
            <Table size="small">
              <TableHead><TableRow><TableCell>Producto</TableCell><TableCell align="right">Sistema</TableCell><TableCell align="right">Contado</TableCell></TableRow></TableHead>
              <TableBody>
                {(products ?? []).map((p) => (
                  <TableRow key={p.id}>
                    <TableCell>{p.name}</TableCell>
                    <TableCell align="right">{systemStock(p.id)}</TableCell>
                    <TableCell align="right">
                      <TextField size="small" type="number" placeholder="—" sx={{ width: 90 }}
                        value={counted[p.id] ?? ''} onChange={(e) => {
                          const v = e.target.value;
                          setCounted((c) => {
                            const next = { ...c };
                            if (v === '') delete next[p.id]; else next[p.id] = Number(v);
                            return next;
                          });
                        }} />
                    </TableCell>
                  </TableRow>
                ))}
                {(products ?? []).length === 0 && <TableRow><TableCell colSpan={3} align="center" sx={{ py: 3, color: 'text.secondary' }}>Sin productos en este almacén</TableCell></TableRow>}
              </TableBody>
            </Table>
          </TableContainer>
        )}
        {create.isError && <Alert severity="error" sx={{ mt: 1 }}>No se pudo registrar el conteo.</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" disabled={!warehouseId || Object.keys(counted).length === 0 || create.isPending} onClick={submit}>Registrar conteo</Button>
      </DialogActions>
    </Dialog>
  );
}

function CountDetailDialog({ count, onClose }: { count: OpsInventoryCount; onClose: () => void }) {
  const close = useCloseCount();
  const [apply, setApply] = useState(true);

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Detalle del conteo — {new Date(count.date).toLocaleDateString()}</DialogTitle>
      <DialogContent>
        <TableContainer component={Card} variant="outlined" sx={{ mt: 1 }}>
          <Table size="small">
            <TableHead><TableRow><TableCell>Producto</TableCell><TableCell align="right">Sistema</TableCell><TableCell align="right">Contado</TableCell><TableCell align="right">Diferencia</TableCell></TableRow></TableHead>
            <TableBody>
              {count.items.map((i) => (
                <TableRow key={i.productId}>
                  <TableCell>{i.productName}</TableCell>
                  <TableCell align="right">{i.systemQuantity}</TableCell>
                  <TableCell align="right">{i.countedQuantity}</TableCell>
                  <TableCell align="right">
                    <Chip size="small" color={i.difference === 0 ? 'default' : i.difference > 0 ? 'info' : 'warning'}
                      label={i.difference > 0 ? `+${i.difference}` : i.difference} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>

        {count.status === 'Open' && (
          <Box mt={2}>
            <FormControlLabel control={<Checkbox checked={apply} onChange={(e) => setApply(e.target.checked)} />}
              label="Ajustar el stock del sistema a lo contado al cerrar" />
            {close.isError && <Alert severity="error">No se pudo cerrar el conteo.</Alert>}
          </Box>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cerrar</Button>
        {count.status === 'Open' && (
          <Button variant="contained" startIcon={<FactCheck />} disabled={close.isPending}
            onClick={() => close.mutate({ id: count.id, applyAdjustments: apply }, { onSuccess: onClose })}>
            Cerrar conteo
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}
