import { useState } from 'react';
import {
  Box, Button, Card, Typography, Chip, CircularProgress, Stack, Grid, Alert, IconButton, Divider,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, Autocomplete, ToggleButtonGroup, ToggleButton,
} from '@mui/material';
import { Add, SwapHoriz, CallSplit, DeleteOutline } from '@mui/icons-material';
import { useInventoryMovements, useCreateMovement, useConvertInventory, useWarehouses, useOpsProducts } from '@/hooks/useOps';
import type { OpsProduct, MovementType, CreateOpsMovement, ConvertInventoryItem } from '@/lib/opsTypes';

const TYPE_LABEL: Record<string, string> = { Entrada: 'Entrada', Salida: 'Salida', Traslado: 'Traslado', Merma: 'Merma' };
const TYPE_COLOR: Record<string, 'success' | 'error' | 'info' | 'warning'> = { Entrada: 'success', Salida: 'error', Traslado: 'info', Merma: 'warning' };

export default function MovementsPage() {
  const { data: movements, isLoading } = useInventoryMovements();
  const { data: warehouses } = useWarehouses();
  const [open, setOpen] = useState(false);
  const [convertOpen, setConvertOpen] = useState(false);

  const whName = (id?: string) => warehouses?.find((w) => w.id === id)?.name ?? '—';

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3} flexWrap="wrap" gap={1}>
        <Typography variant="h5" fontWeight={700}>Movimientos de inventario</Typography>
        <Stack direction="row" spacing={1} flexWrap="wrap">
          <Button startIcon={<CallSplit />} variant="outlined" onClick={() => setConvertOpen(true)}>Convertir</Button>
          <Button startIcon={<Add />} variant="contained" onClick={() => setOpen(true)}>Nuevo movimiento</Button>
        </Stack>
      </Box>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Fecha</TableCell><TableCell>Producto</TableCell><TableCell>Tipo</TableCell>
                <TableCell align="right">Cantidad</TableCell><TableCell>Origen</TableCell><TableCell>Destino</TableCell><TableCell>Motivo</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(movements ?? []).map((m) => (
                <TableRow key={m.id} hover>
                  <TableCell>{new Date(m.date).toLocaleDateString()}</TableCell>
                  <TableCell>{m.productName}</TableCell>
                  <TableCell><Chip size="small" color={TYPE_COLOR[m.type]} label={TYPE_LABEL[m.type] ?? m.type} /></TableCell>
                  <TableCell align="right">{m.quantity}</TableCell>
                  <TableCell>{m.fromWarehouseId ? whName(m.fromWarehouseId) : '—'}</TableCell>
                  <TableCell>{m.toWarehouseId ? whName(m.toWarehouseId) : '—'}</TableCell>
                  <TableCell>{m.reason ?? '—'}</TableCell>
                </TableRow>
              ))}
              {(movements ?? []).length === 0 && <TableRow><TableCell colSpan={7} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin movimientos</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {open && <MovementDialog onClose={() => setOpen(false)} />}
      {convertOpen && <ConvertDialog onClose={() => setConvertOpen(false)} />}
    </Box>
  );
}

// Convierte/desglosa N productos origen (p.ej. una caja) en un producto destino con otra
// unidad (p.ej. unidades sueltas), o viceversa. El producto destino puede ser uno existente
// o darse de alta "al vuelo" con solo el nombre.
interface SourceLine { key: string; product: OpsProduct | null; quantity: number }

function ConvertDialog({ onClose }: { onClose: () => void }) {
  const { data: warehouses } = useWarehouses();
  const [warehouseId, setWarehouseId] = useState('');
  const { data: products } = useOpsProducts({ warehouseId: warehouseId || undefined });
  const convert = useConvertInventory();

  const [sources, setSources] = useState<SourceLine[]>([{ key: 'src-0', product: null, quantity: 1 }]);
  const [destMode, setDestMode] = useState<'existing' | 'new'>('existing');
  const [destProduct, setDestProduct] = useState<OpsProduct | null>(null);
  const [destNewName, setDestNewName] = useState('');
  const [destQuantity, setDestQuantity] = useState(1);
  const [reason, setReason] = useState('');

  const addSource = () => setSources((s) => [...s, { key: `src-${Date.now()}`, product: null, quantity: 1 }]);
  const removeSource = (key: string) => setSources((s) => s.filter((l) => l.key !== key));
  const setSource = (key: string, patch: Partial<SourceLine>) =>
    setSources((s) => s.map((l) => (l.key === key ? { ...l, ...patch } : l)));

  const validSources = sources.filter((s) => s.product && s.quantity > 0);
  const valid = !!warehouseId && validSources.length > 0 && destQuantity > 0
    && (destMode === 'existing' ? !!destProduct : destNewName.trim().length > 0);

  const submit = () => {
    const sourceItems: ConvertInventoryItem[] = validSources.map((s) => ({ productId: s.product!.id, quantity: s.quantity }));
    convert.mutate({
      warehouseId,
      sourceItems,
      destinationQuantity: destQuantity,
      destinationProductId: destMode === 'existing' ? destProduct?.id : undefined,
      newDestinationProduct: destMode === 'new' ? { name: destNewName.trim() } : undefined,
      reason: reason || undefined,
    }, { onSuccess: onClose });
  };

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Convertir inventario</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="caption" color="text.secondary">
            Consume uno o más productos origen y produce un producto destino (p.ej. desglosar una caja en unidades sueltas).
          </Typography>

          <TextField size="small" select label="Almacén" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
            {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
          </TextField>

          <Typography variant="subtitle2" fontWeight={700}>Productos origen (se consumen)</Typography>
          {sources.map((s) => (
            <Stack key={s.key} direction="row" spacing={1} alignItems="center" flexWrap="wrap">
              <Autocomplete size="small" sx={{ flex: '1 1 160px' }} options={products ?? []} getOptionLabel={(p) => p.name}
                value={s.product} onChange={(_, v) => setSource(s.key, { product: v })}
                renderInput={(params) => <TextField {...params} label="Producto" />} />
              <TextField size="small" type="number" label="Cant." sx={{ width: 90 }}
                value={s.quantity || ''} onChange={(e) => setSource(s.key, { quantity: e.target.value === '' ? 0 : Number(e.target.value) })} />
              <IconButton size="small" onClick={() => removeSource(s.key)} disabled={sources.length === 1}>
                <DeleteOutline fontSize="small" />
              </IconButton>
            </Stack>
          ))}
          <Button size="small" onClick={addSource} sx={{ alignSelf: 'flex-start' }}>+ Agregar producto origen</Button>

          <Divider />

          <Typography variant="subtitle2" fontWeight={700}>Producto destino (se produce)</Typography>
          <ToggleButtonGroup exclusive fullWidth size="small" value={destMode} onChange={(_, v) => v && setDestMode(v)}>
            <ToggleButton value="existing">Producto existente</ToggleButton>
            <ToggleButton value="new">Producto nuevo</ToggleButton>
          </ToggleButtonGroup>
          {destMode === 'existing' ? (
            <Autocomplete size="small" options={products ?? []} getOptionLabel={(p) => p.name}
              value={destProduct} onChange={(_, v) => setDestProduct(v)}
              renderInput={(params) => <TextField {...params} label="Producto destino" />} />
          ) : (
            <TextField size="small" label="Nombre del producto nuevo" value={destNewName} onChange={(e) => setDestNewName(e.target.value)} />
          )}
          <TextField size="small" type="number" label="Cantidad producida" value={destQuantity || ''}
            onChange={(e) => setDestQuantity(e.target.value === '' ? 0 : Number(e.target.value))} />

          <TextField size="small" label="Motivo (opcional)" value={reason} onChange={(e) => setReason(e.target.value)} />
          {convert.isError && <Alert severity="error">No se pudo convertir (¿stock suficiente en los productos origen?).</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" startIcon={<CallSplit />} disabled={!valid || convert.isPending} onClick={submit}>Convertir</Button>
      </DialogActions>
    </Dialog>
  );
}

function MovementDialog({ onClose }: { onClose: () => void }) {
  const { data: warehouses } = useWarehouses();
  const { data: products } = useOpsProducts();
  const create = useCreateMovement();

  const [type, setType] = useState<MovementType>('Entrada');
  const [product, setProduct] = useState<OpsProduct | null>(null);
  const [quantity, setQuantity] = useState(1);
  const [fromWarehouseId, setFrom] = useState('');
  const [toWarehouseId, setTo] = useState('');
  const [reason, setReason] = useState('');

  const needsFrom = type === 'Salida' || type === 'Merma' || type === 'Traslado';
  const needsTo = type === 'Entrada' || type === 'Traslado';

  const submit = () => {
    const dto: CreateOpsMovement = {
      productId: product!.id, type, quantity, reason: reason || undefined,
      fromWarehouseId: needsFrom ? fromWarehouseId : undefined,
      toWarehouseId: needsTo ? toWarehouseId : undefined,
    };
    create.mutate(dto, { onSuccess: onClose });
  };

  const valid = !!product && quantity > 0 && (!needsFrom || fromWarehouseId) && (!needsTo || toWarehouseId)
    && (type !== 'Traslado' || fromWarehouseId !== toWarehouseId);

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Nuevo movimiento</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <ToggleButtonGroup exclusive fullWidth size="small" value={type} onChange={(_, v) => v && setType(v)}>
            <ToggleButton value="Entrada">Entrada</ToggleButton>
            <ToggleButton value="Salida">Salida</ToggleButton>
            <ToggleButton value="Traslado">Traslado</ToggleButton>
            <ToggleButton value="Merma">Merma</ToggleButton>
          </ToggleButtonGroup>
          <Autocomplete size="small" options={products ?? []} getOptionLabel={(p) => p.name}
            value={product} onChange={(_, v) => setProduct(v)}
            renderInput={(params) => <TextField {...params} label="Producto" />} />
          <TextField size="small" type="number" label="Cantidad" value={quantity || ''} onChange={(e) => setQuantity(e.target.value === '' ? 0 : Number(e.target.value))} />
          <Grid container spacing={2}>
            {needsFrom && (
              <Grid item xs={12} sm={needsTo ? 6 : 12}>
                <TextField fullWidth size="small" select label="Origen" value={fromWarehouseId} onChange={(e) => setFrom(e.target.value)}>
                  {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
                </TextField>
              </Grid>
            )}
            {needsTo && (
              <Grid item xs={12} sm={needsFrom ? 6 : 12}>
                <TextField fullWidth size="small" select label="Destino" value={toWarehouseId} onChange={(e) => setTo(e.target.value)}>
                  {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
                </TextField>
              </Grid>
            )}
          </Grid>
          <TextField size="small" label="Motivo (opcional)" value={reason} onChange={(e) => setReason(e.target.value)} />
          {create.isError && <Alert severity="error">No se pudo registrar (¿stock suficiente en el origen?).</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" startIcon={<SwapHoriz />} disabled={!valid || create.isPending} onClick={submit}>Registrar</Button>
      </DialogActions>
    </Dialog>
  );
}
