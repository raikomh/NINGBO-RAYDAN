import { useEffect, useMemo, useState } from 'react';
import {
  Box, Card, CardContent, Typography, TextField, MenuItem, Button, IconButton, Chip,
  List, ListItem, ListItemText, Divider, Stack, Grid, Alert, InputAdornment, ToggleButton,
  ToggleButtonGroup, Dialog, DialogTitle, DialogContent, DialogActions, CircularProgress,
} from '@mui/material';
import { Add, Remove, DeleteOutline, PointOfSale, Search, Print, CheckCircle } from '@mui/icons-material';
import { useOpsProducts, useWarehouses, useCurrentCashRegister, useCreateSale, useExchangeRate, useTerminals, fetchProductByBarcode } from '@/hooks/useOps';
import type { OpsProduct, OpsCurrency, OpsPaymentMethod, CreateOpsSale, OpsSale } from '@/lib/opsTypes';
import { useNavigate } from 'react-router-dom';
import SaleReceipt from './SaleReceipt';

interface CartLine {
  product: OpsProduct;
  quantity: number;
  discountType: 'Amount' | 'Percentage';
  discountValue: number;
}

export default function PosPage() {
  const navigate = useNavigate();
  const { data: warehouses } = useWarehouses();
  const { data: register } = useCurrentCashRegister();
  const { data: rate } = useExchangeRate();
  const { data: terminals } = useTerminals();
  const [warehouseId, setWarehouseId] = useState('');
  const [terminalId, setTerminalId] = useState('');
  const [search, setSearch] = useState('');

  // Al abrir/cambiar la caja, preselecciona la terminal con la que se abrió (si tiene una).
  useEffect(() => { setTerminalId(register?.terminalId ?? ''); }, [register?.id]);
  const { data: products, isLoading } = useOpsProducts({ search: search || undefined, warehouseId: warehouseId || undefined });

  const [cart, setCart] = useState<CartLine[]>([]);
  const [currency, setCurrency] = useState<OpsCurrency>('CUP');
  const [payDialog, setPayDialog] = useState(false);
  const [lastSale, setLastSale] = useState<OpsSale | null>(null);
  const createSale = useCreateSale();

  const effectiveWarehouse = warehouseId || warehouses?.[0]?.id || '';

  // Precio del punto de venta seleccionado: el propio del almacén si lo tiene, o el general del producto.
  const priceAt = (p: OpsProduct) => {
    const s = p.stocks.find((x) => x.warehouseId === effectiveWarehouse);
    return {
      cup: s?.sellPrice ?? p.sellPrice,
      usd: s?.sellPriceUSD ?? p.sellPriceUSD,
    };
  };
  const priceOf = (p: OpsProduct) => {
    const { cup, usd } = priceAt(p);
    return currency === 'USD' && usd != null ? usd : cup;
  };
  const stockOf = (p: OpsProduct) =>
    (p.stocks.find((s) => s.warehouseId === effectiveWarehouse)?.quantity ?? p.totalStock);

  // Lectora de código de barras: al presionar Enter, intenta un match exacto por
  // barcode y agrega directo al carrito (flujo típico de pistola lectora).
  const handleSearchKeyDown = async (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key !== 'Enter' || !search.trim()) return;
    try {
      const match = await fetchProductByBarcode(search.trim());
      if (match && match.forSale) {
        addToCart(match);
        setSearch('');
      }
    } catch {
      // sin match por barcode: se mantiene el filtro de texto normal
    }
  };

  const addToCart = (p: OpsProduct) => {
    setCart((c) => {
      const existing = c.find((l) => l.product.id === p.id);
      if (existing) return c.map((l) => (l.product.id === p.id ? { ...l, quantity: l.quantity + 1 } : l));
      return [...c, { product: p, quantity: 1, discountType: 'Amount', discountValue: 0 }];
    });
  };
  const setQty = (id: string, q: number) =>
    setCart((c) => c.map((l) => (l.product.id === id ? { ...l, quantity: Math.max(1, q) } : l)));
  const setDisc = (id: string, v: number) =>
    setCart((c) => c.map((l) => (l.product.id === id ? { ...l, discountValue: Math.max(0, v) } : l)));
  const remove = (id: string) => setCart((c) => c.filter((l) => l.product.id !== id));

  const lineTotal = (l: CartLine) => {
    const gross = priceOf(l.product) * l.quantity;
    const disc = l.discountType === 'Percentage' ? gross * (l.discountValue / 100) : l.discountValue;
    return Math.max(0, gross - disc);
  };
  const subtotal = useMemo(() => cart.reduce((s, l) => s + priceOf(l.product) * l.quantity, 0), [cart, currency]);
  const total = useMemo(() => cart.reduce((s, l) => s + lineTotal(l), 0), [cart, currency]);

  const canSell = !!register && register.status === 'Open' && cart.length > 0 && !!effectiveWarehouse;

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" flexWrap="wrap" gap={2} mb={2}>
        <Typography variant="h5" fontWeight={700}>Punto de Venta</Typography>
        <Stack direction="row" spacing={1} alignItems="center">
          {rate && <Chip label={`Tasa: ${rate.rate} CUP/USD`} size="small" variant="outlined" />}
          {register?.status === 'Open'
            ? <Chip color="success" label="Caja abierta" />
            : <Chip color="warning" label="Caja cerrada" onClick={() => navigate('/dashboard/ops/cash')} />}
        </Stack>
      </Box>

      {(!register || register.status !== 'Open') && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Debes <strong>abrir la caja</strong> antes de vender. Ve a <Button size="small" onClick={() => navigate('/dashboard/ops/cash')}>Caja</Button>
        </Alert>
      )}

      <Grid container spacing={2}>
        {/* Catálogo */}
        <Grid item xs={12} md={7}>
          <Card variant="outlined">
            <CardContent>
              <Stack direction="row" spacing={1.5} mb={2}>
                <TextField size="small" fullWidth placeholder="Buscar producto o escanear código…" value={search}
                  onChange={(e) => setSearch(e.target.value)} onKeyDown={handleSearchKeyDown}
                  InputProps={{ startAdornment: <InputAdornment position="start"><Search fontSize="small" /></InputAdornment> }} />
                <TextField size="small" select label="Almacén" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} sx={{ minWidth: 150 }}>
                  <MenuItem value="">{warehouses?.[0]?.name ?? 'Principal'}</MenuItem>
                  {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
                </TextField>
                <TextField size="small" select label="Terminal" value={terminalId} onChange={(e) => setTerminalId(e.target.value)} sx={{ minWidth: 150 }}>
                  <MenuItem value="">— Sin asignar —</MenuItem>
                  {terminals?.filter((t) => t.isActive).map((t) => <MenuItem key={t.id} value={t.id}>{t.name}</MenuItem>)}
                </TextField>
              </Stack>
              {isLoading ? (
                <Box display="flex" justifyContent="center" py={4}><CircularProgress /></Box>
              ) : (
                <Grid container spacing={1}>
                  {(products ?? []).filter((p) => p.forSale).map((p) => {
                    const st = stockOf(p);
                    return (
                      <Grid item xs={6} sm={4} key={p.id}>
                        <Card variant="outlined" sx={{ cursor: st > 0 ? 'pointer' : 'not-allowed', opacity: st > 0 ? 1 : 0.5, height: '100%' }}
                          onClick={() => st > 0 && addToCart(p)}>
                          <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                            <Typography variant="body2" fontWeight={600} noWrap>{p.name}</Typography>
                            <Typography variant="caption" color="text.secondary" display="block">
                              {currency === 'USD' && priceAt(p).usd != null ? `$${priceAt(p).usd!.toFixed(2)}` : `${priceAt(p).cup.toFixed(2)} CUP`}
                            </Typography>
                            <Chip size="small" label={`Stock ${st}`} color={st <= p.minStock ? 'warning' : 'default'} sx={{ mt: 0.5 }} />
                          </CardContent>
                        </Card>
                      </Grid>
                    );
                  })}
                  {(products ?? []).length === 0 && <Grid item xs={12}><Typography color="text.secondary" py={3} textAlign="center">Sin productos</Typography></Grid>}
                </Grid>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Carrito */}
        <Grid item xs={12} md={5}>
          <Card variant="outlined" sx={{ position: 'sticky', top: 16 }}>
            <CardContent>
              <Box display="flex" justifyContent="space-between" alignItems="center" mb={1}>
                <Typography variant="h6" fontWeight={700}>Carrito</Typography>
                <ToggleButtonGroup size="small" exclusive value={currency} onChange={(_, v) => v && setCurrency(v)}>
                  <ToggleButton value="CUP">CUP</ToggleButton>
                  <ToggleButton value="USD">USD</ToggleButton>
                </ToggleButtonGroup>
              </Box>
              <Divider />
              <List dense sx={{ maxHeight: 340, overflow: 'auto' }}>
                {cart.map((l) => (
                  <ListItem key={l.product.id} alignItems="flex-start" sx={{ px: 0 }}
                    secondaryAction={<IconButton edge="end" size="small" onClick={() => remove(l.product.id)}><DeleteOutline fontSize="small" /></IconButton>}>
                    <ListItemText
                      primary={<Typography variant="body2" fontWeight={600}>{l.product.name}</Typography>}
                      secondary={
                        <Stack direction="row" spacing={1} alignItems="center" mt={0.5}>
                          <IconButton size="small" onClick={() => setQty(l.product.id, l.quantity - 1)}><Remove fontSize="inherit" /></IconButton>
                          <Typography variant="body2">{l.quantity}</Typography>
                          <IconButton size="small" onClick={() => setQty(l.product.id, l.quantity + 1)}><Add fontSize="inherit" /></IconButton>
                          <TextField size="small" type="number" label="Desc." value={l.discountValue}
                            onChange={(e) => setDisc(l.product.id, Number(e.target.value))} sx={{ width: 90 }} />
                          <Typography variant="body2" fontWeight={600} ml="auto">{lineTotal(l).toFixed(2)}</Typography>
                        </Stack>
                      } />
                  </ListItem>
                ))}
                {cart.length === 0 && <Typography color="text.secondary" textAlign="center" py={3}>Carrito vacío</Typography>}
              </List>
              <Divider sx={{ my: 1 }} />
              <Stack spacing={0.5}>
                <Box display="flex" justifyContent="space-between"><Typography variant="body2">Subtotal</Typography><Typography variant="body2">{subtotal.toFixed(2)}</Typography></Box>
                <Box display="flex" justifyContent="space-between"><Typography variant="body2">Descuento</Typography><Typography variant="body2">{(subtotal - total).toFixed(2)}</Typography></Box>
                <Box display="flex" justifyContent="space-between"><Typography variant="h6" fontWeight={800}>Total</Typography><Typography variant="h6" fontWeight={800}>{total.toFixed(2)} {currency}</Typography></Box>
              </Stack>
              <Button fullWidth variant="contained" size="large" sx={{ mt: 2 }} startIcon={<PointOfSale />}
                disabled={!canSell} onClick={() => setPayDialog(true)}>Cobrar</Button>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {payDialog && register && (
        <PaymentDialog
          total={total}
          currency={currency}
          onClose={() => setPayDialog(false)}
          onConfirm={(method, cashTendered) => {
            const dto: CreateOpsSale = {
              registerId: register.id,
              paymentMethod: method,
              paymentCurrency: currency,
              exchangeRate: rate?.rate,
              terminalName: terminals?.find((t) => t.id === terminalId)?.name,
              items: cart.map((l) => ({
                productId: l.product.id,
                warehouseId: effectiveWarehouse,
                quantity: l.quantity,
                discountType: l.discountType,
                discountValue: l.discountValue,
              })),
              payments: [{
                method, amount: total, currency,
                cashTendered: method === 'Cash' ? cashTendered : undefined,
                change: method === 'Cash' && cashTendered ? Math.max(0, cashTendered - total) : undefined,
                changeCurrency: currency,
              }],
            };
            createSale.mutate(dto, {
              onSuccess: (sale) => { setCart([]); setPayDialog(false); setLastSale(sale); },
            });
          }}
          pending={createSale.isPending}
          error={createSale.isError}
        />
      )}

      {lastSale && (
        <Dialog open onClose={() => setLastSale(null)} maxWidth="xs" fullWidth>
          <SaleReceipt sale={lastSale} />
          <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <CheckCircle color="success" fontSize="small" /> Venta registrada
          </DialogTitle>
          <DialogContent>
            <Typography variant="body2" color="text.secondary">
              Total: <strong>{lastSale.total.toFixed(2)} {lastSale.paymentCurrency}</strong>
            </Typography>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setLastSale(null)}>Cerrar</Button>
            <Button variant="contained" startIcon={<Print />} onClick={() => window.print()}>Imprimir recibo</Button>
          </DialogActions>
        </Dialog>
      )}
    </Box>
  );
}

function PaymentDialog({ total, currency, onClose, onConfirm, pending, error }: {
  total: number; currency: OpsCurrency; onClose: () => void;
  onConfirm: (method: OpsPaymentMethod, cashTendered?: number) => void; pending: boolean; error: boolean;
}) {
  const [method, setMethod] = useState<OpsPaymentMethod>('Cash');
  const [tendered, setTendered] = useState(total);
  const change = Math.max(0, tendered - total);
  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Cobro — {total.toFixed(2)} {currency}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <ToggleButtonGroup exclusive fullWidth size="small" value={method} onChange={(_, v) => v && setMethod(v)}>
            <ToggleButton value="Cash">Efectivo</ToggleButton>
            <ToggleButton value="Card">Tarjeta</ToggleButton>
            <ToggleButton value="Transfer">Transf.</ToggleButton>
          </ToggleButtonGroup>
          {method === 'Cash' && (
            <>
              <TextField size="small" type="number" label="Efectivo recibido" value={tendered}
                onChange={(e) => setTendered(Number(e.target.value))} />
              <Box display="flex" justifyContent="space-between">
                <Typography>Cambio</Typography><Typography fontWeight={700}>{change.toFixed(2)} {currency}</Typography>
              </Box>
            </>
          )}
          {error && <Alert severity="error">No se pudo registrar la venta (¿stock suficiente / caja abierta?).</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" disabled={pending || (method === 'Cash' && tendered < total)}
          onClick={() => onConfirm(method, tendered)}>Confirmar venta</Button>
      </DialogActions>
    </Dialog>
  );
}
