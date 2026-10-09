import { useRef, useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, IconButton, Chip, CircularProgress,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, Grid, FormControlLabel, Switch, Alert,
  InputAdornment, Tooltip, Stack, Avatar, Snackbar, Popper, Paper,
} from '@mui/material';
import {
  Add, Edit, Delete, Inventory2, Category as CategoryIcon,
  UploadFile, Public, PublicOff, Image as ImageIcon, PictureAsPdf,
} from '@mui/icons-material';
import {
  useOpsProducts, useSaveOpsProduct, useDeleteOpsProduct, useAdjustStock,
  useWarehouses, useOpsCategories, useSaveOpsCategory,
  useSetProductPublicVisibility, useUploadOpsProductImage, useExchangeRate,
} from '@/hooks/useOps';
import type { OpsProduct, CreateOpsProduct } from '@/lib/opsTypes';
import ImportProductsDialog from '@/components/dashboard/dialogs/ImportProductsDialog';
import ExportInventoryPdfDialog from '@/components/dashboard/dialogs/ExportInventoryPdfDialog';
import { useHasOpsRole } from '@/hooks/useOpsRole';
import { useActiveStore } from '@/context/StoreContext';

export default function InventoryPage() {
  // Cada operación de Inventario es siempre sobre la tienda activa: no hay selector de almacén
  // ni vista "todos" aquí (eso quedó resuelto por el selector global de tienda), y tampoco un
  // CRUD de almacenes suelto en esta página — las tiendas se crean/eligen desde ese selector.
  const { storeId } = useActiveStore();
  const warehouseId = storeId ?? '';
  const [search, setSearch] = useState('');
  const [lowStockOnly, setLowStockOnly] = useState(false);

  const { data: warehouses } = useWarehouses();
  const { data: categories } = useOpsCategories();
  const { data: products, isLoading } = useOpsProducts({
    search: search || undefined,
    warehouseId: warehouseId || undefined,
    lowStockOnly: lowStockOnly || undefined,
  });

  const [productDialog, setProductDialog] = useState(false);
  const [editProduct, setEditProduct] = useState<OpsProduct | null>(null);
  const [stockDialog, setStockDialog] = useState<OpsProduct | null>(null);
  const [catDialog, setCatDialog] = useState(false);
  const [importDialog, setImportDialog] = useState(false);
  const [exportPdfDialog, setExportPdfDialog] = useState(false);
  const [importNotice, setImportNotice] = useState<{ severity: 'success' | 'warning'; lines: string[] } | null>(null);

  const del = useDeleteOpsProduct();
  const setVisibility = useSetProductPublicVisibility();
  const canManageCatalog = useHasOpsRole('Almacenero', 'JefeDeTurno');

  const stockFor = (p: OpsProduct) =>
    warehouseId ? (p.stocks.find((s) => s.warehouseId === warehouseId)?.quantity ?? 0) : p.totalStock;

  const catName = (id?: string) => categories?.find((c) => c.id === id)?.name ?? '—';

  const noWarehouses = warehouses && warehouses.length === 0;

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" flexWrap="wrap" gap={2} mb={3}>
        <Typography variant="h5" fontWeight={700}>Inventario</Typography>
        <Stack direction="row" spacing={1.5} flexWrap="wrap">
          <Button startIcon={<CategoryIcon />} variant="outlined" onClick={() => setCatDialog(true)}>Categorías</Button>
          <Button startIcon={<PictureAsPdf />} variant="outlined" onClick={() => setExportPdfDialog(true)}>Exportar PDF</Button>
          {canManageCatalog && (
            <>
              <Button startIcon={<UploadFile />} variant="outlined" onClick={() => setImportDialog(true)}>Importar productos</Button>
              <Button startIcon={<Add />} variant="contained" disabled={noWarehouses}
                onClick={() => { setEditProduct(null); setProductDialog(true); }}>Nuevo producto</Button>
            </>
          )}
        </Stack>
      </Box>

      {noWarehouses && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Crea al menos un <strong>almacén</strong> antes de registrar productos.
        </Alert>
      )}

      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
          <TextField size="small" label="Buscar (nombre o código)" value={search}
            onChange={(e) => setSearch(e.target.value)} sx={{ minWidth: 240 }} />
          <FormControlLabel control={<Switch checked={lowStockOnly} onChange={(e) => setLowStockOnly(e.target.checked)} />}
            label="Solo bajo stock" />
        </CardContent>
      </Card>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell></TableCell>
                <TableCell>Producto</TableCell>
                <TableCell>Código</TableCell>
                <TableCell>Categoría</TableCell>
                <TableCell align="right">Costo</TableCell>
                <TableCell align="right">Venta</TableCell>
                <TableCell align="right">Stock</TableCell>
                <TableCell align="center">En la app</TableCell>
                <TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(products ?? []).map((p) => {
                const st = stockFor(p);
                const low = st <= p.minStock;
                return (
                  <TableRow key={p.id} hover>
                    <TableCell>
                      <ProductImage src={p.imageUrl} size={32} />
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" fontWeight={600}>{p.name}</Typography>
                      {!p.forSale && <Chip label="No a la venta" size="small" sx={{ mt: 0.5 }} />}
                    </TableCell>
                    <TableCell>{p.barcode ?? '—'}</TableCell>
                    <TableCell>{catName(p.categoryId)}</TableCell>
                    <TableCell align="right">{p.costPriceUSD != null ? `$${p.costPriceUSD.toFixed(2)}` : '—'}</TableCell>
                    <TableCell align="right">{p.sellPriceUSD != null ? `$${p.sellPriceUSD.toFixed(2)}` : '—'}</TableCell>
                    <TableCell align="right">
                      <Chip size="small" color={low ? 'warning' : 'default'} label={st}
                        variant={low ? 'filled' : 'outlined'} />
                    </TableCell>
                    <TableCell align="center">
                      <Tooltip title={p.isPubliclyVisible ? 'Visible en la búsqueda pública (clic para ocultar)' : 'Oculto para la app (clic para publicar)'}>
                        <span>
                          <IconButton
                            size="small"
                            color={p.isPubliclyVisible ? 'success' : 'default'}
                            disabled={setVisibility.isPending || !canManageCatalog}
                            onClick={() => setVisibility.mutate({ id: p.id, isPubliclyVisible: !p.isPubliclyVisible })}
                          >
                            {p.isPubliclyVisible ? <Public fontSize="small" /> : <PublicOff fontSize="small" />}
                          </IconButton>
                        </span>
                      </Tooltip>
                    </TableCell>
                    <TableCell align="right">
                      <Tooltip title="Ajustar stock"><IconButton size="small" onClick={() => setStockDialog(p)}><Inventory2 fontSize="small" /></IconButton></Tooltip>
                      {canManageCatalog && (
                        <>
                          <Tooltip title="Editar"><IconButton size="small" onClick={() => { setEditProduct(p); setProductDialog(true); }}><Edit fontSize="small" /></IconButton></Tooltip>
                          <Tooltip title="Eliminar"><IconButton size="small" color="error" onClick={() => del.mutate(p.id)}><Delete fontSize="small" /></IconButton></Tooltip>
                        </>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
              {(products ?? []).length === 0 && (
                <TableRow><TableCell colSpan={9} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin productos</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {productDialog && (
        <ProductDialog product={editProduct} onClose={() => setProductDialog(false)} />
      )}
      {stockDialog && (
        <AdjustStockDialog product={stockDialog} defaultWarehouse={warehouseId} onClose={() => setStockDialog(null)} />
      )}
      {catDialog && <CategoriesDialog onClose={() => setCatDialog(false)} />}
      {exportPdfDialog && (
        <ExportInventoryPdfDialog
          products={products ?? []}
          categories={categories}
          catName={catName}
          stockFor={stockFor}
          storeName={warehouses?.find((w) => w.id === storeId)?.name}
          onClose={() => setExportPdfDialog(false)}
        />
      )}
      {importDialog && (
        <ImportProductsDialog
          open
          onClose={() => setImportDialog(false)}
          onImported={(res) => setImportNotice({
            severity: 'success',
            lines: [
              `Importación completada: ${res.createdCount} creados, ${res.addedCount} con stock sumado, ${res.reactivatedCount} reactivados, ${res.pricesUpdatedCount} con precio propio en este almacén, ${res.categoriesCreatedCount} categorías nuevas.`,
              ...res.warnings,
            ],
          })}
        />
      )}
      <Snackbar
        open={!!importNotice}
        autoHideDuration={10000}
        onClose={() => setImportNotice(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        {importNotice ? (
          <Alert severity={importNotice.severity} variant="filled" onClose={() => setImportNotice(null)} sx={{ maxWidth: 640 }}>
            {importNotice.lines.map((l, i) => <Typography key={i} variant="body2">{l}</Typography>)}
          </Alert>
        ) : <span />}
      </Snackbar>
    </Box>
  );
}

/** Foto del producto: al pasar el mouse por encima se ve más grande en una vista flotante,
 * y al tocarla se amplía en un diálogo a pantalla casi completa. */
function ProductImage({ src, size }: { src?: string; size: number }) {
  const [zoomed, setZoomed] = useState(false);
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  return (
    <>
      <Avatar
        src={src}
        variant="rounded"
        sx={{ width: size, height: size, cursor: src ? 'zoom-in' : 'default' }}
        onClick={() => src && setZoomed(true)}
        onMouseEnter={(e) => src && setAnchor(e.currentTarget)}
        onMouseLeave={() => setAnchor(null)}
      >
        <ImageIcon sx={{ fontSize: size * 0.5 }} />
      </Avatar>
      {src && (
        <Popper open={!!anchor} anchorEl={anchor} placement="right-start" sx={{ zIndex: 1300, pointerEvents: 'none' }}
          modifiers={[{ name: 'offset', options: { offset: [0, 8] } }]}>
          <Paper elevation={6} sx={{ p: 0.5, lineHeight: 0 }}>
            <Box component="img" src={src} alt="" sx={{ width: 220, height: 220, objectFit: 'contain', display: 'block' }} />
          </Paper>
        </Popper>
      )}
      {zoomed && src && (
        <Dialog open onClose={() => setZoomed(false)} maxWidth="lg">
          <Box
            component="img"
            src={src}
            alt=""
            onClick={() => setZoomed(false)}
            sx={{ maxWidth: '90vw', maxHeight: '90vh', display: 'block', cursor: 'zoom-out' }}
          />
        </Dialog>
      )}
    </>
  );
}

function ProductDialog({ product, onClose }: { product: OpsProduct | null; onClose: () => void }) {
  const { data: categories } = useOpsCategories();
  const { data: warehouses } = useWarehouses();
  const { data: rate } = useExchangeRate();
  const save = useSaveOpsProduct();
  const setVisibility = useSetProductPublicVisibility();
  const uploadImage = useUploadOpsProductImage();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [form, setForm] = useState<CreateOpsProduct>({
    name: product?.name ?? '',
    barcode: product?.barcode ?? '',
    unit: product?.unit ?? 'unidad',
    categoryId: product?.categoryId ?? '',
    costPrice: product?.costPrice ?? 0,
    sellPrice: product?.sellPrice ?? 0,
    costPriceUSD: product?.costPriceUSD,
    sellPriceUSD: product?.sellPriceUSD,
    minStock: product?.minStock ?? 0,
    taxRate: product?.taxRate,
    forSale: product?.forSale ?? true,
    initialWarehouseId: warehouses?.[0]?.id,
    initialStock: 0,
    minOrderQuantity: product?.minOrderQuantity ?? 1,
  });
  const [isPubliclyVisible, setIsPubliclyVisible] = useState(product?.isPubliclyVisible ?? true);

  const set = (k: keyof CreateOpsProduct, v: unknown) => setForm((f) => ({ ...f, [k]: v }));
  const num = (v: string) => (v === '' ? undefined : Number(v));

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file && product) uploadImage.mutate({ id: product.id, file });
  };

  const handleVisibilityToggle = (checked: boolean) => {
    setIsPubliclyVisible(checked);
    if (product) setVisibility.mutate({ id: product.id, isPubliclyVisible: checked });
  };

  // Ventas solo en USD por ahora: el formulario ya no pide costo/precio en CUP, se derivan
  // solos de su valor en USD y la tasa de cambio vigente (el dominio todavía guarda ambos).
  const cupFrom = (usd?: number) => (usd != null && rate?.rate ? Math.round(usd * rate.rate * 100) / 100 : 0);

  const submit = () => {
    const dto: CreateOpsProduct = {
      ...form,
      costPrice: cupFrom(form.costPriceUSD),
      sellPrice: cupFrom(form.sellPriceUSD),
      categoryId: form.categoryId || undefined,
      barcode: form.barcode || undefined,
      // en edición, la visibilidad se actualiza aparte (toggle en vivo más abajo)
      ...(product ? {} : { isPubliclyVisible }),
    };
    save.mutate({ id: product?.id, dto }, { onSuccess: onClose });
  };

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{product ? 'Editar producto' : 'Nuevo producto'}</DialogTitle>
      <DialogContent>
        <Box display="flex" alignItems="center" gap={2} mb={2} flexWrap="wrap">
          {product && (
            <>
              <ProductImage src={product.imageUrl} size={120} />
              <input ref={fileInputRef} type="file" accept="image/*" hidden onChange={handleFileChange} />
              <Button size="small" variant="outlined" startIcon={<UploadFile />}
                disabled={uploadImage.isPending} onClick={() => fileInputRef.current?.click()}>
                {uploadImage.isPending ? 'Subiendo...' : 'Subir foto'}
              </Button>
            </>
          )}
          <FormControlLabel
            control={
              <Switch
                checked={isPubliclyVisible}
                onChange={(e) => (product ? handleVisibilityToggle(e.target.checked) : setIsPubliclyVisible(e.target.checked))}
              />
            }
            label="Visible en la búsqueda pública (app)"
          />
        </Box>
        <Grid container spacing={2} sx={{ mt: 0 }}>
          <Grid item xs={12} sm={8}><TextField fullWidth size="small" label="Nombre" value={form.name} onChange={(e) => set('name', e.target.value)} /></Grid>
          <Grid item xs={12} sm={4}><TextField fullWidth size="small" label="Código de barras" value={form.barcode} onChange={(e) => set('barcode', e.target.value)} /></Grid>
          <Grid item xs={6} sm={4}><TextField fullWidth size="small" label="Unidad" value={form.unit} onChange={(e) => set('unit', e.target.value)} /></Grid>
          <Grid item xs={6} sm={4}>
            <TextField fullWidth size="small" select label="Categoría" value={form.categoryId} onChange={(e) => set('categoryId', e.target.value)}>
              <MenuItem value="">Sin categoría</MenuItem>
              {categories?.map((c) => <MenuItem key={c.id} value={c.id}>{c.name}</MenuItem>)}
            </TextField>
          </Grid>
          <Grid item xs={6} sm={4}><TextField fullWidth size="small" type="number" label="Stock mínimo" value={form.minStock || ''} onChange={(e) => set('minStock', e.target.value === '' ? 0 : Number(e.target.value))} /></Grid>

          <Grid item xs={6} sm={6}><TextField fullWidth size="small" type="number" label="Costo" value={form.costPriceUSD ?? ''} onChange={(e) => set('costPriceUSD', num(e.target.value))} InputProps={{ startAdornment: <InputAdornment position="start">$</InputAdornment> }} /></Grid>
          <Grid item xs={6} sm={6}><TextField fullWidth size="small" type="number" label="Precio de venta" value={form.sellPriceUSD ?? ''} onChange={(e) => set('sellPriceUSD', num(e.target.value))} InputProps={{ startAdornment: <InputAdornment position="start">$</InputAdornment> }} /></Grid>

          <Grid item xs={6} sm={4}><TextField fullWidth size="small" type="number" label="Impuesto %" value={form.taxRate ?? ''} onChange={(e) => set('taxRate', num(e.target.value))} /></Grid>
          <Grid item xs={6} sm={4}><TextField fullWidth size="small" type="number" label="Venta mínima (mayorista)" value={form.minOrderQuantity || ''} onChange={(e) => set('minOrderQuantity', e.target.value === '' ? 1 : Number(e.target.value))} /></Grid>

          {!product && (
            <>
              <Grid item xs={6} sm={4}>
                <TextField fullWidth size="small" select label="Almacén inicial" value={form.initialWarehouseId ?? ''} onChange={(e) => set('initialWarehouseId', e.target.value)}>
                  {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
                </TextField>
              </Grid>
              <Grid item xs={6} sm={4}><TextField fullWidth size="small" type="number" label="Stock inicial" value={form.initialStock || ''} onChange={(e) => set('initialStock', e.target.value === '' ? 0 : Number(e.target.value))} /></Grid>
            </>
          )}
          <Grid item xs={12}><FormControlLabel control={<Switch checked={form.forSale ?? true} onChange={(e) => set('forSale', e.target.checked)} />} label="Disponible para la venta" /></Grid>
        </Grid>
        {save.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo guardar el producto.</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" onClick={submit} disabled={!form.name || save.isPending}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}

function AdjustStockDialog({ product, defaultWarehouse, onClose }: { product: OpsProduct; defaultWarehouse: string; onClose: () => void }) {
  // Se ajusta siempre el stock de la tienda activa (defaultWarehouse ya viene de ahí): no se
  // puede elegir otro almacén, cada tienda administra solo su propio stock.
  const warehouseId = defaultWarehouse;
  const adjust = useAdjustStock();
  const [delta, setDelta] = useState<number | ''>('');
  const deltaValue = delta === '' ? 0 : delta;

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Ajustar stock — {product.name}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField size="small" type="number" label="Cantidad (+ entra, − sale)" value={delta}
            onChange={(e) => setDelta(e.target.value === '' ? '' : Number(e.target.value))}
            InputProps={{ startAdornment: <InputAdornment position="start">Δ</InputAdornment> }} />
        </Stack>
        {adjust.isError && <Alert severity="error" sx={{ mt: 2 }}>Stock insuficiente o error.</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" disabled={!warehouseId || deltaValue === 0 || adjust.isPending}
          onClick={() => adjust.mutate({ id: product.id, warehouseId, delta: deltaValue }, { onSuccess: onClose })}>Aplicar</Button>
      </DialogActions>
    </Dialog>
  );
}

function CategoriesDialog({ onClose }: { onClose: () => void }) {
  const { data: categories } = useOpsCategories();
  const save = useSaveOpsCategory();
  const canManageCatalog = useHasOpsRole('Almacenero', 'JefeDeTurno');
  const [name, setName] = useState('');
  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Categorías</DialogTitle>
      <DialogContent>
        {canManageCatalog && (
          <Stack direction="row" spacing={1} sx={{ mt: 1, mb: 2 }}>
            <TextField size="small" fullWidth label="Nueva categoría" value={name} onChange={(e) => setName(e.target.value)} />
            <Button variant="contained" disabled={!name || save.isPending} onClick={() => save.mutate({ name }, { onSuccess: () => setName('') })}>Añadir</Button>
          </Stack>
        )}
        <Stack spacing={0.5}>
          {categories?.map((c) => <Chip key={c.id} label={c.name} sx={{ justifyContent: 'flex-start' }} />)}
          {(categories ?? []).length === 0 && <Typography variant="body2" color="text.secondary">Sin categorías.</Typography>}
        </Stack>
      </DialogContent>
      <DialogActions><Button onClick={onClose}>Cerrar</Button></DialogActions>
    </Dialog>
  );
}

