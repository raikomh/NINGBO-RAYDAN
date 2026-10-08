import { useMemo, useState } from 'react';
import { Box, Stack, Typography, TextField, Button, Grid, IconButton, Chip } from '@mui/material';
import { Calculate, Close } from '@mui/icons-material';
import type { OpsCurrency } from '@/lib/opsTypes';

const DENOMINATIONS: Record<OpsCurrency, number[]> = {
  CUP: [5000, 2000, 1000, 500, 200, 100, 50, 20, 10, 5, 3, 1],
  USD: [100, 50, 20, 10, 5, 1],
};

export default function CashDenominationBreakdown({ currency = 'CUP', onApply, defaultOpen = false, expected }: {
  currency?: OpsCurrency;
  onApply: (total: number) => void;
  defaultOpen?: boolean;
  /** Si se pasa, muestra en vivo cuánto falta/sobra contra el efectivo esperado del cierre. */
  expected?: number;
}) {
  const [open, setOpen] = useState(defaultOpen);
  const [counts, setCounts] = useState<Record<number, number | ''>>({});

  const denominations = DENOMINATIONS[currency];
  const total = useMemo(
    () => denominations.reduce((sum, d) => sum + d * (Number(counts[d]) || 0), 0),
    [denominations, counts],
  );

  const setCount = (d: number, raw: string) =>
    setCounts((prev) => ({ ...prev, [d]: raw === '' ? '' : Math.max(0, Math.floor(Number(raw))) }));

  const hasCounted = Object.values(counts).some((v) => v !== '' && v > 0);
  const diff = hasCounted && expected != null ? Math.round((total - expected) * 100) / 100 : null;

  if (!open) {
    return (
      <Button size="small" startIcon={<Calculate fontSize="small" />} onClick={() => setOpen(true)} sx={{ alignSelf: 'flex-start' }}>
        Desglosar por billetes
      </Button>
    );
  }

  return (
    <Box sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 1, p: 1.5 }}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1}>
        <Typography variant="caption" fontWeight={700} color="text.secondary">DESGLOSE DE BILLETES ({currency})</Typography>
        <IconButton size="small" onClick={() => setOpen(false)}><Close fontSize="small" /></IconButton>
      </Stack>
      <Grid container spacing={1}>
        {denominations.map((d) => (
          <Grid item xs={4} sm={3} key={d}>
            <TextField
              size="small" type="number" label={d.toString()} fullWidth
              value={counts[d] ?? ''}
              onChange={(e) => setCount(d, e.target.value)}
              inputProps={{ min: 0, style: { textAlign: 'right' } }}
              helperText={counts[d] ? `= ${(d * Number(counts[d])).toFixed(2)}` : ' '}
            />
          </Grid>
        ))}
      </Grid>
      <Stack direction="row" justifyContent="space-between" alignItems="center" mt={1} flexWrap="wrap" gap={1}>
        <Typography variant="body2">Total contado: <strong>{total.toFixed(2)} {currency}</strong></Typography>
        {diff !== null && (
          <Chip
            size="small"
            color={diff === 0 ? 'success' : 'error'}
            label={diff === 0 ? 'Cuadra con el cierre' : `${diff > 0 ? 'Sobran' : 'Faltan'} ${Math.abs(diff).toFixed(2)} ${currency}`}
          />
        )}
        <Button size="small" variant="contained" onClick={() => onApply(total)}>Usar este total</Button>
      </Stack>
    </Box>
  );
}
