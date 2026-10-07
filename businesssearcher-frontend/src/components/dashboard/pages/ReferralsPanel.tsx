import { useState } from 'react';
import {
  Box, Typography, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Paper, CircularProgress, Button,
  Alert, IconButton, Tooltip, Dialog, DialogTitle, DialogContent, DialogActions,
} from '@mui/material';
import { Paid, Refresh, EmojiEvents } from '@mui/icons-material';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/apiClient';
import type { ReferralPayoutAdmin, ReferralLeaderboard, MonthlySettlementReceipt } from '@/lib/types';

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

const currency = (amount: number) => `${amount.toFixed(2)} CUP`;

export default function ReferralsPanel() {
  const qc = useQueryClient();
  const [receipt, setReceipt] = useState<MonthlySettlementReceipt | null>(null);

  const { data: payouts, isLoading: loadingPayouts, isError: payoutsError } = useReferralPayouts();
  const { data: leaderboard, isLoading: loadingLeaderboard } = useReferralLeaderboard();

  const settlePayoutMutation = useMutation({
    mutationFn: (clientId: string) => api.post(`/api/v1/admin/referrals/${clientId}/settle-payout`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'referrals', 'payouts'] }),
  });

  const closeMonthMutation = useMutation({
    mutationFn: () => api.post('/api/v1/admin/referrals/close-month'),
    onSuccess: (res) => {
      setReceipt(res.data?.data ?? res.data);
      qc.invalidateQueries({ queryKey: ['admin', 'referrals'] });
    },
  });

  const refreshAll = () => qc.invalidateQueries({ queryKey: ['admin', 'referrals'] });

  return (
    <Box>
      <Box display="flex" gap={2} mb={3} flexWrap="wrap" alignItems="center" justifyContent="space-between">
        <Typography variant="body2" color="text.secondary">
          Saldos de CUP por referidos pendientes de pago, y competencia mensual (el 1.º lugar gana un premio).
        </Typography>
        <Box display="flex" gap={1}>
          <Tooltip title="Refrescar">
            <IconButton onClick={refreshAll}><Refresh /></IconButton>
          </Tooltip>
          <Button
            variant="contained"
            color="secondary"
            startIcon={<EmojiEvents />}
            disabled={closeMonthMutation.isPending}
            onClick={() => {
              if (window.confirm('¿Cerrar la competencia del mes actual y premiar al 1.º lugar? Esta acción no se puede repetir para el mismo mes.')) {
                closeMonthMutation.mutate();
              }
            }}
          >
            Cerrar mes y premiar top 1
          </Button>
        </Box>
      </Box>

      {closeMonthMutation.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          No se pudo cerrar el mes (¿ya estaba cerrado?).
        </Alert>
      )}

      <Typography variant="subtitle1" fontWeight={700} mb={1}>Saldos pendientes de pago</Typography>
      {payoutsError && <Alert severity="error" sx={{ mb: 2 }}>Error al cargar los saldos. Verifica que tienes permisos de admin.</Alert>}
      {loadingPayouts ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2, mb: 4 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>Nombre</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Email</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>MiPymes referidas</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Bono/Premio MiPymes (CUP)</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Saldo pendiente</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Pagado histórico</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {!payouts || payouts.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    No hay saldos pendientes de pago
                  </TableCell>
                </TableRow>
              ) : (
                payouts.map((p) => (
                  <TableRow key={p.clientId} hover>
                    <TableCell><Typography variant="body2" fontWeight={600}>{p.fullName}</Typography></TableCell>
                    <TableCell><Typography variant="body2" color="text.secondary">{p.email}</Typography></TableCell>
                    <TableCell><Typography variant="body2" color="text.secondary">{p.mipymeReferralsTotal}</Typography></TableCell>
                    <TableCell><Typography variant="body2" color="text.secondary">{currency(p.mipymeBonusTotal)}</Typography></TableCell>
                    <TableCell><Typography variant="body2" fontWeight={600}>{currency(p.cupBalance)}</Typography></TableCell>
                    <TableCell><Typography variant="body2" color="text.secondary">{currency(p.cupPaidTotal)}</Typography></TableCell>
                    <TableCell align="right">
                      <Tooltip title="Marcar como pagado (resetea el saldo a 0)">
                        <IconButton
                          size="small"
                          color="primary"
                          disabled={settlePayoutMutation.isPending}
                          onClick={() => {
                            if (window.confirm(`¿Confirmas que ya pagaste ${currency(p.cupBalance)} a ${p.fullName}?`)) {
                              settlePayoutMutation.mutate(p.clientId);
                            }
                          }}
                        >
                          <Paid fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Typography variant="subtitle1" fontWeight={700} mb={1}>
        Competencia del mes {leaderboard ? `(${leaderboard.month}/${leaderboard.year})` : ''}
      </Typography>
      {loadingLeaderboard ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>#</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Nombre</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Referidos válidos</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {!leaderboard || leaderboard.top.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={3} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    Nadie tiene referidos válidos este mes todavía
                  </TableCell>
                </TableRow>
              ) : (
                leaderboard.top.map((entry) => (
                  <TableRow key={entry.rank} hover>
                    <TableCell>{entry.rank === 1 ? '🏆' : entry.rank}</TableCell>
                    <TableCell><Typography variant="body2" fontWeight={600}>{entry.name}</Typography></TableCell>
                    <TableCell>{entry.validReferralsThisMonth}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={receipt !== null} onClose={() => setReceipt(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Competencia cerrada</DialogTitle>
        <DialogContent>
          {receipt && (
            receipt.winnerClientId ? (
              <Box display="flex" flexDirection="column" gap={1}>
                <Typography>🏆 Ganador: <strong>{receipt.winnerName}</strong></Typography>
                <Typography>Premio: <strong>{currency(receipt.prizeAmount)}</strong></Typography>
                <Typography color="text.secondary">
                  Total de referidos válidos del mes: {receipt.totalValidReferrals}
                </Typography>
              </Box>
            ) : (
              <Typography color="text.secondary">
                Nadie tuvo referidos válidos ese mes: no había nada que premiar.
              </Typography>
            )
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setReceipt(null)}>Cerrar</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
