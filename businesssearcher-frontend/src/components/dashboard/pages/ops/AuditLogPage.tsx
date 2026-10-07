import { useState } from 'react';
import {
  Box, Card, Typography, Chip, CircularProgress, TextField,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Tooltip,
} from '@mui/material';
import { Security } from '@mui/icons-material';
import { useAuditLogs } from '@/hooks/useOps';

const STATUS_COLOR: Record<string, 'success' | 'error' | 'warning' | 'default'> = {
  SUCCESS: 'success', DENIED: 'error', ERROR: 'warning',
};

export default function AuditLogPage() {
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [action, setAction] = useState('');
  const { data: logs, isLoading, isError } = useAuditLogs({
    from: from || undefined, to: to || undefined, action: action || undefined, limit: 200,
  });

  return (
    <Box>
      <Box display="flex" alignItems="center" gap={1} mb={3}>
        <Security color="primary" />
        <Typography variant="h5" fontWeight={700}>Auditoría de seguridad</Typography>
      </Box>

      <Card variant="outlined" sx={{ mb: 2 }}>
        <Box sx={{ p: 2, display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
          <TextField size="small" type="date" label="Desde" InputLabelProps={{ shrink: true }} value={from} onChange={(e) => setFrom(e.target.value)} />
          <TextField size="small" type="date" label="Hasta" InputLabelProps={{ shrink: true }} value={to} onChange={(e) => setTo(e.target.value)} />
          <TextField size="small" label="Filtrar por entidad/acción" value={action} onChange={(e) => setAction(e.target.value)} sx={{ minWidth: 220 }} />
        </Box>
      </Card>

      {isError && (
        <Typography color="error" mb={2}>
          Solo el rol Auditor (o el Administrador) puede ver la auditoría.
        </Typography>
      )}

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Fecha</TableCell><TableCell>Usuario</TableCell><TableCell>Rol</TableCell>
                <TableCell>Acción</TableCell><TableCell>Entidad</TableCell><TableCell>Estado</TableCell>
                <TableCell>IP</TableCell><TableCell>Ruta</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(logs ?? []).map((l) => (
                <TableRow key={l.id} hover>
                  <TableCell>{new Date(l.timestamp).toLocaleString()}</TableCell>
                  <TableCell>{l.userName ?? '—'}</TableCell>
                  <TableCell>{l.userRole ?? '—'}</TableCell>
                  <TableCell>{l.action}</TableCell>
                  <TableCell>{l.targetEntity ?? '—'}</TableCell>
                  <TableCell><Chip size="small" color={STATUS_COLOR[l.status] ?? 'default'} label={l.status} /></TableCell>
                  <TableCell>{l.ipAddress ?? '—'}</TableCell>
                  <TableCell>
                    <Tooltip title={l.path ?? ''}>
                      <Typography variant="caption" sx={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', display: 'block', whiteSpace: 'nowrap' }}>
                        {l.method} {l.path}
                      </Typography>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
              {(logs ?? []).length === 0 && (
                <TableRow><TableCell colSpan={8} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin registros de auditoría</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
