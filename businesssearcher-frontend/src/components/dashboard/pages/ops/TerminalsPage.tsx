import { useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, IconButton, Chip, CircularProgress,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, Grid, Tooltip, Stack, Alert, Switch, FormControlLabel,
} from '@mui/material';
import { Add, Edit, PointOfSale, Block, CheckCircle } from '@mui/icons-material';
import { useTerminals, useSaveTerminal, useWarehouses } from '@/hooks/useOps';
import type { OpsTerminal } from '@/lib/opsTypes';

export default function TerminalsPage() {
  const { data: terminals, isLoading } = useTerminals();
  const { data: warehouses } = useWarehouses();
  const [dialog, setDialog] = useState(false);
  const [edit, setEdit] = useState<OpsTerminal | null>(null);

  const whName = (id?: string) => warehouses?.find((w) => w.id === id)?.name ?? '—';

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" flexWrap="wrap" gap={2} mb={3}>
        <Typography variant="h5" fontWeight={700}>Terminales</Typography>
        <Button startIcon={<Add />} variant="contained"
          onClick={() => { setEdit(null); setDialog(true); }}>Nueva terminal</Button>
      </Box>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Terminal</TableCell>
                <TableCell>Almacén</TableCell>
                <TableCell>Estado</TableCell>
                <TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(terminals ?? []).map((t) => (
                <TableRow key={t.id} hover>
                  <TableCell>
                    <Box display="flex" alignItems="center" gap={1}>
                      <PointOfSale fontSize="small" color="action" />
                      <Box>
                        <Typography variant="body2" fontWeight={600}>{t.name}</Typography>
                        {t.description && <Typography variant="caption" color="text.secondary" display="block">{t.description}</Typography>}
                      </Box>
                    </Box>
                  </TableCell>
                  <TableCell>{whName(t.warehouseId)}</TableCell>
                  <TableCell>
                    <Chip size="small" label={t.isActive ? 'Activa' : 'Inactiva'}
                      color={t.isActive ? 'success' : 'default'} variant="outlined" />
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title="Editar"><IconButton size="small" onClick={() => { setEdit(t); setDialog(true); }}><Edit fontSize="small" /></IconButton></Tooltip>
                  </TableCell>
                </TableRow>
              ))}
              {(terminals ?? []).length === 0 && (
                <TableRow><TableCell colSpan={4} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin terminales registradas</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {dialog && <TerminalDialog terminal={edit} onClose={() => setDialog(false)} />}
    </Box>
  );
}

function TerminalDialog({ terminal, onClose }: { terminal: OpsTerminal | null; onClose: () => void }) {
  const save = useSaveTerminal();
  const { data: warehouses } = useWarehouses();
  const [name, setName] = useState(terminal?.name ?? '');
  const [description, setDescription] = useState(terminal?.description ?? '');
  const [warehouseId, setWarehouseId] = useState(terminal?.warehouseId ?? '');
  const [isActive, setIsActive] = useState(terminal?.isActive ?? true);

  const submit = () => {
    save.mutate({
      id: terminal?.id,
      name,
      description: description || undefined,
      warehouseId: warehouseId || undefined,
      isActive,
    }, { onSuccess: onClose });
  };

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        <Stack direction="row" spacing={1} alignItems="center">
          <PointOfSale fontSize="small" />
          <span>{terminal ? 'Editar terminal' : 'Nueva terminal'}</span>
        </Stack>
      </DialogTitle>
      <DialogContent>
        <Grid container spacing={2} sx={{ mt: 0 }}>
          <Grid item xs={12}><TextField fullWidth size="small" label="Nombre" value={name} onChange={(e) => setName(e.target.value)} /></Grid>
          <Grid item xs={12}><TextField fullWidth size="small" label="Descripción (opcional)" value={description} onChange={(e) => setDescription(e.target.value)} /></Grid>
          <Grid item xs={12}>
            <TextField fullWidth size="small" select label="Almacén (opcional)" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
              <MenuItem value="">— Sin asignar —</MenuItem>
              {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
            </TextField>
          </Grid>
          {terminal && (
            <Grid item xs={12}>
              <FormControlLabel
                control={<Switch checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />}
                label={isActive ? 'Terminal activa' : 'Terminal inactiva'}
              />
            </Grid>
          )}
        </Grid>
        {save.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo guardar la terminal.</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" startIcon={terminal && !isActive ? <Block /> : <CheckCircle />}
          onClick={submit} disabled={!name || save.isPending}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}
