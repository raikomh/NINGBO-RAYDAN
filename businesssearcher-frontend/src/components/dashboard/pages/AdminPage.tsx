import { useState } from 'react';
import {
  Box, Typography, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Paper, Chip, CircularProgress, Button,
  TextField, MenuItem, Alert, IconButton, Tooltip, Dialog,
  DialogTitle, DialogContent, DialogActions, Grid, Card, CardContent,
  Tabs, Tab, List, ListItem, ListItemText, ListItemButton, Divider,
  Avatar, FormControlLabel, Switch,
} from '@mui/material';
import {
  Block, CheckCircle, Delete, Refresh, People, Business,
  Chat as ChatIcon, Send as SendIcon, HowToReg, Star, StarBorder, MoneyOff, Paid,
  EmojiEvents, LocalAtm, Schedule as ScheduleIcon, Phone, Close,
} from '@mui/icons-material';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/apiClient';
import type {
  TenantAdmin, AdminStats, ClientAdmin, OverdueTenant, MarketingVisitSummary,
  ReferralPayoutAdmin, ReferralLeaderboard, MonthlySettlementReceipt,
  AdminPaymentClaim, ExpiringTenant, AdminPremiumClaim, ExpiringClient,
} from '@/lib/types';
import MipymeReferralsPanel from './MipymeReferralsPanel';

const STATUS_OPTS = ['', 'Active', 'Suspended'];

// ── Tenant management ────────────────────────────────────────────────────────

function useAdminStats() {
  return useQuery<AdminStats>({
    queryKey: ['admin', 'stats'],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/tenants/stats');
      return res.data.data ?? res.data;
    },
  });
}

function useOverduePayments() {
  return useQuery<OverdueTenant[]>({
    queryKey: ['admin', 'overdue-payments'],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/tenants/overdue-payments');
      const payload = res.data?.data ?? res.data;
      return Array.isArray(payload) ? payload : [];
    },
  });
}

function useMarketingVisits() {
  return useQuery<MarketingVisitSummary[]>({
    queryKey: ['admin', 'marketing-visits'],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/marketing/visits');
      const payload = res.data?.data ?? res.data;
      return Array.isArray(payload) ? payload : [];
    },
  });
}

function useExpiringSoon(days = 3) {
  return useQuery<ExpiringTenant[]>({
    queryKey: ['admin', 'expiring-soon', days],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/tenants/expiring-soon', { params: { days } });
      const payload = res.data?.data ?? res.data;
      return Array.isArray(payload) ? payload : [];
    },
  });
}

function usePaymentClaims() {
  return useQuery<AdminPaymentClaim[]>({
    queryKey: ['admin', 'payment-claims'],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/tenants/payment-claims');
      const payload = res.data?.data ?? res.data;
      return Array.isArray(payload) ? payload : [];
    },
  });
}

function useAdminTenants(params: { status?: string; search?: string; page?: number; pageSize?: number; isApproved?: boolean }) {
  return useQuery<{ data: TenantAdmin[]; total?: number }>({
    queryKey: ['admin', 'tenants', params],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/tenants', {
        params: { ...params, isApproved: params.isApproved },
      });
      // El backend devuelve PagedResult { items, totalCount } dentro de data
      const payload = res.data?.data ?? res.data;
      const items: TenantAdmin[] = payload?.items ?? (Array.isArray(payload) ? payload : []);
      return { data: items, total: payload?.totalCount };
    },
  });
}

interface SuspendDialogProps {
  open: boolean;
  tenant: TenantAdmin | null;
  onClose: () => void;
}

function SuspendDialog({ open, tenant, onClose }: SuspendDialogProps) {
  const qc = useQueryClient();
  const [reason, setReason] = useState('');

  const mutation = useMutation({
    mutationFn: () => api.post(`/api/v1/admin/tenants/${tenant?.id}/suspend`, { reason }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin'] });
      onClose();
    },
  });

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Suspender cuenta — {tenant?.businessName}</DialogTitle>
      <DialogContent>
        <TextField
          fullWidth
          label="Razón de suspensión"
          multiline
          rows={3}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          sx={{ mt: 1 }}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" color="error" disabled={!reason || mutation.isPending} onClick={() => mutation.mutate()}>
          {mutation.isPending ? 'Suspendiendo...' : 'Suspender'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

interface RenewDialogProps {
  open: boolean;
  tenant: { id: string; businessName: string } | null;
  onClose: () => void;
}

function RenewDialog({ open, tenant, onClose }: RenewDialogProps) {
  const qc = useQueryClient();
  const [amount, setAmount] = useState('');
  const [currency, setCurrency] = useState('COP');
  const [reference, setReference] = useState('');

  const mutation = useMutation({
    mutationFn: () => api.post(`/api/v1/admin/tenants/${tenant?.id}/renew`, {
      amount: Number(amount), currency, reference: reference || null,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin'] });
      setAmount(''); setReference('');
      onClose();
    },
  });

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Renovar suscripción — {tenant?.businessName}</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" mb={2}>
          Registra el pago recibido. Extiende la suscripción 30 días desde hoy y reactiva la cuenta
          si estaba suspendida o inactiva — si el dueño tiene una instalación Local emparejada, se
          desbloquea sola apenas sincronice.
        </Typography>
        <Box display="flex" gap={2} flexWrap="wrap" sx={{ mt: 1 }}>
          <TextField
            label="Monto" type="number" value={amount}
            onChange={(e) => setAmount(e.target.value)} sx={{ flex: 2, minWidth: 140 }}
          />
          <TextField
            label="Moneda" value={currency}
            onChange={(e) => setCurrency(e.target.value)} sx={{ flex: 1, minWidth: 100 }}
          />
        </Box>
        <TextField
          fullWidth label="Referencia (opcional)" value={reference}
          onChange={(e) => setReference(e.target.value)} sx={{ mt: 2 }}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button
          variant="contained" color="success"
          disabled={!amount || Number(amount) <= 0 || mutation.isPending}
          onClick={() => mutation.mutate()}
        >
          {mutation.isPending ? 'Renovando...' : 'Renovar 30 días'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

const CLAIM_STATUS_LABEL: Record<string, string> = {
  Pending: '⏳ Pendiente',
  Approved: '✅ Recibido',
  Rejected: '❌ Rechazado',
};

interface ReviewClaimDialogProps {
  open: boolean;
  claim: AdminPaymentClaim | null;
  action: 'approve' | 'reject' | null;
  onClose: () => void;
}

function ReviewClaimDialog({ open, claim, action, onClose }: ReviewClaimDialogProps) {
  const qc = useQueryClient();
  const [note, setNote] = useState('');
  const isApprove = action === 'approve';

  const mutation = useMutation({
    mutationFn: () => api.post(
      `/api/v1/admin/tenants/${claim?.tenantId}/payment-claims/${claim?.claimId}/${action}`,
      { note: note || null },
    ),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin'] });
      setNote('');
      onClose();
    },
  });

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isApprove ? 'Aprobar pago' : 'Rechazar reporte'} — {claim?.businessName}</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" mb={2}>
          {isApprove
            ? `Se registrará el pago de ${claim?.amount} ${claim?.currency} y la suscripción se extenderá 30 días desde hoy.`
            : 'El reporte quedará marcado como rechazado. No se registra ningún pago.'}
        </Typography>
        <Box display="flex" gap={3} mb={2} flexWrap="wrap">
          <Typography variant="body2"><b>Teléfono:</b> {claim?.phoneNumber}</Typography>
          <Typography variant="body2"><b>Comprobante:</b> {claim?.proofReference}</Typography>
        </Box>
        <TextField
          fullWidth label="Nota (opcional)" value={note}
          onChange={(e) => setNote(e.target.value)} multiline rows={2}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button
          variant="contained" color={isApprove ? 'success' : 'error'}
          disabled={mutation.isPending}
          onClick={() => mutation.mutate()}
        >
          {mutation.isPending ? 'Guardando...' : isApprove ? 'Aprobar y renovar' : 'Rechazar'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function PaymentClaimsPanel() {
  const { data: claims, isLoading } = usePaymentClaims();
  const [reviewClaim, setReviewClaim] = useState<{ claim: AdminPaymentClaim; action: 'approve' | 'reject' } | null>(null);

  const list = claims ?? [];

  return (
    <Card variant="outlined" sx={{ mt: 3, borderRadius: 2 }}>
      <CardContent>
        <Typography variant="subtitle2" fontWeight={700} mb={1.5}>
          Pagos reportados por los negocios
        </Typography>
        {isLoading ? (
          <Box display="flex" justifyContent="center" py={4}><CircularProgress size={28} /></Box>
        ) : list.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
            Todavía no hay reportes de pago.
          </Typography>
        ) : (
          <TableContainer sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>#</TableCell>
                  <TableCell>Fecha</TableCell>
                  <TableCell>Usuario</TableCell>
                  <TableCell>Teléfono</TableCell>
                  <TableCell>Monto</TableCell>
                  <TableCell>Comprobante</TableCell>
                  <TableCell>Estado</TableCell>
                  <TableCell>Fecha Activación</TableCell>
                  <TableCell>Vence (30 días)</TableCell>
                  <TableCell align="right">Acciones</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {list.map((c, i) => (
                  <TableRow key={c.claimId} hover>
                    <TableCell>{i + 1}</TableCell>
                    <TableCell>{new Date(c.requestedAt).toLocaleDateString('es-ES')}</TableCell>
                    <TableCell>
                      <Typography variant="body2" fontWeight={600}>{c.businessName}</Typography>
                      <Typography variant="caption" color="text.secondary">{c.email}</Typography>
                    </TableCell>
                    <TableCell>
                      <Box display="flex" alignItems="center" gap={0.5}>
                        <Phone fontSize="inherit" color="action" />
                        {c.phoneNumber}
                      </Box>
                    </TableCell>
                    <TableCell>{c.amount} {c.currency}</TableCell>
                    <TableCell>
                      <Tooltip title={c.proofReference}>
                        <Chip
                          size="small"
                          label={CLAIM_STATUS_LABEL[c.claimStatus] ?? c.claimStatus}
                          color={c.claimStatus === 'Approved' ? 'success' : c.claimStatus === 'Rejected' ? 'error' : 'warning'}
                          variant={c.claimStatus === 'Pending' ? 'outlined' : 'filled'}
                        />
                      </Tooltip>
                    </TableCell>
                    <TableCell>
                      <Chip
                        size="small" label={c.isSubscriptionActive ? 'Activo' : 'Inactivo'}
                        color={c.isSubscriptionActive ? 'success' : 'default'} variant="outlined"
                      />
                    </TableCell>
                    <TableCell>{c.lastPaymentDate ? new Date(c.lastPaymentDate).toLocaleDateString('es-ES') : '—'}</TableCell>
                    <TableCell>{c.nextPaymentDate ? new Date(c.nextPaymentDate).toLocaleDateString('es-ES') : '—'}</TableCell>
                    <TableCell align="right">
                      {c.claimStatus === 'Pending' ? (
                        <Box display="flex" justifyContent="flex-end" gap={0.5}>
                          <Tooltip title="Aprobar (registra el pago y renueva 30 días)">
                            <IconButton size="small" color="success" onClick={() => setReviewClaim({ claim: c, action: 'approve' })}>
                              <CheckCircle fontSize="small" />
                            </IconButton>
                          </Tooltip>
                          <Tooltip title="Rechazar">
                            <IconButton size="small" color="error" onClick={() => setReviewClaim({ claim: c, action: 'reject' })}>
                              <Close fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        </Box>
                      ) : (
                        <Typography variant="caption" color="text.secondary">
                          {c.reviewedAt ? new Date(c.reviewedAt).toLocaleDateString('es-ES') : '—'}
                        </Typography>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </CardContent>

      <ReviewClaimDialog
        open={!!reviewClaim}
        claim={reviewClaim?.claim ?? null}
        action={reviewClaim?.action ?? null}
        onClose={() => setReviewClaim(null)}
      />
    </Card>
  );
}

function TenantsPanel() {
  const qc = useQueryClient();
  const [statusFilter, setStatusFilter] = useState('');
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [pendingOnly, setPendingOnly] = useState(false);
  const [suspendTenant, setSuspendTenant] = useState<TenantAdmin | null>(null);
  const [renewTenant, setRenewTenant] = useState<TenantAdmin | null>(null);

  const { data: stats, isLoading: statsLoading } = useAdminStats();
  const { data: overdue } = useOverduePayments();
  const { data: expiringSoon } = useExpiringSoon(3);
  const { data: visits } = useMarketingVisits();
  const { data: tenantsData, isLoading, isError } = useAdminTenants({
    status: statusFilter || undefined,
    search: search || undefined,
    isApproved: pendingOnly ? false : undefined,
  });

  const approveMutation = useMutation({
    mutationFn: (tenantId: string) => api.post(`/api/v1/admin/tenants/${tenantId}/approve`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin'] }),
  });

  const activateMutation = useMutation({
    mutationFn: (tenantId: string) => api.post(`/api/v1/admin/tenants/${tenantId}/activate`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin'] }),
  });

  const deleteMutation = useMutation({
    mutationFn: (tenantId: string) => api.delete(`/api/v1/admin/tenants/${tenantId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin'] }),
  });

  const tenants: TenantAdmin[] = tenantsData?.data ?? (Array.isArray(tenantsData) ? tenantsData as unknown as TenantAdmin[] : []);

  return (
    <Box>
      {!!overdue?.length && (
        <Alert severity="warning" icon={<MoneyOff />} sx={{ mb: 3 }}>
          <Typography variant="body2" fontWeight={700} mb={0.5}>
            {overdue.length} negocio{overdue.length !== 1 ? 's' : ''} con más de 1 mes sin pagar
          </Typography>
          <List dense disablePadding>
            {overdue.map((t) => (
              <ListItem key={t.tenantId} disableGutters sx={{ py: 0.25 }}>
                <ListItemText
                  primary={`${t.businessName} (${t.email})`}
                  secondary={`Último pago: ${new Date(t.lastPaymentDate).toLocaleDateString()} — ${t.daysOverdue} días atrasado`}
                  primaryTypographyProps={{ variant: 'body2', fontWeight: 600 }}
                  secondaryTypographyProps={{ variant: 'caption' }}
                />
              </ListItem>
            ))}
          </List>
        </Alert>
      )}

      {!!expiringSoon?.length && (
        <Alert severity="info" icon={<ScheduleIcon />} sx={{ mb: 3 }}>
          <Typography variant="body2" fontWeight={700} mb={0.5}>
            {expiringSoon.length} negocio{expiringSoon.length !== 1 ? 's' : ''} con la suscripción por vencer en los próximos 3 días
          </Typography>
          <List dense disablePadding>
            {expiringSoon.map((t) => (
              <ListItem key={t.tenantId} disableGutters sx={{ py: 0.25 }}>
                <ListItemText
                  primary={`${t.businessName} (${t.email})`}
                  secondary={`Vence: ${new Date(t.nextPaymentDate).toLocaleDateString('es-ES')} — en ${t.daysUntilExpiry} día${t.daysUntilExpiry !== 1 ? 's' : ''}`}
                  primaryTypographyProps={{ variant: 'body2', fontWeight: 600 }}
                  secondaryTypographyProps={{ variant: 'caption' }}
                />
              </ListItem>
            ))}
          </List>
        </Alert>
      )}

      {!!visits?.length && (
        <Card variant="outlined" sx={{ mb: 3, borderRadius: 2 }}>
          <CardContent>
            <Typography variant="subtitle2" fontWeight={700} mb={1.5}>
              Visitas por origen de campaña (marketing)
            </Typography>
            <TableContainer sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Origen (?src=)</TableCell>
                    <TableCell align="right">Visitas</TableCell>
                    <TableCell>Última visita</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {visits.map((v) => (
                    <TableRow key={v.source} hover>
                      <TableCell>{v.source}</TableCell>
                      <TableCell align="right">
                        <Chip size="small" label={v.visitCount} color="primary" variant="outlined" />
                      </TableCell>
                      <TableCell>{new Date(v.lastVisitAt).toLocaleString()}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </CardContent>
        </Card>
      )}

      {!statsLoading && stats && (
        <Grid container spacing={2} mb={3}>
          <Grid item xs={12} sm={4}>
            <Card variant="outlined" sx={{ borderRadius: 2 }}>
              <CardContent>
                <Box display="flex" alignItems="center" gap={1}>
                  <People color="primary" />
                  <Box>
                    <Typography variant="h5" fontWeight={700}>{stats.totalTenants ?? '—'}</Typography>
                    <Typography variant="caption" color="text.secondary">Total de tenants</Typography>
                  </Box>
                </Box>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={4}>
            <Card variant="outlined" sx={{ borderRadius: 2 }}>
              <CardContent>
                <Box display="flex" alignItems="center" gap={1}>
                  <CheckCircle color="success" />
                  <Box>
                    <Typography variant="h5" fontWeight={700}>{stats.activeTenants ?? '—'}</Typography>
                    <Typography variant="caption" color="text.secondary">Activos</Typography>
                  </Box>
                </Box>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={4}>
            <Card variant="outlined" sx={{ borderRadius: 2 }}>
              <CardContent>
                <Box display="flex" alignItems="center" gap={1}>
                  <Block color="error" />
                  <Box>
                    <Typography variant="h5" fontWeight={700}>{stats.suspendedTenants ?? '—'}</Typography>
                    <Typography variant="caption" color="text.secondary">Suspendidos</Typography>
                  </Box>
                </Box>
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      )}

      <Box display="flex" gap={2} mb={3} flexWrap="wrap" alignItems="center">
        <TextField
          size="small"
          placeholder="Buscar por email o nombre..."
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && setSearch(searchInput)}
          sx={{ flex: 1, minWidth: 220 }}
        />
        <TextField select size="small" label="Estado" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} sx={{ minWidth: 130 }}>
          {STATUS_OPTS.map((s) => <MenuItem key={s} value={s}>{s || 'Todos'}</MenuItem>)}
        </TextField>
        <Button variant="outlined" onClick={() => setSearch(searchInput)}>Buscar</Button>
        <FormControlLabel
          control={<Switch checked={pendingOnly} onChange={(e) => setPendingOnly(e.target.checked)} />}
          label="Solo pendientes de aprobación"
        />
        <Tooltip title="Refrescar">
          <IconButton onClick={() => qc.invalidateQueries({ queryKey: ['admin'] })}>
            <Refresh />
          </IconButton>
        </Tooltip>
      </Box>

      {isError && <Alert severity="error" sx={{ mb: 2 }}>Error al cargar tenants. Verifica que tienes permisos de admin.</Alert>}

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>Negocio</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Email</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Teléfono</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Plan</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Estado</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Suscripción</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Vence</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Registro</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {tenants.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={9} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    No se encontraron tenants
                  </TableCell>
                </TableRow>
              ) : (
                tenants.map((tenant) => (
                  <TableRow key={tenant.id} hover>
                    <TableCell>
                      <Box display="flex" alignItems="center" gap={1}>
                        <Business fontSize="small" color="action" />
                        <Typography variant="body2" fontWeight={600}>{tenant.businessName}</Typography>
                      </Box>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" color="text.secondary">{tenant.email}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" color="text.secondary">{tenant.phoneNumber ?? '—'}</Typography>
                    </TableCell>
                    <TableCell>
                      <Chip label={tenant.plan} size="small" variant="outlined" />
                    </TableCell>
                    <TableCell>
                      <Box display="flex" gap={0.5} flexWrap="wrap">
                        <Chip
                          label={tenant.status}
                          size="small"
                          color={tenant.status === 'Active' ? 'success' : tenant.status === 'Suspended' ? 'error' : 'default'}
                        />
                        {!tenant.isApproved && (
                          <Chip label="Pendiente aprobación" size="small" color="warning" variant="outlined" />
                        )}
                      </Box>
                    </TableCell>
                    <TableCell>
                      <Chip
                        label={tenant.isSubscriptionActive ? 'Activa' : 'Inactiva'}
                        size="small"
                        color={tenant.isSubscriptionActive ? 'success' : 'default'}
                        variant="outlined"
                      />
                    </TableCell>
                    <TableCell>
                      <Typography variant="caption" color="text.secondary">
                        {tenant.nextPaymentDate ? new Date(tenant.nextPaymentDate).toLocaleDateString('es-ES') : '—'}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="caption" color="text.secondary">
                        {tenant.createdAt ? new Date(tenant.createdAt).toLocaleDateString('es-ES') : '—'}
                      </Typography>
                    </TableCell>
                    <TableCell align="right">
                      <Box display="flex" justifyContent="flex-end" gap={0.5}>
                        {!tenant.isApproved && (
                          <Tooltip title="Aprobar (habilita su login)">
                            <IconButton
                              size="small"
                              color="primary"
                              disabled={approveMutation.isPending}
                              onClick={() => approveMutation.mutate(tenant.id)}
                            >
                              <HowToReg fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                        <Tooltip title="Renovar (registrar pago, extiende 30 días)">
                          <IconButton size="small" color="success" onClick={() => setRenewTenant(tenant)}>
                            <Paid fontSize="small" />
                          </IconButton>
                        </Tooltip>
                        {tenant.status === 'Active' ? (
                          <Tooltip title="Suspender">
                            <IconButton size="small" color="warning" onClick={() => setSuspendTenant(tenant)}>
                              <Block fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        ) : (
                          <Tooltip title="Activar">
                            <IconButton
                              size="small"
                              color="success"
                              disabled={activateMutation.isPending}
                              onClick={() => activateMutation.mutate(tenant.id)}
                            >
                              <CheckCircle fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                        <Tooltip title="Eliminar">
                          <IconButton
                            size="small"
                            color="error"
                            disabled={deleteMutation.isPending}
                            onClick={() => {
                              if (window.confirm(`¿Eliminar el tenant "${tenant.businessName}"? Esta acción no se puede deshacer.`)) {
                                deleteMutation.mutate(tenant.id);
                              }
                            }}
                          >
                            <Delete fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </Box>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <SuspendDialog
        open={!!suspendTenant}
        tenant={suspendTenant}
        onClose={() => setSuspendTenant(null)}
      />
      <RenewDialog
        open={!!renewTenant}
        tenant={renewTenant}
        onClose={() => setRenewTenant(null)}
      />

      <PaymentClaimsPanel />
    </Box>
  );
}

// ── Cuentas bloqueadas (impago o suspendidas) ────────────────────────────────
// Vista curada de tenants a los que SubscriptionEnforcementMiddleware les está
// devolviendo 402 en todas sus operaciones. La fuente de verdad es isSubscriptionActive
// del listado general: overdue-payments por sí sola NO alcanza, porque exige
// LastPaymentDate (queda vacía para un tenant que nunca pagó y se le venció el
// período inicial de Trial — bloqueado igual, pero invisible ahí). Se excluyen
// Inactive/Deleted: esos ya fueron dados de baja a propósito, no son "reactivables".
interface BlockedAccountRow {
  id: string;
  businessName: string;
  email: string;
  reason: 'overdue' | 'suspended';
  detail: string;
}

function BlockedAccountsPanel() {
  const qc = useQueryClient();
  const [renewTenant, setRenewTenant] = useState<{ id: string; businessName: string } | null>(null);

  const { data: overdue } = useOverduePayments();
  const { data: tenantsData, isLoading } = useAdminTenants({ pageSize: 100 });

  const activateMutation = useMutation({
    mutationFn: (tenantId: string) => api.post(`/api/v1/admin/tenants/${tenantId}/activate`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin'] }),
  });

  const overdueByTenantId = new Map((overdue ?? []).map((t) => [t.tenantId, t]));

  const rows: BlockedAccountRow[] = (tenantsData?.data ?? [])
    .filter((t) => t.isApproved && !t.isSubscriptionActive && t.status !== 'Inactive' && t.status !== 'Deleted')
    .map((t) => {
      const od = overdueByTenantId.get(t.id);
      const isSuspended = t.status === 'Suspended';
      return {
        id: t.id,
        businessName: t.businessName,
        email: t.email,
        reason: isSuspended ? ('suspended' as const) : ('overdue' as const),
        detail: od
          ? `Último pago: ${new Date(od.lastPaymentDate).toLocaleDateString('es-ES')} — ${od.daysOverdue} días atrasado`
          : isSuspended
            ? 'Suspendida manualmente por un admin'
            : 'Sin pagos registrados: venció el período inicial sin renovar',
      };
    });

  return (
    <Box>
      <Alert severity="info" sx={{ mb: 3 }}>
        Tenants a los que el sistema les está bloqueando TODAS las operaciones (respuesta 402,
        ver SubscriptionEnforcementMiddleware) por atraso de pago o por suspensión manual.
        "Renovar" registra el pago y reactiva — es la única acción que también extiende la
        próxima fecha de pago, así que úsala cuando el motivo sea impago. Si el dueño tiene una
        instalación Local emparejada, se desbloquea sola en cuanto sincronice.
      </Alert>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : rows.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: 'center', borderRadius: 2 }}>
          <Typography variant="body2" color="text.secondary">No hay cuentas bloqueadas ahora mismo.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>Negocio</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Email</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Motivo</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Detalle</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.id} hover>
                  <TableCell>
                    <Box display="flex" alignItems="center" gap={1}>
                      <Business fontSize="small" color="action" />
                      <Typography variant="body2" fontWeight={600}>{row.businessName}</Typography>
                    </Box>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" color="text.secondary">{row.email}</Typography>
                  </TableCell>
                  <TableCell>
                    <Chip
                      label={row.reason === 'overdue' ? 'Pago atrasado' : 'Suspendida'}
                      size="small"
                      color="error"
                      icon={row.reason === 'overdue' ? <MoneyOff fontSize="small" /> : <Block fontSize="small" />}
                    />
                  </TableCell>
                  <TableCell>
                    <Typography variant="caption" color="text.secondary">{row.detail}</Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Box display="flex" justifyContent="flex-end" gap={0.5}>
                      <Tooltip title="Renovar (registrar pago, extiende 30 días y reactiva)">
                        <IconButton
                          size="small"
                          color="success"
                          onClick={() => setRenewTenant({ id: row.id, businessName: row.businessName })}
                        >
                          <Paid fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      {row.reason === 'suspended' && (
                        <Tooltip title="Activar sin registrar pago (solo si la suspensión no fue por impago)">
                          <IconButton
                            size="small"
                            color="warning"
                            disabled={activateMutation.isPending}
                            onClick={() => activateMutation.mutate(row.id)}
                          >
                            <CheckCircle fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <RenewDialog
        open={!!renewTenant}
        tenant={renewTenant}
        onClose={() => setRenewTenant(null)}
      />
    </Box>
  );
}

// ── Client management (app móvil) ────────────────────────────────────────────

function useAdminClients(params: { search?: string; page?: number; isApproved?: boolean }) {
  return useQuery<{ data: ClientAdmin[]; total?: number }>({
    queryKey: ['admin', 'clients', params],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/clients', {
        params: { ...params, isApproved: params.isApproved },
      });
      const payload = res.data?.data ?? res.data;
      const items: ClientAdmin[] = payload?.items ?? (Array.isArray(payload) ? payload : []);
      return { data: items, total: payload?.totalCount };
    },
  });
}

function useExpiringClients(days = 3) {
  return useQuery<ExpiringClient[]>({
    queryKey: ['admin', 'clients', 'expiring-soon', days],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/clients/expiring-soon', { params: { days } });
      const payload = res.data?.data ?? res.data;
      return Array.isArray(payload) ? payload : [];
    },
  });
}

function usePremiumClaims() {
  return useQuery<AdminPremiumClaim[]>({
    queryKey: ['admin', 'clients', 'premium-claims'],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/clients/premium-claims');
      const payload = res.data?.data ?? res.data;
      return Array.isArray(payload) ? payload : [];
    },
  });
}

interface ReviewPremiumClaimDialogProps {
  open: boolean;
  claim: AdminPremiumClaim | null;
  action: 'approve' | 'reject' | null;
  onClose: () => void;
}

function ReviewPremiumClaimDialog({ open, claim, action, onClose }: ReviewPremiumClaimDialogProps) {
  const qc = useQueryClient();
  const [note, setNote] = useState('');
  const isApprove = action === 'approve';

  const mutation = useMutation({
    mutationFn: () => api.post(
      `/api/v1/admin/clients/${claim?.clientId}/premium-claims/${claim?.claimId}/${action}`,
      { note: note || null },
    ),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin', 'clients'] });
      setNote('');
      onClose();
    },
  });

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isApprove ? 'Aprobar pago' : 'Rechazar reporte'} — {claim?.fullName}</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" mb={2}>
          {isApprove
            ? `Se registrará el pago de ${claim?.amount} ${claim?.currency} y el Premium se extenderá 30 días desde hoy.`
            : 'El reporte quedará marcado como rechazado. No se concede Premium.'}
        </Typography>
        <Box display="flex" gap={3} mb={2} flexWrap="wrap">
          <Typography variant="body2"><b>Teléfono:</b> {claim?.phoneNumber}</Typography>
          <Typography variant="body2"><b>Comprobante:</b> {claim?.proofReference}</Typography>
        </Box>
        <TextField
          fullWidth label="Nota (opcional)" value={note}
          onChange={(e) => setNote(e.target.value)} multiline rows={2}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button
          variant="contained" color={isApprove ? 'success' : 'error'}
          disabled={mutation.isPending}
          onClick={() => mutation.mutate()}
        >
          {mutation.isPending ? 'Guardando...' : isApprove ? 'Aprobar y extender' : 'Rechazar'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function PremiumClaimsPanel() {
  const { data: claims, isLoading } = usePremiumClaims();
  const [reviewClaim, setReviewClaim] = useState<{ claim: AdminPremiumClaim; action: 'approve' | 'reject' } | null>(null);

  const list = claims ?? [];

  return (
    <Card variant="outlined" sx={{ mt: 3, borderRadius: 2 }}>
      <CardContent>
        <Typography variant="subtitle2" fontWeight={700} mb={1.5}>
          Pagos de Premium reportados por los clientes
        </Typography>
        {isLoading ? (
          <Box display="flex" justifyContent="center" py={4}><CircularProgress size={28} /></Box>
        ) : list.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
            Todavía no hay reportes de pago.
          </Typography>
        ) : (
          <TableContainer sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>#</TableCell>
                  <TableCell>Fecha</TableCell>
                  <TableCell>Usuario</TableCell>
                  <TableCell>Teléfono</TableCell>
                  <TableCell>Monto</TableCell>
                  <TableCell>Comprobante</TableCell>
                  <TableCell>Plan</TableCell>
                  <TableCell>Vence</TableCell>
                  <TableCell align="right">Acciones</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {list.map((c, i) => (
                  <TableRow key={c.claimId} hover>
                    <TableCell>{i + 1}</TableCell>
                    <TableCell>{new Date(c.requestedAt).toLocaleDateString('es-ES')}</TableCell>
                    <TableCell>
                      <Typography variant="body2" fontWeight={600}>{c.fullName}</Typography>
                      <Typography variant="caption" color="text.secondary">{c.email}</Typography>
                    </TableCell>
                    <TableCell>
                      <Box display="flex" alignItems="center" gap={0.5}>
                        <Phone fontSize="inherit" color="action" />
                        {c.phoneNumber}
                      </Box>
                    </TableCell>
                    <TableCell>{c.amount} {c.currency}</TableCell>
                    <TableCell>
                      <Tooltip title={c.proofReference}>
                        <Chip
                          size="small"
                          label={CLAIM_STATUS_LABEL[c.claimStatus] ?? c.claimStatus}
                          color={c.claimStatus === 'Approved' ? 'success' : c.claimStatus === 'Rejected' ? 'error' : 'warning'}
                          variant={c.claimStatus === 'Pending' ? 'outlined' : 'filled'}
                        />
                      </Tooltip>
                    </TableCell>
                    <TableCell>
                      <Chip
                        size="small" label={c.plan}
                        color={c.plan === 'Premium' ? 'secondary' : 'default'} variant="outlined"
                      />
                    </TableCell>
                    <TableCell>{c.premiumUntil ? new Date(c.premiumUntil).toLocaleDateString('es-ES') : '—'}</TableCell>
                    <TableCell align="right">
                      {c.claimStatus === 'Pending' ? (
                        <Box display="flex" justifyContent="flex-end" gap={0.5}>
                          <Tooltip title="Aprobar (extiende Premium 30 días)">
                            <IconButton size="small" color="success" onClick={() => setReviewClaim({ claim: c, action: 'approve' })}>
                              <CheckCircle fontSize="small" />
                            </IconButton>
                          </Tooltip>
                          <Tooltip title="Rechazar">
                            <IconButton size="small" color="error" onClick={() => setReviewClaim({ claim: c, action: 'reject' })}>
                              <Close fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        </Box>
                      ) : (
                        <Typography variant="caption" color="text.secondary">
                          {c.reviewedAt ? new Date(c.reviewedAt).toLocaleDateString('es-ES') : '—'}
                        </Typography>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </CardContent>

      <ReviewPremiumClaimDialog
        open={!!reviewClaim}
        claim={reviewClaim?.claim ?? null}
        action={reviewClaim?.action ?? null}
        onClose={() => setReviewClaim(null)}
      />
    </Card>
  );
}

function ClientsPanel() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [pendingOnly, setPendingOnly] = useState(false);

  const { data: clientsData, isLoading, isError } = useAdminClients({
    search: search || undefined,
    isApproved: pendingOnly ? false : undefined,
  });
  const { data: expiringSoon } = useExpiringClients(3);

  const approveMutation = useMutation({
    mutationFn: (clientId: string) => api.post(`/api/v1/admin/clients/${clientId}/approve`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'clients'] }),
  });

  const rejectMutation = useMutation({
    mutationFn: (clientId: string) => api.delete(`/api/v1/admin/clients/${clientId}/approve`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'clients'] }),
  });

  const grantPremiumMutation = useMutation({
    mutationFn: (clientId: string) => api.post(`/api/v1/admin/clients/${clientId}/premium`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'clients'] }),
  });

  const revokePremiumMutation = useMutation({
    mutationFn: (clientId: string) => api.delete(`/api/v1/admin/clients/${clientId}/premium`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'clients'] }),
  });

  const rejectPremiumRequestMutation = useMutation({
    mutationFn: (clientId: string) => api.delete(`/api/v1/admin/clients/${clientId}/premium-request`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'clients'] }),
  });

  const clients: ClientAdmin[] = clientsData?.data ?? [];

  return (
    <Box>
      {!!expiringSoon?.length && (
        <Alert severity="info" icon={<ScheduleIcon />} sx={{ mb: 3 }}>
          <Typography variant="body2" fontWeight={700} mb={0.5}>
            {expiringSoon.length} cliente{expiringSoon.length !== 1 ? 's' : ''} Premium con el período por vencer en los próximos 3 días
          </Typography>
          <List dense disablePadding>
            {expiringSoon.map((c) => (
              <ListItem key={c.clientId} disableGutters sx={{ py: 0.25 }}>
                <ListItemText
                  primary={`${c.fullName} (${c.email})`}
                  secondary={`Vence: ${new Date(c.premiumUntil).toLocaleDateString('es-ES')} — en ${c.daysUntilExpiry} día${c.daysUntilExpiry !== 1 ? 's' : ''}`}
                  primaryTypographyProps={{ variant: 'body2', fontWeight: 600 }}
                  secondaryTypographyProps={{ variant: 'caption' }}
                />
              </ListItem>
            ))}
          </List>
        </Alert>
      )}

      <Box display="flex" gap={2} mb={3} flexWrap="wrap" alignItems="center">
        <TextField
          size="small"
          placeholder="Buscar por email o nombre..."
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && setSearch(searchInput)}
          sx={{ flex: 1, minWidth: 220 }}
        />
        <Button variant="outlined" onClick={() => setSearch(searchInput)}>Buscar</Button>
        <FormControlLabel
          control={<Switch checked={pendingOnly} onChange={(e) => setPendingOnly(e.target.checked)} />}
          label="Solo pendientes de aprobación"
        />
        <Tooltip title="Refrescar">
          <IconButton onClick={() => qc.invalidateQueries({ queryKey: ['admin', 'clients'] })}>
            <Refresh />
          </IconButton>
        </Tooltip>
      </Box>

      {isError && <Alert severity="error" sx={{ mb: 2 }}>Error al cargar clientes. Verifica que tienes permisos de admin.</Alert>}

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>Nombre</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Email</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Teléfono</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Plan</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Estado</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Vence</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Reputación</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Registro</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {clients.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={9} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    No se encontraron clientes
                  </TableCell>
                </TableRow>
              ) : (
                clients.map((client) => (
                  <TableRow key={client.id} hover>
                    <TableCell>
                      <Box display="flex" alignItems="center" gap={1}>
                        <People fontSize="small" color="action" />
                        <Typography variant="body2" fontWeight={600}>{client.fullName}</Typography>
                      </Box>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" color="text.secondary">{client.email}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" color="text.secondary">{client.phoneNumber ?? '—'}</Typography>
                    </TableCell>
                    <TableCell>
                      <Box display="flex" alignItems="center" gap={0.5} flexWrap="wrap">
                        <Chip
                          label={client.plan}
                          size="small"
                          variant="outlined"
                          color={client.plan === 'Premium' ? 'secondary' : 'default'}
                        />
                        {client.premiumRequested && client.plan !== 'Premium' && (
                          <Chip label="Solicitó Premium" size="small" color="warning" />
                        )}
                      </Box>
                    </TableCell>
                    <TableCell>
                      {client.isApproved ? (
                        <Chip label="Aprobado" size="small" color="success" variant="outlined" />
                      ) : (
                        <Chip label="Pendiente aprobación" size="small" color="warning" variant="outlined" />
                      )}
                    </TableCell>
                    <TableCell>
                      <Typography variant="caption" color="text.secondary">
                        {client.premiumUntil ? new Date(client.premiumUntil).toLocaleDateString('es-ES') : '—'}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" color="text.secondary">{client.reputationScore}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="caption" color="text.secondary">
                        {client.createdAt ? new Date(client.createdAt).toLocaleDateString('es-ES') : '—'}
                      </Typography>
                    </TableCell>
                    <TableCell align="right">
                      {!client.isApproved ? (
                        <Tooltip title="Aprobar (habilita su login)">
                          <IconButton
                            size="small"
                            color="primary"
                            disabled={approveMutation.isPending}
                            onClick={() => approveMutation.mutate(client.id)}
                          >
                            <HowToReg fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      ) : (
                        <Tooltip title="Desaprobar (bloquea su login y cierra su sesión)">
                          <IconButton
                            size="small"
                            color="error"
                            disabled={rejectMutation.isPending}
                            onClick={() => rejectMutation.mutate(client.id)}
                          >
                            <Block fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                      {client.plan === 'Premium' ? (
                        <Tooltip title="Quitar Premium (vuelve a Normal)">
                          <IconButton
                            size="small"
                            color="secondary"
                            disabled={revokePremiumMutation.isPending}
                            onClick={() => revokePremiumMutation.mutate(client.id)}
                          >
                            <Star fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      ) : client.premiumRequested ? (
                        <>
                          <Tooltip title="Aprobar solicitud de Premium">
                            <IconButton
                              size="small"
                              color="success"
                              disabled={grantPremiumMutation.isPending}
                              onClick={() => grantPremiumMutation.mutate(client.id)}
                            >
                              <CheckCircle fontSize="small" />
                            </IconButton>
                          </Tooltip>
                          <Tooltip title="Rechazar solicitud de Premium">
                            <IconButton
                              size="small"
                              color="error"
                              disabled={rejectPremiumRequestMutation.isPending}
                              onClick={() => rejectPremiumRequestMutation.mutate(client.id)}
                            >
                              <Block fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        </>
                      ) : (
                        <Tooltip title="Conceder Premium">
                          <IconButton
                            size="small"
                            color="secondary"
                            disabled={grantPremiumMutation.isPending}
                            onClick={() => grantPremiumMutation.mutate(client.id)}
                          >
                            <StarBorder fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <PremiumClaimsPanel />
    </Box>
  );
}

// ── Referidos (recompensa en CUP) ───────────────────────────────────────────

function useReferralPayouts() {
  return useQuery<ReferralPayoutAdmin[]>({
    queryKey: ['admin', 'referrals', 'payouts'],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/referrals/payouts');
      const payload = res.data?.data ?? res.data;
      return Array.isArray(payload) ? payload : [];
    },
  });
}

function useReferralLeaderboard() {
  return useQuery<ReferralLeaderboard>({
    queryKey: ['admin', 'referrals', 'leaderboard'],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/referrals/leaderboard');
      return res.data?.data ?? res.data;
    },
  });
}

function ReferralsPanel() {
  const qc = useQueryClient();
  const [closeResult, setCloseResult] = useState<MonthlySettlementReceipt | null>(null);
  const [closeError, setCloseError] = useState<string | null>(null);

  const { data: payouts, isLoading: payoutsLoading, isError: payoutsError } = useReferralPayouts();
  const { data: leaderboard, isLoading: leaderboardLoading } = useReferralLeaderboard();

  const settleMutation = useMutation({
    mutationFn: (clientId: string) => api.post(`/api/v1/admin/referrals/${clientId}/settle-payout`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'referrals', 'payouts'] }),
  });

  const closeMonthMutation = useMutation({
    mutationFn: () => api.post('/api/v1/admin/referrals/close-month'),
    onSuccess: (res) => {
      setCloseError(null);
      setCloseResult((res.data?.data ?? res.data) as MonthlySettlementReceipt);
      qc.invalidateQueries({ queryKey: ['admin', 'referrals'] });
    },
    onError: (err: unknown) => {
      const message = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
        ?? 'No se pudo cerrar la competencia mensual.';
      setCloseError(message);
    },
  });

  const monthLabel = leaderboard
    ? new Date(leaderboard.year, leaderboard.month - 1, 1).toLocaleDateString('es-ES', { month: 'long', year: 'numeric' })
    : '';

  return (
    <Box>
      {closeResult && (
        <Alert severity="success" sx={{ mb: 3 }} onClose={() => setCloseResult(null)}>
          {closeResult.winnerClientId
            ? `${closeResult.winnerName} ganó ${closeResult.prizeAmount} CUP (${closeResult.totalValidReferrals} referidos válidos en ${closeResult.month}/${closeResult.year}).`
            : `Nadie tuvo referidos válidos en ${closeResult.month}/${closeResult.year}: nada que premiar.`}
        </Alert>
      )}
      {closeError && (
        <Alert severity="error" sx={{ mb: 3 }} onClose={() => setCloseError(null)}>{closeError}</Alert>
      )}

      <Card variant="outlined" sx={{ mb: 3, borderRadius: 2 }}>
        <CardContent>
          <Box display="flex" alignItems="center" justifyContent="space-between" mb={1.5} flexWrap="wrap" gap={1}>
            <Typography variant="subtitle2" fontWeight={700}>
              Competencia mensual — {monthLabel || 'mes actual'}
            </Typography>
            <Button
              variant="contained"
              color="secondary"
              size="small"
              startIcon={<EmojiEvents fontSize="small" />}
              disabled={closeMonthMutation.isPending}
              onClick={() => {
                if (window.confirm('¿Cerrar la competencia mensual? Esto premia al top 1 de referidos válidos del mes y no se puede repetir para el mismo mes.')) {
                  closeMonthMutation.mutate();
                }
              }}
            >
              {closeMonthMutation.isPending ? 'Cerrando...' : 'Cerrar mes y premiar'}
            </Button>
          </Box>

          {leaderboardLoading ? (
            <Box display="flex" justifyContent="center" py={4}><CircularProgress size={24} /></Box>
          ) : !leaderboard || leaderboard.top.length === 0 ? (
            <Typography variant="body2" color="text.secondary">Nadie tiene referidos válidos este mes todavía.</Typography>
          ) : (
            <TableContainer sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>#</TableCell>
                    <TableCell>Cliente</TableCell>
                    <TableCell align="right">Referidos válidos</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {leaderboard.top.map((entry) => (
                    <TableRow key={entry.rank} hover selected={entry.isMe}>
                      <TableCell>{entry.rank}</TableCell>
                      <TableCell>{entry.name}</TableCell>
                      <TableCell align="right">
                        <Chip size="small" label={entry.validReferralsThisMonth} color={entry.rank === 1 ? 'secondary' : 'default'} variant="outlined" />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </CardContent>
      </Card>

      <Typography variant="subtitle2" fontWeight={700} mb={0.5}>
        Clientes con actividad de referidos
      </Typography>
      <Typography variant="body2" color="text.secondary" mb={1.5}>
        Cuánto acumuló cada uno por invitados (10 CUP fijos c/u) y por premios de la competencia
        mensual, cuánto queda pendiente de pago y cuánto ya se le liquidó.
      </Typography>

      {payoutsError && <Alert severity="error" sx={{ mb: 2 }}>Error al cargar la actividad de referidos.</Alert>}

      {payoutsLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : !payouts || payouts.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: 'center', borderRadius: 2 }}>
          <Typography variant="body2" color="text.secondary">Todavía nadie tiene actividad de referidos.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>Cliente</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Email</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>Invitados válidos</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>CUP por invitados</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>CUP por competencia</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>Saldo pendiente</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>Ya pagado (histórico)</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {payouts.map((p) => (
                <TableRow key={p.clientId} hover>
                  <TableCell>
                    <Box display="flex" alignItems="center" gap={1}>
                      <People fontSize="small" color="action" />
                      <Typography variant="body2" fontWeight={600}>{p.fullName}</Typography>
                    </Box>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" color="text.secondary">{p.email}</Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Typography variant="body2" color="text.secondary">{p.validReferralsTotal}</Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Typography variant="body2" color="text.secondary">{p.referralBonusTotal} CUP</Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Typography variant="body2" color="text.secondary">{p.competitionPrizeTotal} CUP</Typography>
                  </TableCell>
                  <TableCell align="right">
                    {p.cupBalance > 0 ? (
                      <Chip size="small" label={`${p.cupBalance} CUP`} color="warning" />
                    ) : (
                      <Typography variant="body2" color="text.secondary">0 CUP</Typography>
                    )}
                  </TableCell>
                  <TableCell align="right">
                    <Typography variant="body2" color="text.secondary">{p.cupPaidTotal} CUP</Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title={p.cupBalance > 0 ? 'Marcar como pagado (fuera del sistema) y resetear el saldo a 0' : 'No tiene saldo pendiente'}>
                      <span>
                        <IconButton
                          size="small"
                          color="success"
                          disabled={p.cupBalance <= 0 || settleMutation.isPending}
                          onClick={() => {
                            if (window.confirm(`¿Confirmas que ya le pagaste ${p.cupBalance} CUP a ${p.fullName}? Esto resetea su saldo a 0.`)) {
                              settleMutation.mutate(p.clientId);
                            }
                          }}
                        >
                          <LocalAtm fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}

// ── Admin Chat ───────────────────────────────────────────────────────────────

interface Conversation {
  tenantId: string;
  businessName?: string;
  email?: string;
  lastMessage?: string;
  unreadCount?: number;
  updatedAt?: string;
}

interface ChatMessage {
  id: string;
  text: string;
  isFromAdmin?: boolean;
  createdAt?: string;
}

function useConversations() {
  return useQuery<Conversation[]>({
    queryKey: ['admin', 'chat', 'conversations'],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/chat/conversations');
      // Backend: ConversationSummaryDto { tenantId, tenantName, lastMessage, lastMessageAt, unreadFromTenant }
      const payload = res.data.data ?? res.data;
      const items: Array<Record<string, unknown>> = payload?.items ?? (Array.isArray(payload) ? payload : []);
      return items.map((c) => ({
        tenantId: c.tenantId as string,
        businessName: (c.tenantName as string) ?? (c.businessName as string),
        lastMessage: c.lastMessage as string,
        unreadCount: (c.unreadFromTenant as number) ?? (c.unreadCount as number) ?? 0,
        updatedAt: (c.lastMessageAt as string) ?? (c.updatedAt as string),
      }));
    },
  });
}

function useConversationMessages(tenantId: string | null, page = 1) {
  return useQuery<ChatMessage[]>({
    queryKey: ['admin', 'chat', 'messages', tenantId, page],
    queryFn: async () => {
      const res = await api.get(`/api/v1/admin/chat/conversations/${tenantId}`, {
        params: { page, pageSize: 50 },
      });
      // Backend: PagedResult<ChatMessageDto> con senderType 'Admin' | 'Tenant' y sentAt
      const payload = res.data.data ?? res.data;
      const items: Array<Record<string, unknown>> = payload?.items ?? (Array.isArray(payload) ? payload : []);
      return items.map((m) => ({
        id: m.id as string,
        text: (m.text as string) ?? '',
        isFromAdmin: m.senderType === 'Admin' || (m.isFromAdmin as boolean) === true,
        createdAt: (m.sentAt as string) ?? (m.createdAt as string),
      }));
    },
    enabled: !!tenantId,
  });
}

function AdminChatPanel() {
  const qc = useQueryClient();
  const { data: conversations, isLoading } = useConversations();
  const [selectedTenant, setSelectedTenant] = useState<Conversation | null>(null);
  const [replyText, setReplyText] = useState('');

  const { data: messages, isLoading: messagesLoading } = useConversationMessages(selectedTenant?.tenantId ?? null);

  const replyMutation = useMutation({
    mutationFn: (text: string) =>
      api.post(`/api/v1/admin/chat/conversations/${selectedTenant?.tenantId}/reply`, { text }),
    onSuccess: () => {
      setReplyText('');
      qc.invalidateQueries({ queryKey: ['admin', 'chat', 'messages', selectedTenant?.tenantId] });
      qc.invalidateQueries({ queryKey: ['admin', 'chat', 'conversations'] });
    },
  });

  const handleReply = () => {
    const text = replyText.trim();
    if (!text || replyMutation.isPending) return;
    replyMutation.mutate(text);
  };

  return (
    <Box
      display="flex"
      flexDirection={{ xs: 'column', sm: 'row' }}
      gap={2}
      height={{ xs: 'auto', sm: '60vh' }}
      minHeight={400}
    >
      {/* Conversation list */}
      <Paper
        variant="outlined"
        sx={{
          width: { xs: '100%', sm: 260 },
          maxHeight: { xs: 240, sm: 'none' },
          flexShrink: 0,
          borderRadius: 2,
          overflow: 'hidden',
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        <Box px={2} py={1.5} borderBottom="1px solid" sx={{ borderColor: 'divider' }}>
          <Typography variant="subtitle2" fontWeight={700}>Conversaciones</Typography>
        </Box>
        {isLoading ? (
          <Box display="flex" justifyContent="center" py={4}><CircularProgress size={24} /></Box>
        ) : !conversations || conversations.length === 0 ? (
          <Box display="flex" alignItems="center" justifyContent="center" flex={1} p={2}>
            <Typography variant="body2" color="text.secondary" textAlign="center">
              No hay conversaciones
            </Typography>
          </Box>
        ) : (
          <List disablePadding sx={{ overflow: 'auto', flex: 1 }}>
            {conversations.map((conv) => (
              <ListItemButton
                key={conv.tenantId}
                selected={selectedTenant?.tenantId === conv.tenantId}
                onClick={() => setSelectedTenant(conv)}
                sx={{ borderBottom: '1px solid', borderColor: 'divider' }}
              >
                <Avatar sx={{ width: 32, height: 32, mr: 1.5, fontSize: 13, bgcolor: 'primary.main' }}>
                  {conv.businessName?.[0]?.toUpperCase() ?? '?'}
                </Avatar>
                <ListItemText
                  primary={conv.businessName ?? conv.email ?? conv.tenantId.slice(0, 8)}
                  secondary={conv.lastMessage ? conv.lastMessage.slice(0, 30) + (conv.lastMessage.length > 30 ? '…' : '') : undefined}
                  primaryTypographyProps={{ variant: 'body2', fontWeight: 600, noWrap: true }}
                  secondaryTypographyProps={{ variant: 'caption', noWrap: true }}
                />
                {(conv.unreadCount ?? 0) > 0 && (
                  <Chip label={conv.unreadCount} size="small" color="error" sx={{ ml: 1, height: 18, fontSize: '0.65rem' }} />
                )}
              </ListItemButton>
            ))}
          </List>
        )}
      </Paper>

      {/* Message pane */}
      <Paper
        variant="outlined"
        sx={{
          flex: 1,
          minHeight: { xs: 320, sm: 'auto' },
          borderRadius: 2,
          overflow: 'hidden',
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        {!selectedTenant ? (
          <Box display="flex" alignItems="center" justifyContent="center" flex={1} flexDirection="column" gap={1}>
            <ChatIcon sx={{ fontSize: 48, color: 'text.disabled' }} />
            <Typography variant="body2" color="text.secondary">
              Selecciona una conversación
            </Typography>
          </Box>
        ) : (
          <>
            <Box px={2} py={1.5} borderBottom="1px solid" sx={{ borderColor: 'divider' }}>
              <Typography variant="subtitle2" fontWeight={700}>
                {selectedTenant.businessName ?? selectedTenant.email ?? selectedTenant.tenantId}
              </Typography>
            </Box>

            <Box flex={1} overflow="auto" p={2} display="flex" flexDirection="column" gap={1.5}>
              {messagesLoading ? (
                <Box display="flex" justifyContent="center" py={4}><CircularProgress size={24} /></Box>
              ) : !messages || messages.length === 0 ? (
                <Typography variant="body2" color="text.secondary" textAlign="center" mt={4}>
                  Sin mensajes
                </Typography>
              ) : (
                messages.map((msg) => (
                  <Box
                    key={msg.id}
                    display="flex"
                    justifyContent={msg.isFromAdmin ? 'flex-end' : 'flex-start'}
                  >
                    <Box
                      sx={{
                        maxWidth: '75%', px: 1.5, py: 1,
                        borderRadius: msg.isFromAdmin ? '16px 16px 4px 16px' : '16px 16px 16px 4px',
                        bgcolor: msg.isFromAdmin ? 'primary.main' : 'action.hover',
                        color: msg.isFromAdmin ? 'white' : 'text.primary',
                      }}
                    >
                      <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', lineHeight: 1.5 }}>
                        {msg.text}
                      </Typography>
                      {msg.createdAt && (
                        <Typography variant="caption" sx={{ opacity: 0.7, display: 'block', mt: 0.25 }}>
                          {new Date(msg.createdAt).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })}
                        </Typography>
                      )}
                    </Box>
                  </Box>
                ))
              )}
            </Box>

            <Divider />
            <Box p={1.5} display="flex" gap={1} alignItems="flex-end">
              <TextField
                fullWidth
                multiline
                maxRows={3}
                size="small"
                placeholder="Escribe una respuesta…"
                value={replyText}
                onChange={(e) => setReplyText(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleReply(); }
                }}
                disabled={replyMutation.isPending}
              />
              <IconButton
                onClick={handleReply}
                disabled={!replyText.trim() || replyMutation.isPending}
                sx={{ bgcolor: 'primary.main', color: 'white', '&:hover': { bgcolor: 'primary.dark' }, '&.Mui-disabled': { bgcolor: 'action.disabledBackground' } }}
              >
                {replyMutation.isPending ? <CircularProgress size={18} color="inherit" /> : <SendIcon fontSize="small" />}
              </IconButton>
            </Box>
          </>
        )}
      </Paper>
    </Box>
  );
}

// ── Main page ────────────────────────────────────────────────────────────────

export default function AdminPage() {
  const [tab, setTab] = useState(0);

  return (
    <Box>
      <Typography variant="h5" fontWeight={700} mb={0.5}>Panel de Administración</Typography>
      <Typography variant="body2" color="text.secondary" mb={3}>
        Gestión de tenants y conversaciones
      </Typography>

      <Tabs
        value={tab}
        onChange={(_, v) => setTab(v)}
        variant="scrollable"
        scrollButtons="auto"
        allowScrollButtonsMobile
        sx={{ mb: 3, borderBottom: '1px solid', borderColor: 'divider' }}
      >
        <Tab label="Tenants" icon={<Business fontSize="small" />} iconPosition="start" sx={{ fontWeight: 600, minHeight: 48 }} />
        <Tab label="Bloqueadas" icon={<Block fontSize="small" />} iconPosition="start" sx={{ fontWeight: 600, minHeight: 48 }} />
        <Tab label="Clientes" icon={<People fontSize="small" />} iconPosition="start" sx={{ fontWeight: 600, minHeight: 48 }} />
        <Tab label="Referidos" icon={<EmojiEvents fontSize="small" />} iconPosition="start" sx={{ fontWeight: 600, minHeight: 48 }} />
        <Tab label="MiPymes Referidas" icon={<EmojiEvents fontSize="small" />} iconPosition="start" sx={{ fontWeight: 600, minHeight: 48 }} />
        <Tab label="Chat" icon={<ChatIcon fontSize="small" />} iconPosition="start" sx={{ fontWeight: 600, minHeight: 48 }} />
      </Tabs>

      {tab === 0 && <TenantsPanel />}
      {tab === 1 && <BlockedAccountsPanel />}
      {tab === 2 && <ClientsPanel />}
      {tab === 3 && <ReferralsPanel />}
      {tab === 4 && <MipymeReferralsPanel />}
      {tab === 5 && <AdminChatPanel />}
    </Box>
  );
}
