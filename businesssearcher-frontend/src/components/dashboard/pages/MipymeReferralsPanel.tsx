import { useState } from 'react';
import {
  Box, Typography, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Paper, CircularProgress, Button,
  Alert, IconButton, Tooltip, Dialog, DialogTitle, DialogContent, DialogActions,
} from '@mui/material';
import { Refresh, EmojiEvents } from '@mui/icons-material';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/apiClient';
import type { MipymeReferralLeaderboard } from '@/lib/types';

function useMipymeReferralLeaderboard() {
  return useQuery<MipymeReferralLeaderboard>({
    queryKey: ['admin', 'mipyme-referrals', 'leaderboard'],
    queryFn: async () => {
      const res = await api.get('/api/v1/admin/mipyme-referrals/leaderboard');
      return res.data?.data ?? res.data;
    },
  });
}

export default function MipymeReferralsPanel() {
  const qc = useQueryClient();
  const [closedMonth, setClosedMonth] = useState<{ year: number; month: number } | null>(null);

  const { data: leaderboard, isLoading: loadingLeaderboard, isError: leaderboardError } = useMipymeReferralLeaderboard();

  const closeMonthMutation = useMutation({
    mutationFn: () => api.post('/api/v1/admin/mipyme-referrals/close-month'),
    onSuccess: () => {
      if (leaderboard) setClosedMonth({ year: leaderboard.year, month: leaderboard.month });
      qc.invalidateQueries({ queryKey: ['admin', 'mipyme-referrals'] });
      // El premio se paga en el saldo unificado de CUP, visible en la tabla de saldos de "Referidos".
      qc.invalidateQueries({ queryKey: ['admin', 'referrals', 'payouts'] });
    },
  });

  const refreshAll = () => qc.invalidateQueries({ queryKey: ['admin', 'mipyme-referrals'] });

  return (
    <Box>
      <Box display="flex" gap={2} mb={3} flexWrap="wrap" alignItems="center" justifyContent="space-between">
        <Typography variant="body2" color="text.secondary">
          Competencia mensual de MiPymes referidas por clientes (el 1.º lugar gana un premio que se acredita
          al saldo unificado de CUP del cliente, visible en la pestaña "Referidos").
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
              if (window.confirm('¿Cerrar la competencia de MiPymes referidas del mes actual y premiar al 1.º lugar? Esta acción no se puede repetir para el mismo mes.')) {
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

      {leaderboardError && <Alert severity="error" sx={{ mb: 2 }}>Error al cargar la competencia. Verifica que tienes permisos de admin.</Alert>}

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
                <TableCell sx={{ fontWeight: 700 }}>MiPymes referidas</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {!leaderboard || leaderboard.top.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={3} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    Nadie tiene MiPymes referidas este mes todavía
                  </TableCell>
                </TableRow>
              ) : (
                leaderboard.top.map((entry) => (
                  <TableRow key={entry.rank} hover>
                    <TableCell>{entry.rank === 1 ? '🏆' : entry.rank}</TableCell>
                    <TableCell><Typography variant="body2" fontWeight={600}>{entry.name}</Typography></TableCell>
                    <TableCell>{entry.mipymeReferralsThisMonth}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={closedMonth !== null} onClose={() => setClosedMonth(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Competencia cerrada</DialogTitle>
        <DialogContent>
          <Typography color="text.secondary">
            Se cerró la competencia de MiPymes referidas de {closedMonth?.month}/{closedMonth?.year}.
            Si hubo un ganador, el premio ya se acreditó a su saldo unificado de CUP —
            revisa la pestaña "Referidos" para verlo.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setClosedMonth(null)}>Cerrar</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
