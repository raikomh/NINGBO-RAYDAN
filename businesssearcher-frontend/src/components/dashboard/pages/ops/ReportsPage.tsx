import { useMemo, useState } from 'react';
import {
  Box, Card, CardContent, Typography, Tabs, Tab, TextField, Button, Chip, CircularProgress,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Stack, FormControlLabel, Switch,
  Grid, IconButton, ToggleButtonGroup, ToggleButton,
} from '@mui/material';
import {
  Download, Assessment, ChevronLeft, ChevronRight, TrendingUp, TrendingDown,
  PointOfSale, ShoppingCart, Payments, DeleteSweep, AttachMoney, Sell,
} from '@mui/icons-material';
import {
  ResponsiveContainer, ComposedChart, Bar, Line, XAxis, YAxis, CartesianGrid,
  Tooltip as ReTooltip, Legend,
} from 'recharts';
import {
  useSalesReport, useExportSalesReport, useInventoryReport, useExportInventoryReport,
  useExpensesReport, useExportExpensesReport, useMonthlyDashboard, useDashboardSummary,
} from '@/hooks/useOps';

const money = (n: number | null | undefined) => (n ?? 0).toLocaleString('es', { maximumFractionDigits: 2 });

export default function ReportsPage() {
  const [tab, setTab] = useState(0);

  return (
    <Box>
      <Box display="flex" alignItems="center" gap={1} mb={3}>
        <Assessment color="primary" />
        <Typography variant="h5" fontWeight={700}>Estadísticas del Negocio</Typography>
      </Box>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable" scrollButtons="auto" sx={{ mb: 2 }}>
        <Tab label="Dashboard" />
        <Tab label="Ventas" />
        <Tab label="Inventario" />
        <Tab label="Gastos" />
      </Tabs>

      {tab === 0 && <DashboardTab />}
      {tab === 1 && <SalesReportTab />}
      {tab === 2 && <InventoryReportTab />}
      {tab === 3 && <ExpensesReportTab />}
    </Box>
  );
}

interface KpiCardProps {
  label: string;
  value: string;
  icon: React.ReactNode;
  color: string;
}

function KpiCard({ label, value, icon, color }: KpiCardProps) {
  return (
    <Card
      variant="outlined"
      sx={{
        borderRadius: 2,
        borderLeft: `4px solid ${color}`,
        height: '100%',
        transition: 'transform 0.25s ease, box-shadow 0.25s ease, border-left-color 0.25s ease',
        '&:hover': {
          transform: 'translateY(-4px)',
          boxShadow: `0 12px 24px -10px ${color}80`,
          borderLeftWidth: 6,
        },
        '&:hover .kpi-icon': { transform: 'scale(1.15)' },
      }}
    >
      <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 1.5, py: 1.75, '&:last-child': { pb: 1.75 } }}>
        <Box className="kpi-icon" sx={{ color, display: 'flex', transition: 'transform 0.25s ease' }}>{icon}</Box>
        <Box minWidth={0}>
          <Typography variant="caption" color="text.secondary" noWrap display="block">{label}</Typography>
          <Typography variant="subtitle1" fontWeight={700} noWrap>{value}</Typography>
        </Box>
      </CardContent>
    </Card>
  );
}

function DashboardTab() {
  const [year, setYear] = useState(new Date().getFullYear());
  const [currency, setCurrency] = useState<'CUP' | 'USD'>('CUP');
  const { data, isLoading } = useMonthlyDashboard(year);

  const [rangeFrom, setRangeFrom] = useState('');
  const [rangeTo, setRangeTo] = useState('');
  const { data: rangeSummary, isLoading: rangeLoading } = useDashboardSummary({ from: rangeFrom || undefined, to: rangeTo || undefined });

  const sym = currency === 'USD' ? '$' : '';
  const suffix = currency === 'USD' ? ' USD' : ' CUP';
  const fmt = (n: number) => `${sym}${money(n)}`;

  const chartData = useMemo(
    () => (data?.months ?? []).map((m) => ({
      name: m.monthLabel,
      Ventas: currency === 'USD' ? m.salesTotalUsd : m.salesTotal,
      'Costo de venta': currency === 'USD' ? m.costOfGoodsSoldUsd : m.costOfGoodsSold,
      Compras: currency === 'USD' ? m.purchasesTotalUsd : m.purchasesTotal,
      Gastos: currency === 'USD' ? m.expensesTotalUsd : m.expensesTotal,
      Merma: currency === 'USD' ? m.mermaValueUsd : m.mermaValue,
      Ganancia: currency === 'USD' ? m.profitUsd : m.profit,
    })),
    [data, currency],
  );

  const changeUp = (data?.profitChangePercent ?? 0) >= 0;

  return (
    <Box>
      <Box display="flex" alignItems="center" gap={1} mb={3} flexWrap="wrap">
        <IconButton size="small" onClick={() => setYear((y) => y - 1)}><ChevronLeft /></IconButton>
        <Typography variant="h6" fontWeight={700} minWidth={64} textAlign="center">{year}</Typography>
        <IconButton size="small" onClick={() => setYear((y) => y + 1)} disabled={year >= new Date().getFullYear()}>
          <ChevronRight />
        </IconButton>
        {data?.profitChangePercent != null && (
          <Chip
            size="small"
            icon={changeUp ? <TrendingUp fontSize="small" /> : <TrendingDown fontSize="small" />}
            color={changeUp ? 'success' : 'error'}
            label={`${changeUp ? '+' : ''}${data.profitChangePercent.toFixed(1)}% vs ${year - 1}`}
            sx={{ ml: { xs: 0, sm: 1 } }}
          />
        )}
        <Box flexGrow={1} />
        <ToggleButtonGroup size="small" exclusive value={currency} onChange={(_, v) => v && setCurrency(v)}>
          <ToggleButton value="CUP">CUP</ToggleButton>
          <ToggleButton value="USD">USD</ToggleButton>
        </ToggleButtonGroup>
        {data?.exchangeRate && <Chip size="small" variant="outlined" label={`Tasa: ${data.exchangeRate} CUP/USD`} />}
      </Box>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <>
          <Grid container spacing={2} mb={3}>
            <Grid item xs={12} sm={6} md={2}>
              <KpiCard label="Ventas del año" value={fmt(currency === 'USD' ? (data?.yearSalesTotalUsd ?? 0) : (data?.yearSalesTotal ?? 0))} icon={<PointOfSale />} color="#10B981" />
            </Grid>
            <Grid item xs={12} sm={6} md={2}>
              <KpiCard label="Costo de venta del año" value={fmt(currency === 'USD' ? (data?.yearCostOfGoodsSoldUsd ?? 0) : (data?.yearCostOfGoodsSold ?? 0))} icon={<Sell />} color="#F97316" />
            </Grid>
            <Grid item xs={12} sm={6} md={2}>
              <KpiCard label="Compras del año" value={fmt(currency === 'USD' ? (data?.yearPurchasesTotalUsd ?? 0) : (data?.yearPurchasesTotal ?? 0))} icon={<ShoppingCart />} color="#2563EB" />
            </Grid>
            <Grid item xs={12} sm={6} md={2}>
              <KpiCard label="Gastos del año" value={fmt(currency === 'USD' ? (data?.yearExpensesTotalUsd ?? 0) : (data?.yearExpensesTotal ?? 0))} icon={<Payments />} color="#F59E0B" />
            </Grid>
            <Grid item xs={12} sm={6} md={2}>
              <KpiCard label="Valor en mermas" value={fmt(currency === 'USD' ? (data?.yearMermaValueUsd ?? 0) : (data?.yearMermaValue ?? 0))} icon={<DeleteSweep />} color="#EF4444" />
            </Grid>
            <Grid item xs={12} sm={6} md={2}>
              <KpiCard label="Ganancia del año" value={fmt(currency === 'USD' ? (data?.yearProfitUsd ?? 0) : (data?.yearProfit ?? 0))} icon={<AttachMoney />} color="#7C3AED" />
            </Grid>
          </Grid>

          <Card variant="outlined" sx={{ borderRadius: 2, p: { xs: 1, sm: 2 }, mb: 3 }}>
            <Typography variant="subtitle1" fontWeight={600} mb={1} sx={{ px: { xs: 1, sm: 0 } }}>
              Ventas, costo de venta, compras, gastos, mermas y ganancia por mes ({currency})
            </Typography>
            <Box sx={{ width: '100%', height: { xs: 260, sm: 340 } }}>
              <ResponsiveContainer width="100%" height="100%">
                <ComposedChart data={chartData} margin={{ top: 8, right: 8, bottom: 4, left: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="currentColor" strokeOpacity={0.08} />
                  <XAxis dataKey="name" tick={{ fontSize: 12 }} />
                  <YAxis tick={{ fontSize: 12 }} width={56} />
                  <ReTooltip formatter={(v: number) => fmt(v)} />
                  <Legend wrapperStyle={{ fontSize: 12 }} />
                  <Bar dataKey="Ventas" fill="#10B981" radius={[3, 3, 0, 0]} maxBarSize={22} />
                  <Bar dataKey="Costo de venta" fill="#F97316" radius={[3, 3, 0, 0]} maxBarSize={22} />
                  <Bar dataKey="Compras" fill="#2563EB" radius={[3, 3, 0, 0]} maxBarSize={22} />
                  <Bar dataKey="Gastos" fill="#F59E0B" radius={[3, 3, 0, 0]} maxBarSize={22} />
                  <Bar dataKey="Merma" fill="#EF4444" radius={[3, 3, 0, 0]} maxBarSize={22} />
                  <Line type="monotone" dataKey="Ganancia" stroke="#7C3AED" strokeWidth={2.5} dot={{ r: 3, fill: '#7C3AED', strokeWidth: 0 }} />
                </ComposedChart>
              </ResponsiveContainer>
            </Box>
          </Card>

          <TableContainer component={Card} variant="outlined" sx={{ overflowX: 'auto', mb: 3 }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Mes</TableCell>
                  <TableCell align="right">Ventas</TableCell>
                  <TableCell align="right">Costo de venta</TableCell>
                  <TableCell align="right">Compras</TableCell>
                  <TableCell align="right">Gastos</TableCell>
                  <TableCell align="right">Merma</TableCell>
                  <TableCell align="right">Ganancia</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {(data?.months ?? []).map((m) => {
                  const profit = currency === 'USD' ? m.profitUsd : m.profit;
                  return (
                    <TableRow key={m.month} hover>
                      <TableCell>{m.monthLabel}</TableCell>
                      <TableCell align="right">{fmt(currency === 'USD' ? m.salesTotalUsd : m.salesTotal)}</TableCell>
                      <TableCell align="right">{fmt(currency === 'USD' ? m.costOfGoodsSoldUsd : m.costOfGoodsSold)}</TableCell>
                      <TableCell align="right">{fmt(currency === 'USD' ? m.purchasesTotalUsd : m.purchasesTotal)}</TableCell>
                      <TableCell align="right">{fmt(currency === 'USD' ? m.expensesTotalUsd : m.expensesTotal)}</TableCell>
                      <TableCell align="right">{fmt(currency === 'USD' ? m.mermaValueUsd : m.mermaValue)}</TableCell>
                      <TableCell align="right">
                        <Typography
                          component="span"
                          variant="body2"
                          fontWeight={700}
                          color={profit >= 0 ? 'success.main' : 'error.main'}
                        >
                          {fmt(profit)}
                        </Typography>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>

          <Card variant="outlined" sx={{ borderRadius: 2, p: 2 }}>
            <Typography variant="subtitle1" fontWeight={600} mb={1.5}>
              Resumen por rango de fechas
            </Typography>
            <Stack direction="row" spacing={2} mb={2} flexWrap="wrap" alignItems="center">
              <TextField size="small" type="date" label="Fecha inicial" InputLabelProps={{ shrink: true }}
                value={rangeFrom} onChange={(e) => setRangeFrom(e.target.value)} />
              <TextField size="small" type="date" label="Fecha final" InputLabelProps={{ shrink: true }}
                value={rangeTo} onChange={(e) => setRangeTo(e.target.value)} />
              <Typography variant="caption" color="text.secondary">
                {rangeFrom && rangeTo ? '' : 'Sin fechas: muestra los últimos 30 días.'}
              </Typography>
            </Stack>
            {rangeLoading ? (
              <Box display="flex" justifyContent="center" py={3}><CircularProgress size={28} /></Box>
            ) : rangeSummary ? (
              <Grid container spacing={2}>
                <Grid item xs={12} sm={6} md={2}>
                  <KpiCard label={`Ventas (${rangeSummary.salesCount})`} value={fmt(currency === 'USD' ? rangeSummary.salesTotalUsd : rangeSummary.salesTotal)} icon={<PointOfSale />} color="#10B981" />
                </Grid>
                <Grid item xs={12} sm={6} md={2}>
                  <KpiCard label="Costo de venta" value={fmt(currency === 'USD' ? rangeSummary.costOfGoodsSoldUsd : rangeSummary.costOfGoodsSold)} icon={<Sell />} color="#F97316" />
                </Grid>
                <Grid item xs={12} sm={6} md={2}>
                  <KpiCard label="Compras" value={fmt(currency === 'USD' ? rangeSummary.purchasesTotalUsd : rangeSummary.purchasesTotal)} icon={<ShoppingCart />} color="#2563EB" />
                </Grid>
                <Grid item xs={12} sm={6} md={2}>
                  <KpiCard label="Gastos" value={fmt(currency === 'USD' ? rangeSummary.expensesTotalUsd : rangeSummary.expensesTotal)} icon={<Payments />} color="#F59E0B" />
                </Grid>
                <Grid item xs={12} sm={6} md={2}>
                  <KpiCard label="Mermas" value={fmt(currency === 'USD' ? rangeSummary.mermaValueUsd : rangeSummary.mermaValue)} icon={<DeleteSweep />} color="#EF4444" />
                </Grid>
                <Grid item xs={12} sm={6} md={2}>
                  <KpiCard label="Ganancia" value={fmt(currency === 'USD' ? rangeSummary.profitUsd : rangeSummary.profit)} icon={<AttachMoney />} color="#7C3AED" />
                </Grid>
              </Grid>
            ) : null}
          </Card>
        </>
      )}
    </Box>
  );
}

function SalesReportTab() {
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const { data: report, isLoading } = useSalesReport({ from: from || undefined, to: to || undefined });
  const exportReport = useExportSalesReport();

  return (
    <Box>
      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
          <TextField size="small" type="date" label="Desde" InputLabelProps={{ shrink: true }} value={from} onChange={(e) => setFrom(e.target.value)} />
          <TextField size="small" type="date" label="Hasta" InputLabelProps={{ shrink: true }} value={to} onChange={(e) => setTo(e.target.value)} />
          <Box flexGrow={1} />
          {report && (
            <Stack direction="row" spacing={1} flexWrap="wrap">
              <Chip color="primary" label={`Ventas: ${report.totalSales.toFixed(2)} CUP ($${report.totalSalesUsd.toFixed(2)})`} />
              <Chip color="error" label={`Reembolsado: ${report.totalRefunded.toFixed(2)} CUP ($${report.totalRefundedUsd.toFixed(2)})`} />
            </Stack>
          )}
          <Button startIcon={<Download />} variant="contained" disabled={exportReport.isPending}
            onClick={() => exportReport.mutate({ from: from || undefined, to: to || undefined })}>Exportar Excel</Button>
        </CardContent>
      </Card>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead><TableRow><TableCell>Fecha</TableCell><TableCell>Método</TableCell><TableCell>Moneda</TableCell><TableCell align="right">Total</TableCell><TableCell>Estado</TableCell></TableRow></TableHead>
            <TableBody>
              {(report?.rows ?? []).map((r, i) => (
                <TableRow key={i} hover>
                  <TableCell>{new Date(r.date).toLocaleString()}</TableCell>
                  <TableCell>{r.paymentMethod}</TableCell>
                  <TableCell>{r.currency}</TableCell>
                  <TableCell align="right">
                    {r.total.toFixed(2)}
                    {r.totalUsd != null && (
                      <Typography variant="caption" color="text.secondary" display="block">${r.totalUsd.toFixed(2)}</Typography>
                    )}
                  </TableCell>
                  <TableCell><Chip size="small" color={r.status === 'Refunded' ? 'error' : 'success'} label={r.status} /></TableCell>
                </TableRow>
              ))}
              {(report?.rows ?? []).length === 0 && <TableRow><TableCell colSpan={5} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin ventas en el período</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}

function InventoryReportTab() {
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const { data: report, isLoading } = useInventoryReport(lowStockOnly);
  const exportReport = useExportInventoryReport();

  return (
    <Box>
      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
          <FormControlLabel control={<Switch checked={lowStockOnly} onChange={(e) => setLowStockOnly(e.target.checked)} />} label="Solo bajo stock" />
          <Box flexGrow={1} />
          {report && (
            <Stack direction="row" spacing={1} flexWrap="wrap">
              <Chip label={`Productos: ${report.productCount}`} />
              <Chip color="warning" label={`Bajo stock: ${report.lowStockCount}`} />
              <Chip color="primary" label={`Valor: ${report.inventoryValue.toFixed(2)}`} />
            </Stack>
          )}
          <Button startIcon={<Download />} variant="contained" disabled={exportReport.isPending}
            onClick={() => exportReport.mutate(lowStockOnly)}>Exportar Excel</Button>
        </CardContent>
      </Card>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead><TableRow><TableCell>Producto</TableCell><TableCell>Código</TableCell><TableCell align="right">Stock</TableCell><TableCell align="right">Mínimo</TableCell><TableCell align="right">Precio venta</TableCell></TableRow></TableHead>
            <TableBody>
              {(report?.rows ?? []).map((r, i) => (
                <TableRow key={i} hover>
                  <TableCell>{r.productName}</TableCell>
                  <TableCell>{r.barcode ?? '—'}</TableCell>
                  <TableCell align="right"><Chip size="small" color={r.lowStock ? 'warning' : 'default'} label={r.totalStock} /></TableCell>
                  <TableCell align="right">{r.minStock}</TableCell>
                  <TableCell align="right">{r.sellPrice.toFixed(2)}</TableCell>
                </TableRow>
              ))}
              {(report?.rows ?? []).length === 0 && <TableRow><TableCell colSpan={5} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin productos</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}

function ExpensesReportTab() {
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const { data: report, isLoading } = useExpensesReport({ from: from || undefined, to: to || undefined });
  const exportReport = useExportExpensesReport();

  return (
    <Box>
      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
          <TextField size="small" type="date" label="Desde" InputLabelProps={{ shrink: true }} value={from} onChange={(e) => setFrom(e.target.value)} />
          <TextField size="small" type="date" label="Hasta" InputLabelProps={{ shrink: true }} value={to} onChange={(e) => setTo(e.target.value)} />
          <Box flexGrow={1} />
          {report && <Chip color="primary" label={`Total: ${report.total.toFixed(2)}`} />}
          <Button startIcon={<Download />} variant="contained" disabled={exportReport.isPending}
            onClick={() => exportReport.mutate({ from: from || undefined, to: to || undefined })}>Exportar Excel</Button>
        </CardContent>
      </Card>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead><TableRow><TableCell>Fecha</TableCell><TableCell>Tipo</TableCell><TableCell>Descripción</TableCell><TableCell align="right">Monto</TableCell></TableRow></TableHead>
            <TableBody>
              {(report?.rows ?? []).map((r, i) => (
                <TableRow key={i} hover>
                  <TableCell>{new Date(r.date).toLocaleDateString()}</TableCell>
                  <TableCell>{r.type}</TableCell>
                  <TableCell>{r.description}</TableCell>
                  <TableCell align="right">{r.amount.toFixed(2)}</TableCell>
                </TableRow>
              ))}
              {(report?.rows ?? []).length === 0 && <TableRow><TableCell colSpan={4} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin gastos en el período</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
