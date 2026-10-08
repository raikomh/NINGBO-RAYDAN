import { useState } from 'react';
import {
  Box, Button, Card, Typography, Chip, CircularProgress, IconButton, Tooltip,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Dialog, DialogTitle,
  DialogContent, DialogActions, TextField, Stack, FormControlLabel, Switch, Alert,
} from '@mui/material';
import { Add, Edit } from '@mui/icons-material';
import { useOpsManagers, useSaveOpsManager } from '@/hooks/useOps';
import type { OpsManager, SaveOpsManager } from '@/lib/opsTypes';

export default function ManagersPage() {
  const { data: managers, isLoading } = useOpsManagers();
  const [dialog, setDialog] = useState(false);
  const [edit, setEdit] = useState<OpsManager | null>(null);

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h5" fontWeight={700}>Gestores</Typography>
        <Button startIcon={<Add />} variant="contained" onClick={() => { setEdit(null); setDialog(true); }}>Nuevo gestor</Button>
      </Box>

      <Alert severity="info" sx={{ mb: 2 }}>
        El <strong>código</strong> del gestor se indica en el Punto de Venta al registrar cada venta y aparece en el
        historial de ventas (columna «Código del Gestor») para saber qué gestor la realizó.
      </Alert>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Código</TableCell><TableCell>Nombre</TableCell><TableCell>CI</TableCell>
                <TableCell>Municipio</TableCell><TableCell>Provincia</TableCell><TableCell>Teléfono</TableCell>
                <TableCell>Estado</TableCell><TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(managers ?? []).map((m) => (
                <TableRow key={m.id} hover>
                  <TableCell><Typography variant="body2" fontWeight={700}>{m.code}</Typography></TableCell>
                  <TableCell>{m.name}</TableCell>
                  <TableCell>{m.idNumber}</TableCell>
                  <TableCell>{m.municipality}</TableCell>
                  <TableCell>{m.province}</TableCell>
                  <TableCell>{m.phone}</TableCell>
                  <TableCell><Chip size="small" color={m.isActive ? 'success' : 'default'} label={m.isActive ? 'Activo' : 'Inactivo'} variant={m.isActive ? 'filled' : 'outlined'} /></TableCell>
                  <TableCell align="right">
                    <Tooltip title="Editar"><IconButton size="small" onClick={() => { setEdit(m); setDialog(true); }}><Edit fontSize="small" /></IconButton></Tooltip>
                  </TableCell>
                </TableRow>
              ))}
              {(managers ?? []).length === 0 && <TableRow><TableCell colSpan={8} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin gestores. Crea el primero.</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {dialog && <ManagerDialog manager={edit} onClose={() => setDialog(false)} />}
    </Box>
  );
}

function ManagerDialog({ manager, onClose }: { manager: OpsManager | null; onClose: () => void }) {
  const save = useSaveOpsManager();
  const [code, setCode] = useState(manager?.code ?? '');
  const [name, setName] = useState(manager?.name ?? '');
  const [idNumber, setIdNumber] = useState(manager?.idNumber ?? '');
  const [municipality, setMunicipality] = useState(manager?.municipality ?? '');
  const [province, setProvince] = useState(manager?.province ?? '');
  const [phone, setPhone] = useState(manager?.phone ?? '');
  const [isActive, setIsActive] = useState(manager?.isActive ?? true);

  const valid = [code, name, idNumber, municipality, province, phone].every((v) => v.trim());

  const submit = () => {
    const dto: SaveOpsManager = { code, name, idNumber, municipality, province, phone };
    if (manager) save.mutate({ id: manager.id, dto: { ...dto, isActive } }, { onSuccess: onClose });
    else save.mutate({ dto }, { onSuccess: onClose });
  };

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{manager ? 'Editar gestor' : 'Nuevo gestor'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField size="small" label="Código" value={code} onChange={(e) => setCode(e.target.value)} />
          <TextField size="small" label="Nombre" value={name} onChange={(e) => setName(e.target.value)} />
          <TextField size="small" label="CI" value={idNumber} onChange={(e) => setIdNumber(e.target.value)} />
          <TextField size="small" label="Municipio" value={municipality} onChange={(e) => setMunicipality(e.target.value)} />
          <TextField size="small" label="Provincia" value={province} onChange={(e) => setProvince(e.target.value)} />
          <TextField size="small" label="Número de teléfono" value={phone} onChange={(e) => setPhone(e.target.value)} />
          {manager && <FormControlLabel control={<Switch checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />} label="Activo" />}
          {save.isError && <Alert severity="error">No se pudo guardar (¿código duplicado o campos vacíos?).</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" disabled={save.isPending || !valid} onClick={submit}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}
