import { useMemo, useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, Chip, CircularProgress, IconButton, Stack, Grid, Tooltip,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, MenuItem, Dialog,
  DialogTitle, DialogContent, DialogActions, Autocomplete, Alert, Divider,
} from '@mui/material';
import { Add, DeleteOutline, ShoppingCart, NoteAdd, Visibility, Edit, Block } from '@mui/icons-material';
import {
  usePurchases, useCreatePurchase, useUpdatePurchase, useCancelPurchase,
  useWarehouses, useOpsProducts, useSuppliers, usePurchaseRequests,
} from '@/hooks/useOps';
import { useIsOpsAdmin } from '@/hooks/useOpsRole';
import type { OpsProduct, OpsPurchase, CreateOpsPurchase, UpdateOpsPurchaseItem, NewPurchaseProduct } from '@/lib/opsTypes';

const STATUS_LABEL: Record<string, string> = { Completed: 'Completada', Pending: 'Pendiente', Cancelled: 'Cancelada' };
const STATUS_COLOR: Record<string, 'success' | 'warning' | 'error' | 'default'> = {
  Completed: 'success', Pending: 'warning', Cancelled: 'error',
};

// Una línea representa un producto existente (`product`) o el alta de uno nuevo
// "al vuelo" (`newProduct`) que se crea al confirmar la compra.
interface Line { key: string; product?: OpsProduct; newProduct?: NewPurchaseProduct; quantity: number; costPrice: number; }

export default function PurchasesPage() {
  const { data: purchases, isLoading } = usePurchases();
  const [open, setOpen] = useState(false);
  const [selected, setSelected] = useState<OpsPurchase | null>(null);

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
                <TableCell>Estado</TableCell><TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(purchases ?? []).map((p) => (
                <TableRow key={p.id} hover>
                  <TableCell>{new Date(p.date).toLocaleDateString()}</TableCell>
                  <TableCell align="right">{p.items.length}</TableCell>
                  <TableCell align="right">{p.associatedExpenses.toFixed(2)}</TableCell>
                  <TableCell align="right">{p.total.toFixed(2)} {p.currency}</TableCell>
                  <TableCell>
                    <Tooltip title={p.status === 'Cancelled' && p.cancellationReason ? `Motivo: ${p.cancellationReason}` : ''}>
                      <Chip size="small" label={STATUS_LABEL[p.status] ?? p.status} color={STATUS_COLOR[p.status] ?? 'default'} variant="outlined" />
                    </Tooltip>
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title="Ver detalle">
                      <IconButton size="small" onClick={() => setSelected(p)}><Visibility fontSize="small" /></IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
              {(purchases ?? []).length === 0 && <TableRow><TableCell colSpan={6} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin compras</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {open && <PurchaseDialog onClose={() => setOpen(false)} />}
      {selected && <PurchaseDetailDialog purchase={selected} onClose={() => setSelected(null)} />}
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
            <TextField fullWidth size="small" type="number" label="Gastos asociados" value={expenses || ''} onChange={(e) => setExpenses(e.target.value === '' ? 0 : Number(e.target.value))} />
          </Grid>
        </Grid>

        <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap" sx={{ mt: 2, mb: 1 }}>
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
          <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap" sx={{ mb: 2 }}>
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
                  <TableCell align="right"><TextField size="small" type="number" value={l.quantity || ''} onChange={(e) => setLine(l.key, { quantity: e.target.value === '' ? 0 : Number(e.target.value) })} sx={{ width: 80 }} /></TableCell>
                  <TableCell align="right"><TextField size="small" type="number" value={l.costPrice || ''} onChange={(e) => setLine(l.key, { costPrice: e.target.value === '' ? 0 : Number(e.target.value) })} sx={{ width: 90 }} /></TableCell>
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

function PurchaseDetailDialog({ purchase, onClose }: { purchase: OpsPurchase; onClose: () => void }) {
  const update = useUpdatePurchase();
  const cancel = useCancelPurchase();
  // Regla TPV: modificar o cancelar una compra ya registrada es exclusivo del Administrador.
  const isAdmin = useIsOpsAdmin();
  const canCancel = isAdmin;
  const canEdit = isAdmin && purchase.status !== 'Cancelled';

  const [editMode, setEditMode] = useState(false);
  const [editItems, setEditItems] = useState<UpdateOpsPurchaseItem[]>([]);
  const [editExpenses, setEditExpenses] = useState(purchase.associatedExpenses);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [cancelReason, setCancelReason] = useState('');

  const startEdit = () => {
    setEditItems(purchase.items.map((it) => ({
      productId: it.productId, quantity: it.quantity, costPrice: it.costPrice,
      batchNumber: it.batchNumber, expirationDate: it.expirationDate,
    })));
    setEditExpenses(purchase.associatedExpenses);
    setEditMode(true);
  };
  const setEditItem = (i: number, patch: Partial<UpdateOpsPurchaseItem>) =>
    setEditItems((items) => items.map((it, idx) => (idx === i ? { ...it, ...patch } : it)));
  const removeEditItem = (i: number) => setEditItems((items) => items.filter((_, idx) => idx !== i));

  const saveEdit = () => {
    update.mutate({
      id: purchase.id,
      dto: {
        items: editItems,
        associatedExpenses: Number(editExpenses) || 0,
        supplierId: purchase.supplierId,
        invoiceUrl: purchase.invoiceUrl,
      },
    }, { onSuccess: () => setEditMode(false) });
  };
  const editValid = editItems.length > 0 && editItems.every((it) => it.quantity > 0 && it.costPrice >= 0);
  const editTotal = editItems.reduce((s, it) => s + it.quantity * it.costPrice, 0) + (Number(editExpenses) || 0);

  const doCancel = () => cancel.mutate({ id: purchase.id, reason: cancelReason.trim() }, { onSuccess: onClose });

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Compra del {new Date(purchase.date).toLocaleString('es-ES')}</DialogTitle>
      <DialogContent dividers>
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Producto</TableCell><TableCell align="right">Cant.</TableCell>
                <TableCell align="right">Costo</TableCell><TableCell align="right">Subtotal</TableCell>
                {editMode && <TableCell />}
              </TableRow>
            </TableHead>
            <TableBody>
              {(editMode ? editItems : purchase.items).map((it, i) => (
                <TableRow key={editMode ? it.productId : i}>
                  <TableCell>{purchase.items.find((oi) => oi.productId === it.productId)?.productName ?? '—'}</TableCell>
                  <TableCell align="right">
                    {editMode ? (
                      <TextField size="small" type="number" value={editItems[i].quantity || ''} sx={{ width: 80 }}
                        onChange={(e) => setEditItem(i, { quantity: e.target.value === '' ? 0 : Number(e.target.value) })} />
                    ) : it.quantity}
                  </TableCell>
                  <TableCell align="right">
                    {editMode ? (
                      <TextField size="small" type="number" value={editItems[i].costPrice || ''} sx={{ width: 90 }}
                        onChange={(e) => setEditItem(i, { costPrice: e.target.value === '' ? 0 : Number(e.target.value) })} />
                    ) : it.costPrice.toFixed(2)}
                  </TableCell>
                  <TableCell align="right">{(it.costPrice * it.quantity).toFixed(2)}</TableCell>
                  {editMode && (
                    <TableCell align="right">
                      <IconButton size="small" disabled={editItems.length <= 1} onClick={() => removeEditItem(i)}>
                        <DeleteOutline fontSize="small" />
                      </IconButton>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>

        <Divider sx={{ my: 2 }} />

        <Box display="flex" justifyContent="space-between">
          <Typography variant="body2" color="text.secondary">Gastos asociados</Typography>
          {editMode ? (
            <TextField size="small" type="number" value={editExpenses || ''} sx={{ width: 100 }}
              onChange={(e) => setEditExpenses(e.target.value === '' ? 0 : Number(e.target.value))} />
          ) : (
            <Typography variant="body2">{purchase.associatedExpenses.toFixed(2)}</Typography>
          )}
        </Box>
        <Box display="flex" justifyContent="space-between" mt={0.5}>
          <Typography variant="subtitle1" fontWeight={700}>Total</Typography>
          <Typography variant="subtitle1" fontWeight={700}>
            {(editMode ? editTotal : purchase.total).toFixed(2)} {purchase.currency}
          </Typography>
        </Box>

        {editMode && (
          <Alert severity="info" sx={{ mt: 2 }}>
            Editar la compra reajusta el stock (revierte lo recibido antes y vuelve a recibir con los
            datos nuevos) y recalcula el costo promedio del producto.
          </Alert>
        )}
        {update.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo actualizar la compra (¿stock suficiente para revertir?).</Alert>}
        {purchase.status === 'Cancelled' && (
          <Alert severity="warning" sx={{ mt: 2 }}>
            Esta compra fue cancelada. Motivo: {purchase.cancellationReason || '—'}
          </Alert>
        )}
        {cancel.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo cancelar la compra (¿el producto ya no tiene stock suficiente?).</Alert>}
        {confirmCancel && (
          <TextField
            fullWidth size="small" autoFocus multiline minRows={2} sx={{ mt: 2 }}
            label="Motivo de la cancelación" placeholder="Explica por qué se cancela esta compra…"
            value={cancelReason} onChange={(e) => setCancelReason(e.target.value)}
          />
        )}
      </DialogContent>
      <DialogActions>
        {editMode ? (
          <>
            <Button onClick={() => setEditMode(false)}>Cancelar edición</Button>
            <Button variant="contained" disabled={!editValid || update.isPending} onClick={saveEdit}>Guardar cambios</Button>
          </>
        ) : (
          <>
            <Button onClick={onClose}>Cerrar</Button>
            {canEdit && !confirmCancel && <Button startIcon={<Edit />} onClick={startEdit}>Editar</Button>}
            {canCancel && purchase.status !== 'Cancelled' && !confirmCancel && (
              <Button color="error" startIcon={<Block />} onClick={() => setConfirmCancel(true)}>Cancelar compra</Button>
            )}
            {confirmCancel && (
              <>
                <Button onClick={() => { setConfirmCancel(false); setCancelReason(''); }}>Volver</Button>
                <Button color="error" variant="contained" disabled={!cancelReason.trim() || cancel.isPending} onClick={doCancel}>
                  Confirmar cancelación
                </Button>
              </>
            )}
          </>
        )}
      </DialogActions>
    </Dialog>
  );
}
