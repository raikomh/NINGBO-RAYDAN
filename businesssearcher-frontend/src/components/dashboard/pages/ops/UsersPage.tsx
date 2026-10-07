import { useState } from 'react';
import {
  Box, Button, Card, Typography, Chip, CircularProgress, IconButton, Tooltip,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Dialog, DialogTitle,
  DialogContent, DialogActions, TextField, MenuItem, Stack, FormControlLabel, Switch, Alert,
} from '@mui/material';
import { Add, Edit, Badge } from '@mui/icons-material';
import { useOpsUsers, useSaveOpsUser, useWarehouses } from '@/hooks/useOps';
import type { OpsUser, OpsRole, CreateOpsUser, UpdateOpsUser } from '@/lib/opsTypes';

const ROLES: OpsRole[] = ['Administrador', 'Cajero', 'JefeDeTurno', 'Almacenero', 'Comercial', 'Auditor'];
const ROLE_LABEL: Record<OpsRole, string> = {
  Administrador: 'Administrador', Cajero: 'Cajero', JefeDeTurno: 'Jefe de Turno',
  Almacenero: 'Almacenero', Comercial: 'Comercial', Auditor: 'Auditor',
};

export default function UsersPage() {
  const { data: users, isLoading } = useOpsUsers();
  const [dialog, setDialog] = useState(false);
  const [edit, setEdit] = useState<OpsUser | null>(null);

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h5" fontWeight={700}>Usuarios del negocio</Typography>
        <Button startIcon={<Add />} variant="contained" onClick={() => { setEdit(null); setDialog(true); }}>Nuevo usuario</Button>
      </Box>

      <Alert severity="info" sx={{ mb: 2 }}>
        Los empleados inician sesión en la pantalla de login con la opción <strong>Empleado (TPV)</strong>,
        usando su email y contraseña. Su rol define a qué operaciones tienen acceso.
      </Alert>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Nombre</TableCell><TableCell>Email</TableCell><TableCell>Rol</TableCell>
                <TableCell>Estado</TableCell><TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(users ?? []).map((u) => (
                <TableRow key={u.id} hover>
                  <TableCell><Typography variant="body2" fontWeight={600}>{u.name}</Typography></TableCell>
                  <TableCell>{u.email}</TableCell>
                  <TableCell><Chip size="small" icon={<Badge fontSize="small" />} label={ROLE_LABEL[u.role] ?? u.role} /></TableCell>
                  <TableCell><Chip size="small" color={u.isActive ? 'success' : 'default'} label={u.isActive ? 'Activo' : 'Inactivo'} variant={u.isActive ? 'filled' : 'outlined'} /></TableCell>
                  <TableCell align="right">
                    <Tooltip title="Editar"><IconButton size="small" onClick={() => { setEdit(u); setDialog(true); }}><Edit fontSize="small" /></IconButton></Tooltip>
                  </TableCell>
                </TableRow>
              ))}
              {(users ?? []).length === 0 && <TableRow><TableCell colSpan={5} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin empleados. Crea el primero.</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {dialog && <UserDialog user={edit} onClose={() => setDialog(false)} />}
    </Box>
  );
}

function UserDialog({ user, onClose }: { user: OpsUser | null; onClose: () => void }) {
  const save = useSaveOpsUser();
  const { data: warehouses } = useWarehouses();
  const [name, setName] = useState(user?.name ?? '');
  const [email, setEmail] = useState(user?.email ?? '');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<OpsRole>(user?.role ?? 'Cajero');
  const [warehouseId, setWarehouseId] = useState(user?.assignedWarehouseId ?? '');
  const [isActive, setIsActive] = useState(user?.isActive ?? true);

  const submit = () => {
    if (user) {
      const dto: UpdateOpsUser = {
        name, role, assignedWarehouseId: warehouseId || undefined, isActive,
        newPassword: password || undefined,
      };
      save.mutate({ id: user.id, dto }, { onSuccess: onClose });
    } else {
      const dto: CreateOpsUser = { name, email, password, role, assignedWarehouseId: warehouseId || undefined };
      save.mutate({ dto }, { onSuccess: onClose });
    }
  };

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{user ? 'Editar usuario' : 'Nuevo usuario'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField size="small" label="Nombre" value={name} onChange={(e) => setName(e.target.value)} />
          <TextField size="small" label="Email" type="email" value={email} disabled={!!user}
            onChange={(e) => setEmail(e.target.value)} />
          <TextField size="small" label={user ? 'Nueva contraseña (opcional)' : 'Contraseña'} type="password"
            value={password} onChange={(e) => setPassword(e.target.value)} />
          <TextField size="small" select label="Rol" value={role} onChange={(e) => setRole(e.target.value as OpsRole)}>
            {ROLES.map((r) => <MenuItem key={r} value={r}>{ROLE_LABEL[r]}</MenuItem>)}
          </TextField>
          <TextField size="small" select label="Almacén asignado (opcional)" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
            <MenuItem value="">Sin asignar</MenuItem>
            {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
          </TextField>
          {user && <FormControlLabel control={<Switch checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />} label="Activo" />}
          {save.isError && <Alert severity="error">No se pudo guardar (¿email duplicado o contraseña &lt; 6?).</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" disabled={save.isPending || !name || (!user && (!email || password.length < 6))}
          onClick={submit}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}
