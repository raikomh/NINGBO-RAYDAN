import { useMemo, useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, Chip, CircularProgress, IconButton, Stack, Grid,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, MenuItem, Dialog,
  DialogTitle, DialogContent, DialogActions, Autocomplete, Alert, Divider,
} from '@mui/material';
import { Add, DeleteOutline, ShoppingCart, NoteAdd } from '@mui/icons-material';
import {
  usePurchases, useCreatePurchase, useWarehouses, useOpsProducts, useSuppliers, usePurchaseRequests,
} from '@/hooks/useOps';
import type { OpsProduct, CreateOpsPurchase, NewPurchaseProduct } from '@/lib/opsTypes';

// Una línea representa un producto existente (`product`) o el alta de uno nuevo
// "al vuelo" (`newProduct`) que se crea al confirmar la compra.
interface Line { key: string; product?: OpsProduct; newProduct?: NewPurchaseProduct; quantity: number; costPrice: number; }

export default function PurchasesPage() {
  const { data: purchases, isLoading } = usePurchases();
  const [open, setOpen] = useState(false);

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h5" fontWeight={700}>Compras</Typography>
        <Button startIcon={<Add />} variant="contained" onClick={() => setOpen(true)}>Nueva compra</Button>
      </Box>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Fecha</TableCell><TableCell align="right">Productos</TableCell>
                <TableCell align="right">Gastos asoc.</TableCell><TableCell align="right">Total</TableCell>
                <TableCell>Estado</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(purchases ?? []).map((p) => (
                <TableRow key={p.id} hover>
                  <TableCell>{new Date(p.date).toLocaleDateString()}</TableCell>
                  <TableCell align="right">{p.items.length}</TableCell>
                  <TableCell align="right">{p.associatedExpenses.toFixed(2)}</TableCell>
                  <TableCell align="right">{p.total.toFixed(2)} {p.currency}</TableCell>
                  <TableCell><Chip size="small" label={p.status} /></TableCell>
                </TableRow>
              ))}
              {(purchases ?? []).length === 0 && <TableRow><TableCell colSpan={5} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin compras</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {open && <PurchaseDialog onClose={() => setOpen(false)} />}
    </Box>
  );
}

function PurchaseDialog({ onClose }: { onClose: () => void }) {
  const { data: warehouses } = useWarehouses();
  const { data: suppliers } = useSuppliers();
  const { data: approved } = usePurchaseRequests('Approved');
  const create = useCreatePurchase();

  const [warehouseId, setWarehouseId] = useState('');
  const [supplierId, setSupplierId] = useState('');
  const [expenses, setExpenses] = useState(0);
  const [lines, setLines] = useState<Line[]>([]);
  const [newProductMode, setNewProductMode] = useState(false);
  const [newProductName, setNewProductName] = useState('');
  const { data: products } = useOpsProducts({ warehouseId: warehouseId || undefined });

  const addLine = (p: OpsProduct | null) => {
    if (!p || lines.some((l) => l.product?.id === p.id)) return;
    setLines((ls) => [...ls, { key: p.id, product: p, quantity: 1, costPrice: p.costPrice }]);
  };
  const addNewProductLine = () => {
    if (!newProductName.trim()) return;
    setLines((ls) => [...ls, {
      key: `new-${Date.now()}`, newProduct: { name: newProductName.trim() }, quantity: 1, costPrice: 0,
    }]);
    setNewProductName('');
    setNewProductMode(false);
  };
  const setLine = (key: string, patch: Partial<Line>) =>
    setLines((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  const remove = (key: string) => setLines((ls) => ls.filter((l) => l.key !== key));

  const loadApproved = () => {
    const wh = warehouseId;
    (approved ?? []).filter((r) => !wh || r.warehouseId === wh).forEach((r) => {
      const p = products?.find((pp) => pp.id === r.productId);
      if (p && !lines.some((l) => l.product?.id === p.id)) {
        setLines((ls) => [...ls, { key: p.id, product: p, quantity: r.requestedQuantity, costPrice: p.costPrice }]);
      }
    });
  };

  const total = useMemo(() => lines.reduce((s, l) => s + l.costPrice * l.quantity, 0) + Number(expenses || 0), [lines, expenses]);

  const submit = () => {
    const dto: CreateOpsPurchase = {
      warehouseId,
      supplierId: supplierId || undefined,
      associatedExpenses: Number(expenses) || 0,
      items: lines.map((l) => ({
        productId: l.product?.id,
        quantity: l.quantity,
        costPrice: l.costPrice,
        newProduct: l.newProduct,
      })),
      purchaseRequestIds: (approved ?? []).filter((r) => (!warehouseId || r.warehouseId === warehouseId)
        && lines.some((l) => l.product?.id === r.productId)).map((r) => r.id),
    };
    create.mutate(dto, { onSuccess: onClose });
  };

  return (
    <Dialog open onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Nueva compra</DialogTitle>
      <DialogContent>
        <Grid container spacing={2} sx={{ mt: 0 }}>
          <Grid item xs={12} sm={4}>
            <TextField fullWidth size="small" select label="Almacén" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
              {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
            </TextField>
          </Grid>
          <Grid item xs={12} sm={4}>
            <TextField fullWidth size="small" select label="Proveedor (opcional)" value={supplierId} onChange={(e) => setSupplierId(e.target.value)}>
              <MenuItem value="">Sin proveedor</MenuItem>
              {suppliers?.map((s) => <MenuItem key={s.id} value={s.id}>{s.name}</MenuItem>)}
            </TextField>
          </Grid>
          <Grid item xs={12} sm={4}>
            <TextField fullWidth size="small" type="number" label="Gastos asociados" value={expenses} onChange={(e) => setExpenses(Number(e.target.value))} />
          </Grid>
        </Grid>

        <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mt: 2, mb: 1 }}>
          <Autocomplete
            size="small" sx={{ flex: 1 }} options={products ?? []} getOptionLabel={(p) => p.name}
            onChange={(_, v) => addLine(v)} value={null} disabled={!warehouseId}
            renderInput={(params) => <TextField {...params} label="Agregar producto" placeholder="Buscar…" />}
          />
          <Button variant="outlined" disabled={!warehouseId || (approved ?? []).length === 0} onClick={loadApproved}>
            Cargar solicitudes aprobadas
          </Button>
          <Button variant="outlined" startIcon={<NoteAdd />} disabled={!warehouseId} onClick={() => setNewProductMode(true)}>
            Producto nuevo
          </Button>
        </Stack>

        {newProductMode && (
          <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 2 }}>
            <TextField size="small" sx={{ flex: 1 }} autoFocus label="Nombre del producto nuevo" value={newProductName}
              onChange={(e) => setNewProductName(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && addNewProductLine()} />
            <Button variant="contained" size="small" disabled={!newProductName.trim()} onClick={addNewProductLine}>Agregar</Button>
            <Button size="small" onClick={() => { setNewProductMode(false); setNewProductName(''); }}>Cancelar</Button>
          </Stack>
        )}

        <TableContainer component={Card} variant="outlined" sx={{ mt: 1 }}>
          <Table size="small">
            <TableHead><TableRow><TableCell>Producto</TableCell><TableCell align="right">Cantidad</TableCell><TableCell align="right">Costo</TableCell><TableCell align="right">Subtotal</TableCell><TableCell /></TableRow></TableHead>
            <TableBody>
              {lines.map((l) => (
                <TableRow key={l.key}>
                  <TableCell>
                    {l.product?.name ?? l.newProduct?.name}
                    {l.newProduct && <Chip size="small" label="Nuevo" color="info" variant="outlined" sx={{ ml: 1 }} />}
                  </TableCell>
                  <TableCell align="right"><TextField size="small" type="number" value={l.quantity} onChange={(e) => setLine(l.key, { quantity: Math.max(1, Number(e.target.value)) })} sx={{ width: 80 }} /></TableCell>
                  <TableCell align="right"><TextField size="small" type="number" value={l.costPrice} onChange={(e) => setLine(l.key, { costPrice: Number(e.target.value) })} sx={{ width: 90 }} /></TableCell>
                  <TableCell align="right">{(l.costPrice * l.quantity).toFixed(2)}</TableCell>
                  <TableCell align="right"><IconButton size="small" onClick={() => remove(l.key)}><DeleteOutline fontSize="small" /></IconButton></TableCell>
                </TableRow>
              ))}
              {lines.length === 0 && <TableRow><TableCell colSpan={5} align="center" sx={{ py: 3, color: 'text.secondary' }}>Agrega productos a recibir</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>

        <Divider sx={{ my: 2 }} />
        <Box display="flex" justifyContent="space-between">
          <Typography variant="h6" fontWeight={800}>Total</Typography>
          <Typography variant="h6" fontWeight={800}>{total.toFixed(2)}</Typography>
        </Box>
        {create.isError && <Alert severity="error" sx={{ mt: 1 }}>No se pudo registrar la compra.</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" startIcon={<ShoppingCart />} disabled={!warehouseId || lines.length === 0 || create.isPending} onClick={submit}>
          Registrar compra
        </Button>
      </DialogActions>
    </Dialog>
  );
}
