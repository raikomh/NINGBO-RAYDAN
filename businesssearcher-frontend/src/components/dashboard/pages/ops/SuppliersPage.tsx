import { useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, IconButton, Chip, CircularProgress,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField,
  Dialog, DialogTitle, DialogContent, DialogActions, Grid, Tooltip, Stack, Alert,
} from '@mui/material';
import { Add, Edit, Delete, LocalShipping } from '@mui/icons-material';
import { useSuppliers, useSaveSupplier, useDeleteSupplier } from '@/hooks/useOps';
import type { OpsSupplier } from '@/lib/opsTypes';

export default function SuppliersPage() {
  const [search, setSearch] = useState('');
  const { data: suppliers, isLoading } = useSuppliers(search || undefined);
  const del = useDeleteSupplier();

  const [dialog, setDialog] = useState(false);
  const [edit, setEdit] = useState<OpsSupplier | null>(null);

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" flexWrap="wrap" gap={2} mb={3}>
        <Typography variant="h5" fontWeight={700}>Proveedores</Typography>
        <Button startIcon={<Add />} variant="contained"
          onClick={() => { setEdit(null); setDialog(true); }}>Nuevo proveedor</Button>
      </Box>

      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
          <TextField size="small" label="Buscar (nombre o contacto)" value={search}
            onChange={(e) => setSearch(e.target.value)} sx={{ minWidth: 240 }} />
        </CardContent>
      </Card>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Proveedor</TableCell>
                <TableCell>Contacto</TableCell>
                <TableCell>Teléfono</TableCell>
                <TableCell>Email</TableCell>
                <TableCell>Categoría</TableCell>
                <TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(suppliers ?? []).map((s) => (
                <TableRow key={s.id} hover>
                  <TableCell>
                    <Typography variant="body2" fontWeight={600}>{s.name}</Typography>
                    {s.address && <Typography variant="caption" color="text.secondary" display="block">{s.address}</Typography>}
                  </TableCell>
                  <TableCell>{s.contact ?? '—'}</TableCell>
                  <TableCell>{s.phone}</TableCell>
                  <TableCell>{s.email ?? '—'}</TableCell>
                  <TableCell>{s.category ? <Chip size="small" variant="outlined" label={s.category} /> : '—'}</TableCell>
                  <TableCell align="right">
                    <Tooltip title="Editar"><IconButton size="small" onClick={() => { setEdit(s); setDialog(true); }}><Edit fontSize="small" /></IconButton></Tooltip>
                    <Tooltip title="Eliminar"><IconButton size="small" color="error" onClick={() => del.mutate(s.id)}><Delete fontSize="small" /></IconButton></Tooltip>
                  </TableCell>
                </TableRow>
              ))}
              {(suppliers ?? []).length === 0 && (
                <TableRow><TableCell colSpan={6} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin proveedores</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {dialog && <SupplierDialog supplier={edit} onClose={() => setDialog(false)} />}
    </Box>
  );
}

function SupplierDialog({ supplier, onClose }: { supplier: OpsSupplier | null; onClose: () => void }) {
  const save = useSaveSupplier();
  const [form, setForm] = useState({
    name: supplier?.name ?? '',
    phone: supplier?.phone ?? '',
    email: supplier?.email ?? '',
    contact: supplier?.contact ?? '',
    address: supplier?.address ?? '',
    category: supplier?.category ?? '',
  });

  const set = (k: keyof typeof form, v: string) => setForm((f) => ({ ...f, [k]: v }));

  const submit = () => {
    save.mutate({
      id: supplier?.id,
      name: form.name,
      phone: form.phone,
      email: form.email || undefined,
      contact: form.contact || undefined,
      address: form.address || undefined,
      category: form.category || undefined,
    }, { onSuccess: onClose });
  };

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        <Stack direction="row" spacing={1} alignItems="center">
          <LocalShipping fontSize="small" />
          <span>{supplier ? 'Editar proveedor' : 'Nuevo proveedor'}</span>
        </Stack>
      </DialogTitle>
      <DialogContent>
        <Grid container spacing={2} sx={{ mt: 0 }}>
          <Grid item xs={12} sm={8}><TextField fullWidth size="small" label="Nombre" value={form.name} onChange={(e) => set('name', e.target.value)} /></Grid>
          <Grid item xs={12} sm={4}><TextField fullWidth size="small" label="Teléfono" value={form.phone} onChange={(e) => set('phone', e.target.value)} /></Grid>
          <Grid item xs={12} sm={6}><TextField fullWidth size="small" label="Contacto" value={form.contact} onChange={(e) => set('contact', e.target.value)} /></Grid>
          <Grid item xs={12} sm={6}><TextField fullWidth size="small" label="Email" value={form.email} onChange={(e) => set('email', e.target.value)} /></Grid>
          <Grid item xs={12} sm={8}><TextField fullWidth size="small" label="Dirección" value={form.address} onChange={(e) => set('address', e.target.value)} /></Grid>
          <Grid item xs={12} sm={4}><TextField fullWidth size="small" label="Categoría" value={form.category} onChange={(e) => set('category', e.target.value)} /></Grid>
        </Grid>
        {save.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo guardar el proveedor.</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" onClick={submit} disabled={!form.name || !form.phone || save.isPending}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}
