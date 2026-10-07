import { useEffect, useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, CircularProgress, Alert, IconButton, MenuItem,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Stack, Chip, Grid,
  Dialog, DialogTitle, DialogContent, DialogActions, FormControlLabel, Switch, Divider,
} from '@mui/material';
import {
  CurrencyExchange, Storefront, PointOfSale, Add, Edit,
  PowerSettingsNew, Block, LocationOn, AccessTime, Settings as SettingsIcon,
} from '@mui/icons-material';
import {
  useExchangeRate, useExchangeRateHistory, useSetExchangeRate, useBusinessInfo, useSaveBusinessInfo,
  useTerminals, useSaveTerminal, useWarehouses, useSettings, useSaveSetting,
} from '@/hooks/useOps';
import { useStore, useActivateStore, useDeactivateStore } from '@/hooks/useStores';
import StoreDialog from '@/components/dashboard/dialogs/StoreDialog';
import ScheduleTab from '@/components/dashboard/pages/ScheduleTab';
import StoreMapTab from '@/components/dashboard/pages/StoreMapTab';
import type { SaveOpsBusinessInfo, OpsTerminal } from '@/lib/opsTypes';

export default function SettingsPage() {
  const { data: current, isLoading } = useExchangeRate();
  const { data: history } = useExchangeRateHistory(30);
  const setRate = useSetExchangeRate();
  const [rate, setRateValue] = useState('');

  const submit = () => {
    const value = Number(rate);
    if (!value || value <= 0) return;
    setRate.mutate(value, { onSuccess: () => setRateValue('') });
  };

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" flexWrap="wrap" gap={2} mb={3}>
        <Typography variant="h5" fontWeight={700}>Configuración</Typography>
      </Box>

      <BusinessInfoCard />
      <StoreProfileCard />
      <TerminalsCard />
      <SettingsKeyValueCard />

      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent>
          <Stack direction="row" spacing={1} alignItems="center" mb={2}>
            <CurrencyExchange fontSize="small" color="primary" />
            <Typography variant="h6" fontWeight={700}>Tasa de cambio (USD → CUP)</Typography>
          </Stack>

          {isLoading ? (
            <Box display="flex" justifyContent="center" py={3}><CircularProgress /></Box>
          ) : current ? (
            <Stack direction="row" spacing={1} alignItems="center" mb={2}>
              <Chip color="primary" label={`1 USD = ${current.rate.toFixed(2)} CUP`} />
              <Typography variant="caption" color="text.secondary">
                Actualizada el {new Date(current.date).toLocaleString()}
              </Typography>
            </Stack>
          ) : (
            <Alert severity="info" sx={{ mb: 2 }}>Aún no se ha fijado ninguna tasa de cambio.</Alert>
          )}

          <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap">
            <TextField size="small" type="number" label="Nueva tasa (CUP por USD)" value={rate}
              onChange={(e) => setRateValue(e.target.value)} sx={{ minWidth: 220 }} />
            <Button variant="contained" onClick={submit}
              disabled={!rate || Number(rate) <= 0 || setRate.isPending}>Fijar tasa</Button>
          </Stack>
          {setRate.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo fijar la tasa. Solo el Administrador puede hacerlo.</Alert>}
        </CardContent>
      </Card>

      <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 1 }}>Historial</Typography>
      <TableContainer component={Card} variant="outlined">
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Fecha</TableCell>
              <TableCell align="right">Tasa (CUP por USD)</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {(history ?? []).map((h) => (
              <TableRow key={h.id} hover>
                <TableCell>{new Date(h.date).toLocaleString()}</TableCell>
                <TableCell align="right">{h.rate.toFixed(2)}</TableCell>
              </TableRow>
            ))}
            {(history ?? []).length === 0 && (
              <TableRow><TableCell colSpan={2} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin historial</TableCell></TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
}

function BusinessInfoCard() {
  const { data: info, isLoading } = useBusinessInfo();
  const save = useSaveBusinessInfo();
  const [form, setForm] = useState<SaveOpsBusinessInfo>({ name: '' });

  useEffect(() => {
    if (info) setForm({ name: info.name, address: info.address, phone: info.phone, email: info.email, taxId: info.taxId, logoUrl: info.logoUrl });
  }, [info]);

  const set = (k: keyof SaveOpsBusinessInfo, v: string) => setForm((f) => ({ ...f, [k]: v }));

  return (
    <Card variant="outlined" sx={{ mb: 2 }}>
      <CardContent>
        <Stack direction="row" spacing={1} alignItems="center" mb={2}>
          <Storefront fontSize="small" color="primary" />
          <Typography variant="h6" fontWeight={700}>Datos fiscales</Typography>
        </Stack>

        {isLoading ? (
          <Box display="flex" justifyContent="center" py={3}><CircularProgress /></Box>
        ) : (
          <Grid container spacing={2}>
            <Grid item xs={12} sm={6}><TextField fullWidth size="small" label="Nombre del negocio" value={form.name} onChange={(e) => set('name', e.target.value)} /></Grid>
            <Grid item xs={12} sm={6}><TextField fullWidth size="small" label="NIT / Identificación fiscal" value={form.taxId ?? ''} onChange={(e) => set('taxId', e.target.value)} /></Grid>
            <Grid item xs={12} sm={6}><TextField fullWidth size="small" label="Dirección fiscal" value={form.address ?? ''} onChange={(e) => set('address', e.target.value)} /></Grid>
            <Grid item xs={6} sm={3}><TextField fullWidth size="small" label="Teléfono" value={form.phone ?? ''} onChange={(e) => set('phone', e.target.value)} /></Grid>
            <Grid item xs={6} sm={3}><TextField fullWidth size="small" label="Email" value={form.email ?? ''} onChange={(e) => set('email', e.target.value)} /></Grid>
          </Grid>
        )}
        {save.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudieron guardar los datos del negocio.</Alert>}
        <Box mt={2}>
          <Button variant="contained" disabled={!form.name || save.isPending} onClick={() => save.mutate(form)}>Guardar</Button>
        </Box>
      </CardContent>
    </Card>
  );
}

function StoreProfileCard() {
  const { data: store, isLoading } = useStore();
  const activateMutation = useActivateStore();
  const deactivateMutation = useDeactivateStore();
  const [editOpen, setEditOpen] = useState(false);
  const [statusMsg, setStatusMsg] = useState('');

  if (isLoading) {
    return (
      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent><Box display="flex" justifyContent="center" py={3}><CircularProgress /></Box></CardContent>
      </Card>
    );
  }

  const addressStr = store
    ? [store.address?.street, store.address?.city, store.address?.state, store.address?.country].filter(Boolean).join(', ')
    : '';

  return (
    <>
      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent>
          <Box display="flex" justifyContent="space-between" alignItems="center" mb={2}>
            <Stack direction="row" spacing={1} alignItems="center">
              <LocationOn fontSize="small" color="primary" />
              <Typography variant="h6" fontWeight={700}>Ubicación y horario</Typography>
            </Stack>
            <Button size="small" startIcon={<Edit />} onClick={() => setEditOpen(true)}>
              {store ? 'Editar' : 'Configurar tienda'}
            </Button>
          </Box>

          {!store ? (
            <Alert severity="info">
              Configura la dirección de tu negocio para que aparezca en el mapa y en la búsqueda pública.
            </Alert>
          ) : (
            <>
              <Grid container spacing={2} mb={2}>
                <Grid item xs={12} sm={6}>
                  <Typography variant="caption" color="text.secondary" display="block">Dirección</Typography>
                  <Typography variant="body2" fontWeight={500}>{addressStr || '—'}</Typography>
                </Grid>
                <Grid item xs={6} sm={3}>
                  <Typography variant="caption" color="text.secondary" display="block">Teléfono</Typography>
                  <Typography variant="body2" fontWeight={500}>{store.phone || '—'}</Typography>
                </Grid>
                <Grid item xs={6} sm={3}>
                  <Typography variant="caption" color="text.secondary" display="block">Estado</Typography>
                  <Chip size="small" label={store.isOpen ? 'Abierta' : 'Cerrada'} color={store.isOpen ? 'success' : 'default'} />
                </Grid>
              </Grid>

              {statusMsg && <Alert severity="info" sx={{ mb: 2 }} onClose={() => setStatusMsg('')}>{statusMsg}</Alert>}
              <Stack direction="row" spacing={1.5} mb={3}>
                <Button size="small" variant="outlined" color="success" startIcon={<PowerSettingsNew />}
                  disabled={activateMutation.isPending || deactivateMutation.isPending}
                  onClick={async () => { await activateMutation.mutateAsync(); setStatusMsg('Tienda activada correctamente.'); }}>
                  Activar
                </Button>
                <Button size="small" variant="outlined" color="error" startIcon={<Block />}
                  disabled={activateMutation.isPending || deactivateMutation.isPending}
                  onClick={async () => { await deactivateMutation.mutateAsync(); setStatusMsg('Tienda desactivada correctamente.'); }}>
                  Desactivar
                </Button>
              </Stack>

              <Divider sx={{ mb: 3 }} />
              <Stack direction="row" spacing={1} alignItems="center" mb={2}>
                <AccessTime fontSize="small" color="primary" />
                <Typography variant="subtitle1" fontWeight={700}>Horario de atención</Typography>
              </Stack>
              <ScheduleTab storeId={store.id} />

              <Divider sx={{ my: 3 }} />
              <Typography variant="subtitle1" fontWeight={700} mb={2}>Mapa</Typography>
              <StoreMapTab store={store} />
            </>
          )}
        </CardContent>
      </Card>

      <StoreDialog open={editOpen} onClose={() => setEditOpen(false)} store={store ?? undefined} />
    </>
  );
}

function TerminalsCard() {
  const { data: terminals, isLoading } = useTerminals();
  const { data: warehouses } = useWarehouses();
  const [dialogTerminal, setDialogTerminal] = useState<OpsTerminal | 'new' | null>(null);

  const whName = (id?: string) => warehouses?.find((w) => w.id === id)?.name ?? '—';

  return (
    <Card variant="outlined" sx={{ mb: 2 }}>
      <CardContent>
        <Box display="flex" justifyContent="space-between" alignItems="center" mb={2}>
          <Stack direction="row" spacing={1} alignItems="center">
            <PointOfSale fontSize="small" color="primary" />
            <Typography variant="h6" fontWeight={700}>Terminales</Typography>
          </Stack>
          <Button size="small" startIcon={<Add />} onClick={() => setDialogTerminal('new')}>Nueva terminal</Button>
        </Box>

        {isLoading ? (
          <Box display="flex" justifyContent="center" py={3}><CircularProgress /></Box>
        ) : (
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow><TableCell>Nombre</TableCell><TableCell>Almacén</TableCell><TableCell>Estado</TableCell><TableCell align="right">Acciones</TableCell></TableRow>
              </TableHead>
              <TableBody>
                {(terminals ?? []).map((t) => (
                  <TableRow key={t.id} hover>
                    <TableCell>{t.name}</TableCell>
                    <TableCell>{whName(t.warehouseId)}</TableCell>
                    <TableCell><Chip size="small" color={t.isActive ? 'success' : 'default'} label={t.isActive ? 'Activa' : 'Inactiva'} /></TableCell>
                    <TableCell align="right">
                      <IconButton size="small" onClick={() => setDialogTerminal(t)}><Edit fontSize="small" /></IconButton>
                    </TableCell>
                  </TableRow>
                ))}
                {(terminals ?? []).length === 0 && (
                  <TableRow><TableCell colSpan={4} align="center" sx={{ py: 3, color: 'text.secondary' }}>Sin terminales</TableCell></TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </CardContent>

      {dialogTerminal && (
        <TerminalDialog terminal={dialogTerminal === 'new' ? null : dialogTerminal} onClose={() => setDialogTerminal(null)} />
      )}
    </Card>
  );
}

// Configuración clave-valor genérica (flags/parámetros varios que no ameritan una entidad propia).
function SettingsKeyValueCard() {
  const { data: settings, isLoading } = useSettings();
  const save = useSaveSetting();
  const [newKey, setNewKey] = useState('');
  const [newValue, setNewValue] = useState('');
  const [editing, setEditing] = useState<Record<string, string>>({});

  const addNew = () => {
    if (!newKey.trim()) return;
    save.mutate({ key: newKey.trim(), value: newValue }, { onSuccess: () => { setNewKey(''); setNewValue(''); } });
  };

  return (
    <Card variant="outlined" sx={{ mb: 2 }}>
      <CardContent>
        <Stack direction="row" spacing={1} alignItems="center" mb={2}>
          <SettingsIcon fontSize="small" color="primary" />
          <Typography variant="h6" fontWeight={700}>Configuración avanzada</Typography>
        </Stack>
        <Typography variant="caption" color="text.secondary" display="block" mb={2}>
          Parámetros clave-valor de uso interno (p.ej. texto del pie del recibo, flags de comportamiento).
        </Typography>

        {isLoading ? (
          <Box display="flex" justifyContent="center" py={3}><CircularProgress /></Box>
        ) : (
          <TableContainer sx={{ mb: 2 }}>
            <Table size="small">
              <TableHead>
                <TableRow><TableCell>Clave</TableCell><TableCell>Valor</TableCell><TableCell align="right">Acciones</TableCell></TableRow>
              </TableHead>
              <TableBody>
                {(settings ?? []).map((s) => {
                  const editValue = editing[s.key] ?? s.value;
                  const changed = editValue !== s.value;
                  return (
                    <TableRow key={s.key} hover>
                      <TableCell sx={{ fontFamily: 'monospace' }}>{s.key}</TableCell>
                      <TableCell>
                        <TextField size="small" value={editValue} fullWidth
                          onChange={(e) => setEditing((ed) => ({ ...ed, [s.key]: e.target.value }))} />
                      </TableCell>
                      <TableCell align="right">
                        <Button size="small" disabled={!changed || save.isPending}
                          onClick={() => save.mutate({ key: s.key, value: editValue }, {
                            onSuccess: () => setEditing((ed) => { const n = { ...ed }; delete n[s.key]; return n; }),
                          })}>
                          Guardar
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
                {(settings ?? []).length === 0 && (
                  <TableRow><TableCell colSpan={3} align="center" sx={{ py: 3, color: 'text.secondary' }}>Sin configuración registrada</TableCell></TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        )}

        <Divider sx={{ mb: 2 }} />
        <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap">
          <TextField size="small" label="Clave nueva" value={newKey} onChange={(e) => setNewKey(e.target.value)} sx={{ minWidth: 180 }} />
          <TextField size="small" label="Valor" value={newValue} onChange={(e) => setNewValue(e.target.value)} sx={{ minWidth: 220 }} />
          <Button variant="outlined" startIcon={<Add />} disabled={!newKey.trim() || save.isPending} onClick={addNew}>Agregar</Button>
        </Stack>
        {save.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo guardar la configuración.</Alert>}
      </CardContent>
    </Card>
  );
}

function TerminalDialog({ terminal, onClose }: { terminal: OpsTerminal | null; onClose: () => void }) {
  const { data: warehouses } = useWarehouses();
  const save = useSaveTerminal();
  const [name, setName] = useState(terminal?.name ?? '');
  const [description, setDescription] = useState(terminal?.description ?? '');
  const [warehouseId, setWarehouseId] = useState(terminal?.warehouseId ?? '');
  const [isActive, setIsActive] = useState(terminal?.isActive ?? true);

  const submit = () => {
    save.mutate({ id: terminal?.id, name, description: description || undefined, warehouseId: warehouseId || undefined, isActive },
      { onSuccess: onClose });
  };

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{terminal ? 'Editar terminal' : 'Nueva terminal'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField size="small" label="Nombre" value={name} onChange={(e) => setName(e.target.value)} />
          <TextField size="small" label="Descripción (opcional)" value={description} onChange={(e) => setDescription(e.target.value)} />
          <TextField size="small" select label="Almacén asignado" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
            <MenuItem value="">Sin asignar</MenuItem>
            {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
          </TextField>
          {terminal && <FormControlLabel control={<Switch checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />} label="Activa" />}
          {save.isError && <Alert severity="error">No se pudo guardar la terminal.</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" disabled={!name || save.isPending} onClick={submit}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}
