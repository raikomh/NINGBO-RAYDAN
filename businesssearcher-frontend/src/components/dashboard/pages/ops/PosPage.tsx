import { useEffect, useMemo, useState } from 'react';
import {
  Box, Card, CardContent, Typography, TextField, MenuItem, Button, IconButton, Chip,
  List, ListItem, ListItemText, Divider, Stack, Grid, Alert, InputAdornment, ToggleButton,
  ToggleButtonGroup, Dialog, DialogTitle, DialogContent, DialogActions, CircularProgress,
} from '@mui/material';
import { Add, Remove, DeleteOutline, PointOfSale, Search, Print, CheckCircle } from '@mui/icons-material';
import { useOpsProducts, useWarehouses, useCurrentCashRegister, useCreateSale, useOpsManagers, useExchangeRate, fetchProductByBarcode } from '@/hooks/useOps';
import type { OpsProduct, OpsCurrency, OpsPaymentMethod, CreateOpsSale, OpsSale } from '@/lib/opsTypes';
import { useActiveStore } from '@/context/StoreContext';
import { useNavigate } from 'react-router-dom';
import SaleReceipt from './SaleReceipt';
import OrdenEntregaReceipt, { type OrdenEntregaDeliveryInfo } from './OrdenEntregaReceipt';
import CashDenominationBreakdown from './CashDenominationBreakdown';

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
  const { storeId } = useActiveStore();
  const [warehouseId, setWarehouseId] = useState('');
  const [search, setSearch] = useState('');

  // Al abrir/cambiar la caja, preselecciona la terminal y el almacén con los que se abrió (si tiene).
  // Sin esto, "effectiveWarehouse" caía al primer almacén de la lista aunque la caja se hubiera
  // abierto contra otro: el producto se veía en stock (con el stock de OTRO almacén) pero al
  // vender, AdjustStock validaba el almacén real y tiraba "Stock insuficiente" — se sentía como
  // si la caja abierta no dejara vender. Si la caja no tiene almacén propio, cae a la tienda activa
  // del selector global (si hay una elegida).
  useEffect(() => { setWarehouseId(register?.warehouseId ?? storeId ?? ''); }, [register?.id, storeId]);
  const { data: products, isLoading } = useOpsProducts({ search: search || undefined, warehouseId: warehouseId || undefined });

  const [cart, setCart] = useState<CartLine[]>([]);
  // Ventas solo en USD por ahora: se esconde el selector de moneda (CUP queda deshabilitado
  // temporalmente, no se elimina el soporte del código por si se reactiva más adelante).
  const currency: OpsCurrency = 'USD';
  const [payDialog, setPayDialog] = useState(false);
  const [lastSale, setLastSale] = useState<OpsSale | null>(null);
  const [lastDelivery, setLastDelivery] = useState<OrdenEntregaDeliveryInfo | null>(null);
  const [lastCodeByProductId, setLastCodeByProductId] = useState<Record<string, string | undefined>>({});
  const [printTarget, setPrintTarget] = useState<'ticket' | 'orden'>('ticket');
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
  // Si la venta es en USD y el producto no tiene precio propio en USD, NUNCA reutiliza el número
  // de sellPrice (CUP) tal cual — eso mezclaría unidades (un producto de 300 CUP se cobraría como
  // 300 USD). Se convierte con la tasa vigente; si no hay tasa, no se puede tasar en USD.
  const priceOf = (p: OpsProduct) => {
    const { cup, usd } = priceAt(p);
    if (currency !== 'USD') return cup;
    if (usd != null) return usd;
    if (rate?.rate) return Math.round((cup / rate.rate) * 100) / 100;
    return null;
  };
  const isPriceable = (p: OpsProduct) => priceOf(p) != null;
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
  const setDiscType = (id: string, t: 'Amount' | 'Percentage') =>
    setCart((c) => c.map((l) => (l.product.id === id ? { ...l, discountType: t } : l)));
  const remove = (id: string) => setCart((c) => c.filter((l) => l.product.id !== id));

  const lineTotal = (l: CartLine) => {
    const gross = (priceOf(l.product) ?? 0) * l.quantity;
    const disc = l.discountType === 'Percentage' ? gross * (l.discountValue / 100) : l.discountValue;
    return Math.max(0, gross - disc);
  };
  const subtotal = useMemo(() => cart.reduce((s, l) => s + (priceOf(l.product) ?? 0) * l.quantity, 0), [cart, currency]);
  const total = useMemo(() => cart.reduce((s, l) => s + lineTotal(l), 0), [cart, currency]);
  const hasUnpriceableLine = cart.some((l) => !isPriceable(l.product));

  const canSell = !!register && register.status === 'Open' && cart.length > 0 && !!effectiveWarehouse && !hasUnpriceableLine;

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" flexWrap="wrap" gap={2} mb={2}>
        <Typography variant="h5" fontWeight={700}>Punto de Venta</Typography>
        <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap">
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
              <Stack direction="row" spacing={1.5} mb={2} flexWrap="wrap">
                <TextField size="small" fullWidth placeholder="Buscar producto o escanear código…" value={search}
                  onChange={(e) => setSearch(e.target.value)} onKeyDown={handleSearchKeyDown}
                  InputProps={{ startAdornment: <InputAdornment position="start"><Search fontSize="small" /></InputAdornment> }} />
                <TextField size="small" select label="Almacén" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} sx={{ minWidth: 150, flex: '1 1 150px' }}>
                  <MenuItem value="">{warehouses?.[0]?.name ?? 'Principal'}</MenuItem>
                  {warehouses?.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
                </TextField>
              </Stack>
              {isLoading ? (
                <Box display="flex" justifyContent="center" py={4}><CircularProgress /></Box>
              ) : (
                <Grid container spacing={1}>
                  {(products ?? []).filter((p) => p.forSale).map((p) => {
                    const st = stockOf(p);
                    const priceable = isPriceable(p);
                    const sellable = st > 0 && priceable;
                    const price = priceOf(p);
                    return (
                      <Grid item xs={6} sm={4} key={p.id}>
                        <Card variant="outlined" sx={{ cursor: sellable ? 'pointer' : 'not-allowed', opacity: sellable ? 1 : 0.5, height: '100%' }}
                          onClick={() => sellable && addToCart(p)}>
                          <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                            <Typography variant="body2" fontWeight={600} noWrap>{p.name}</Typography>
                            {p.barcode && (
                              <Typography variant="caption" color="text.secondary" display="block" noWrap>{p.barcode}</Typography>
                            )}
                            <Typography variant="caption" color={priceable ? 'text.secondary' : 'error'} display="block">
                              {currency === 'USD'
                                ? (price != null ? `${priceAt(p).usd != null ? '' : '≈ '}$${price.toFixed(2)}` : 'Sin precio USD')
                                : `${priceAt(p).cup.toFixed(2)} CUP`}
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
                <Chip label="USD" size="small" color="primary" variant="outlined" />
              </Box>
              <Divider />
              <List dense sx={{ maxHeight: 340, overflow: 'auto' }}>
                {cart.map((l) => (
                  <ListItem key={l.product.id} alignItems="flex-start" sx={{ px: 0 }}
                    secondaryAction={<IconButton edge="end" size="small" onClick={() => remove(l.product.id)}><DeleteOutline fontSize="small" /></IconButton>}>
                    <ListItemText
                      primary={
                        <Typography variant="body2" fontWeight={600}>
                          {l.product.name}
                          {!isPriceable(l.product) && <Typography component="span" variant="caption" color="error"> · sin precio en {currency}</Typography>}
                        </Typography>
                      }
                      secondary={
                        <Stack direction="row" spacing={1} alignItems="center" mt={0.5} flexWrap="wrap">
                          <IconButton size="small" onClick={() => setQty(l.product.id, l.quantity - 1)}><Remove fontSize="inherit" /></IconButton>
                          <Typography variant="body2">{l.quantity}</Typography>
                          <IconButton size="small" onClick={() => setQty(l.product.id, l.quantity + 1)}><Add fontSize="inherit" /></IconButton>
                          <TextField size="small" type="number" label="Desc." value={l.discountValue === 0 ? '' : l.discountValue}
                            onChange={(e) => setDisc(l.product.id, e.target.value === '' ? 0 : Number(e.target.value))} sx={{ width: 80 }} />
                          <ToggleButtonGroup size="small" exclusive value={l.discountType}
                            onChange={(_, v) => v && setDiscType(l.product.id, v)}>
                            <ToggleButton value="Amount" sx={{ px: 0.75, py: 0.25 }}>$</ToggleButton>
                            <ToggleButton value="Percentage" sx={{ px: 0.75, py: 0.25 }}>%</ToggleButton>
                          </ToggleButtonGroup>
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
                {rate?.rate && (
                  <Box display="flex" justifyContent="flex-end">
                    <Typography variant="caption" color="text.secondary">
                      ≈ {(total * rate.rate).toFixed(2)} CUP
                    </Typography>
                  </Box>
                )}
              </Stack>
              {hasUnpriceableLine && (
                <Alert severity="error" sx={{ mt: 1.5 }}>
                  Hay productos sin precio en {currency}. Configúrales un precio en USD o define una tasa de cambio en Ajustes para poder venderlos en {currency}.
                </Alert>
              )}
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
          onConfirm={(method, cashTendered, managerCode, delivery) => {
            const dto: CreateOpsSale = {
              registerId: register.id,
              managerCode,
              paymentMethod: method,
              paymentCurrency: currency,
              exchangeRate: rate?.rate,
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
            const codeByProductId = Object.fromEntries(cart.map((l) => [l.product.id, l.product.barcode]));
            createSale.mutate(dto, {
              onSuccess: (sale) => {
                setCart([]); setPayDialog(false); setLastSale(sale);
                setLastDelivery(delivery ?? null); setLastCodeByProductId(codeByProductId); setPrintTarget('ticket');
              },
            });
          }}
          pending={createSale.isPending}
          error={createSale.isError}
          errorMessage={(createSale.error as { response?: { data?: { message?: string } } })?.response?.data?.message}
        />
      )}

      {lastSale && (
        <Dialog open onClose={() => setLastSale(null)} maxWidth="xs" fullWidth>
          <SaleReceipt sale={lastSale} className={printTarget === 'ticket' ? 'receipt-print-area' : undefined} />
          <OrdenEntregaReceipt sale={lastSale} delivery={lastDelivery} codeByProductId={lastCodeByProductId}
            className={printTarget === 'orden' ? 'receipt-print-area' : undefined} />
          <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <CheckCircle color="success" fontSize="small" /> Venta registrada
          </DialogTitle>
          <DialogContent>
            <Typography variant="body2" color="text.secondary">
              Total: <strong>{lastSale.total.toFixed(2)} {lastSale.paymentCurrency}</strong>
              {lastSale.totalUSD != null && lastSale.paymentCurrency !== 'USD' && (
                <> (${lastSale.totalUSD.toFixed(2)} USD)</>
              )}
            </Typography>
          </DialogContent>
          <DialogActions sx={{ flexWrap: 'wrap', gap: 1 }}>
            <Button onClick={() => setLastSale(null)}>Cerrar</Button>
            <Button variant="outlined" startIcon={<Print />}
              onClick={() => { setPrintTarget('ticket'); setTimeout(() => window.print(), 50); }}>Imprimir recibo</Button>
            <Button variant="contained" startIcon={<Print />}
              onClick={() => { setPrintTarget('orden'); setTimeout(() => window.print(), 50); }}>Imprimir orden de entrega</Button>
          </DialogActions>
        </Dialog>
      )}
    </Box>
  );
}

function PaymentDialog({ total, currency, onClose, onConfirm, pending, error, errorMessage }: {
  total: number; currency: OpsCurrency; onClose: () => void;
  onConfirm: (method: OpsPaymentMethod, cashTendered: number | undefined, managerCode: string,
    delivery: OrdenEntregaDeliveryInfo) => void;
  pending: boolean; error: boolean; errorMessage?: string;
}) {
  const [method, setMethod] = useState<OpsPaymentMethod>('Cash');
  const [tendered, setTendered] = useState<number | ''>(total);
  const tenderedValue = tendered === '' ? 0 : tendered;
  const [managerCode, setManagerCode] = useState('');
  const { data: managers } = useOpsManagers();
  const manager = (managers ?? []).find((m) => m.isActive && m.code.toUpperCase() === managerCode.trim().toUpperCase());
  const change = Math.max(0, tenderedValue - total);

  // Datos para la Orden de Entrega impresa (no se guardan en el sistema todavía, solo se usan
  // para generar ese documento al confirmar la venta).
  const [showDelivery, setShowDelivery] = useState(false);
  const [cliente, setCliente] = useState('');
  const [ci, setCi] = useState('');
  const [telefono, setTelefono] = useState('');
  const [direccion, setDireccion] = useState('');
  const [domicilio, setDomicilio] = useState<number | ''>('');

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
          <TextField size="small" label="Código del gestor" value={managerCode} autoFocus
            onChange={(e) => setManagerCode(e.target.value)}
            error={!!managerCode.trim() && !manager}
            helperText={manager ? manager.name : managerCode.trim() ? 'No existe un gestor activo con ese código' : 'Gestor que realizó la venta'} />
          {method === 'Cash' && (
            <>
              <TextField size="small" type="number" label="Efectivo recibido" value={tendered}
                onChange={(e) => setTendered(e.target.value === '' ? '' : Number(e.target.value))} />
              <CashDenominationBreakdown currency={currency} onApply={(t) => setTendered(t)} />
              <Box display="flex" justifyContent="space-between">
                <Typography>Cambio</Typography><Typography fontWeight={700}>{change.toFixed(2)} {currency}</Typography>
              </Box>
            </>
          )}
          <Button size="small" onClick={() => setShowDelivery((v) => !v)} sx={{ alignSelf: 'flex-start' }}>
            {showDelivery ? 'Ocultar' : 'Agregar'} datos de entrega (opcional)
          </Button>
          {showDelivery && (
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
          )}
          {error && <Alert severity="error">{errorMessage || 'No se pudo registrar la venta (¿stock suficiente / caja abierta?).'}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" disabled={pending || !manager || (method === 'Cash' && tenderedValue < total)}
          onClick={() => onConfirm(method, tenderedValue, manager!.code, {
            cliente: cliente || undefined, ci: ci || undefined, telefono: telefono || undefined,
            direccion: direccion || undefined, domicilio: domicilio === '' ? undefined : domicilio,
          })}>Confirmar venta</Button>
      </DialogActions>
    </Dialog>
  );
}
