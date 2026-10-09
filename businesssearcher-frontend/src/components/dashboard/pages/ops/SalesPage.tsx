import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box, Card, CardContent, Typography, TextField, Chip, CircularProgress, Checkbox,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, IconButton, Tooltip, Paper,
  Dialog, DialogTitle, DialogContent, DialogActions, Button, Alert, Divider, Stack,
} from '@mui/material';
import { Receipt, Visibility, Undo, Edit, Print, Add, PointOfSale, Download, UploadFile, LocalShipping } from '@mui/icons-material';
import { useOpsSales, useRefundSale, useUpdateSale, useExportSalesReport, useGenerateOrdenEntrega } from '@/hooks/useOps';
import { useIsOpsAdmin, useHasOpsRole } from '@/hooks/useOpsRole';
import { useActiveStore } from '@/context/StoreContext';
import SaleReceipt from './SaleReceipt';
import SalesImportDialog from '../../dialogs/SalesImportDialog';
import type { OpsSale } from '@/lib/opsTypes';

const METHOD_LABEL: Record<string, string> = { Cash: 'Efectivo', Card: 'Tarjeta', Transfer: 'Transferencia', Mixed: 'Mixto' };

export default function SalesPage() {
  const navigate = useNavigate();
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const { storeId } = useActiveStore();
  const { data: sales, isLoading } = useOpsSales({ from: from || undefined, to: to || undefined, warehouseId: storeId || undefined });
  const [selected, setSelected] = useState<OpsSale | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const exportReport = useExportSalesReport();
  const [checkedIds, setCheckedIds] = useState<string[]>([]);
  const [ordenDialog, setOrdenDialog] = useState(false);
  const toggleChecked = (id: string) =>
    setCheckedIds((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  const allChecked = !!sales?.length && checkedIds.length === sales.length;
  const toggleAll = () => setCheckedIds(allChecked ? [] : (sales ?? []).map((s) => s.id));

  return (
    <Box>
      <Box display="flex" alignItems="center" justifyContent="space-between" flexWrap="wrap" gap={2} mb={3}>
        <Box display="flex" alignItems="center" gap={1}>
          <Receipt color="primary" />
          <Typography variant="h5" fontWeight={700}>Ventas</Typography>
        </Box>
        <Box display="flex" gap={1} flexWrap="wrap">
          <Button
            variant="outlined"
            startIcon={<UploadFile />}
            onClick={() => setImportOpen(true)}
          >
            Importar
          </Button>
          <Button
            variant="outlined"
            startIcon={<Download />}
            disabled={exportReport.isPending}
            onClick={() => exportReport.mutate({ from: from || undefined, to: to || undefined })}
          >
            Exportar
          </Button>
          <Button
            variant="outlined"
            startIcon={<LocalShipping />}
            disabled={checkedIds.length === 0}
            onClick={() => setOrdenDialog(true)}
          >
            Orden de entrega{checkedIds.length > 0 ? ` (${checkedIds.length})` : ''}
          </Button>
          <Button
            variant="contained"
            startIcon={<Add />}
            onClick={() => navigate('/dashboard/ops/pos')}
          >
            Nueva venta
          </Button>
        </Box>
      </Box>

      <Alert severity="info" icon={<PointOfSale fontSize="small" />} sx={{ mb: 2 }}>
        Las ventas se registran desde <strong>Punto de Venta</strong>. Aquí ves el historial y podés editar, reembolsar o imprimir cada una.
      </Alert>

      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
          <TextField size="small" type="date" label="Desde" InputLabelProps={{ shrink: true }} value={from} onChange={(e) => setFrom(e.target.value)} />
          <TextField size="small" type="date" label="Hasta" InputLabelProps={{ shrink: true }} value={to} onChange={(e) => setTo(e.target.value)} />
        </CardContent>
      </Card>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell padding="checkbox">
                  <Checkbox size="small" checked={allChecked} indeterminate={checkedIds.length > 0 && !allChecked}
                    onChange={toggleAll} />
                </TableCell>
                <TableCell>Fecha</TableCell><TableCell>Cajero</TableCell><TableCell>Almacén</TableCell>
                <TableCell>Código del Gestor</TableCell><TableCell align="right">Items</TableCell>
                <TableCell>Método</TableCell><TableCell align="right">Total</TableCell><TableCell>Estado</TableCell>
                <TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(sales ?? []).map((s) => (
                <TableRow key={s.id} hover selected={checkedIds.includes(s.id)}>
                  <TableCell padding="checkbox">
                    <Checkbox size="small" checked={checkedIds.includes(s.id)} onChange={() => toggleChecked(s.id)} />
                  </TableCell>
                  <TableCell>{new Date(s.date).toLocaleString('es-ES')}</TableCell>
                  <TableCell>{s.cashierName ?? '—'}</TableCell>
                  <TableCell>{s.warehouseName ?? '—'}</TableCell>
                  <TableCell>{s.managerCode ?? '—'}</TableCell>
                  <TableCell align="right">{s.items.length}</TableCell>
                  <TableCell>{METHOD_LABEL[s.paymentMethod] ?? s.paymentMethod}</TableCell>
                  <TableCell align="right">{s.total.toFixed(2)} {s.paymentCurrency}</TableCell>
                  <TableCell>
                    <Chip size="small" label={s.status === 'Refunded' ? 'Reembolsada' : 'Completada'}
                      color={s.status === 'Refunded' ? 'error' : 'success'} variant="outlined" />
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title="Ver detalle">
                      <IconButton size="small" onClick={() => setSelected(s)}><Visibility fontSize="small" /></IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
              {(sales ?? []).length === 0 && (
                <TableRow><TableCell colSpan={10} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin ventas registradas</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {selected && <SaleDetailDialog sale={selected} onClose={() => setSelected(null)} />}
      <SalesImportDialog open={importOpen} onClose={() => setImportOpen(false)} />
      {ordenDialog && (
        <OrdenEntregaDialog
          saleIds={checkedIds}
          onClose={() => setOrdenDialog(false)}
          onGenerated={() => { setOrdenDialog(false); setCheckedIds([]); }}
        />
      )}
    </Box>
  );
}

function OrdenEntregaDialog({ saleIds, onClose, onGenerated }: {
  saleIds: string[]; onClose: () => void; onGenerated: () => void;
}) {
  const generate = useGenerateOrdenEntrega();
  const [cliente, setCliente] = useState('');
  const [ci, setCi] = useState('');
  const [telefono, setTelefono] = useState('');
  const [direccion, setDireccion] = useState('');
  const [domicilio, setDomicilio] = useState<number | ''>('');

  const submit = () => {
    generate.mutate({
      saleIds,
      cliente: cliente || undefined, ci: ci || undefined, telefono: telefono || undefined,
      direccion: direccion || undefined, domicilio: domicilio === '' ? undefined : domicilio,
    }, { onSuccess: onGenerated });
  };

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Orden de entrega — {saleIds.length} venta{saleIds.length !== 1 ? 's' : ''}</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" mb={2}>
          Junta los productos de las ventas seleccionadas en un solo documento descargable (.xlsx).
          Los datos de abajo son opcionales y solo se usan para este documento.
        </Typography>
        <Stack spacing={1.5}>
          <TextField size="small" label="Cliente" value={cliente} onChange={(e) => setCliente(e.target.value)} />
          <Stack direction="row" spacing={1.5}>
            <TextField size="small" label="CI" value={ci} onChange={(e) => setCi(e.target.value)} fullWidth />
            <TextField size="small" label="Teléfono" value={telefono} onChange={(e) => setTelefono(e.target.value)} fullWidth />
          </Stack>
          <TextField size="small" label="Dirección" value={direccion} onChange={(e) => setDireccion(e.target.value)} />
          <TextField size="small" type="number" label="Domicilio (costo de envío)" value={domicilio}
            onChange={(e) => setDomicilio(e.target.value === '' ? '' : Number(e.target.value))} />
        </Stack>
        {generate.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo generar la orden de entrega.</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" startIcon={<LocalShipping />} disabled={generate.isPending} onClick={submit}>
          {generate.isPending ? 'Generando...' : 'Descargar'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function SaleDetailDialog({ sale, onClose }: { sale: OpsSale; onClose: () => void }) {
  const refund = useRefundSale();
  const update = useUpdateSale();
  const isAdmin = useIsOpsAdmin();
  const canRefund = useHasOpsRole('JefeDeTurno');
  const [confirmRefund, setConfirmRefund] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [editQty, setEditQty] = useState<number[]>(() => sale.items.map((it) => it.quantity));

  const doRefund = () => refund.mutate(sale.id, { onSuccess: onClose });

  const canEdit = isAdmin && sale.status !== 'Refunded';

  const startEdit = () => { setEditQty(sale.items.map((it) => it.quantity)); setEditMode(true); };
  const saveEdit = () => {
    const dto = {
      items: sale.items.map((it, i) => ({
        productId: it.productId, warehouseId: it.warehouseId, quantity: editQty[i],
        unitPriceOverride: it.unitPrice, discountType: it.discountType as 'Amount' | 'Percentage', discountValue: it.discountValue,
      })),
    };
    update.mutate({ id: sale.id, dto }, { onSuccess: () => setEditMode(false) });
  };
  const editValid = editQty.every((q) => q > 0);

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <SaleReceipt sale={sale} />
      <DialogTitle>Venta del {new Date(sale.date).toLocaleString('es-ES')}</DialogTitle>
      <DialogContent dividers>
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Producto</TableCell><TableCell align="right">Cant.</TableCell>
                <TableCell align="right">P. Unit.</TableCell><TableCell align="right">Subtotal</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {sale.items.map((it, i) => (
                <TableRow key={i}>
                  <TableCell>{it.productName}</TableCell>
                  <TableCell align="right">
                    {editMode ? (
                      <TextField size="small" type="number" value={editQty[i]} sx={{ width: 80 }}
                        onChange={(e) => setEditQty((qs) => qs.map((q, qi) => (qi === i ? Math.max(1, Number(e.target.value)) : q)))} />
                    ) : it.quantity}
                  </TableCell>
                  <TableCell align="right">{it.unitPrice.toFixed(2)}</TableCell>
                  <TableCell align="right">{(it.unitPrice * (editMode ? editQty[i] : it.quantity)).toFixed(2)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>

        <Divider sx={{ my: 2 }} />

        <Box display="flex" justifyContent="space-between">
          <Typography variant="body2" color="text.secondary">Subtotal</Typography>
          <Typography variant="body2">{sale.subtotal.toFixed(2)}</Typography>
        </Box>
        <Box display="flex" justifyContent="space-between">
          <Typography variant="body2" color="text.secondary">Descuento</Typography>
          <Typography variant="body2">{sale.discount.toFixed(2)}</Typography>
        </Box>
        <Box display="flex" justifyContent="space-between" mt={0.5}>
          <Typography variant="subtitle1" fontWeight={700}>Total</Typography>
          <Typography variant="subtitle1" fontWeight={700}>{sale.total.toFixed(2)} {sale.paymentCurrency}</Typography>
        </Box>

        <Divider sx={{ my: 2 }} />

        <Typography variant="body2" fontWeight={600} gutterBottom>Pagos</Typography>
        {sale.payments.map((p, i) => (
          <Box key={i} display="flex" justifyContent="space-between">
            <Typography variant="body2" color="text.secondary">{METHOD_LABEL[p.method] ?? p.method}</Typography>
            <Typography variant="body2">{p.amount.toFixed(2)} {p.currency}</Typography>
          </Box>
        ))}

        {editMode && (
          <Alert severity="info" sx={{ mt: 2 }}>
            Editar cantidades reajusta el stock (restaura lo anterior y descuenta lo nuevo), pero no
            modifica los pagos ya registrados de esta venta.
          </Alert>
        )}
        {update.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo actualizar la venta (¿stock suficiente?).</Alert>}
        {refund.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo reembolsar la venta.</Alert>}
        {sale.status === 'Refunded' && <Alert severity="warning" sx={{ mt: 2 }}>Esta venta ya fue reembolsada.</Alert>}
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
            <Button startIcon={<Print />} onClick={() => window.print()}>Imprimir</Button>
            {canEdit && <Button startIcon={<Edit />} onClick={startEdit}>Editar</Button>}
            {canRefund && sale.status !== 'Refunded' && !confirmRefund && (
              <Button color="error" startIcon={<Undo />} onClick={() => setConfirmRefund(true)}>Reembolsar</Button>
            )}
            {confirmRefund && (
              <>
                <Button onClick={() => setConfirmRefund(false)}>Cancelar</Button>
                <Button color="error" variant="contained" disabled={refund.isPending} onClick={doRefund}>Confirmar reembolso</Button>
              </>
            )}
          </>
        )}
      </DialogActions>
    </Dialog>
  );
}
