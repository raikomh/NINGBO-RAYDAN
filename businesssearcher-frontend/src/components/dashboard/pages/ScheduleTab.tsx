import { useState, useEffect } from 'react';
import {
  Box, Typography, List, ListItem, ListItemText, Switch,
  TextField, Button, CircularProgress, Alert,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/apiClient';
import type { AddScheduleDto, Schedule } from '@/lib/types';

const DAYS = [
  { key: 'Monday', label: 'Lunes' },
  { key: 'Tuesday', label: 'Martes' },
  { key: 'Wednesday', label: 'Miércoles' },
  { key: 'Thursday', label: 'Jueves' },
  { key: 'Friday', label: 'Viernes' },
  { key: 'Saturday', label: 'Sábado' },
  { key: 'Sunday', label: 'Domingo' },
];

interface DaySchedule {
  isClosed: boolean;
  openTime: string;
  closeTime: string;
}

interface Props {
  storeId: string;
}

export default function ScheduleTab({ storeId }: Props) {
  const qc = useQueryClient();

  const { data: apiSchedules, isLoading } = useQuery<Schedule[]>({
    queryKey: ['schedules', storeId],
    queryFn: async () => {
      const res = await api.get(`/api/v1/stores/${storeId}/schedules`);
      return res.data.data ?? res.data ?? [];
    },
    enabled: !!storeId,
  });

  const buildSchedules = (source?: Schedule[]) => {
    const initial: Record<string, DaySchedule> = {};
    DAYS.forEach((d) => {
      const existing = source?.find((s) => s.dayOfWeek === d.key);
      initial[d.key] = {
        isClosed: existing?.isClosed ?? false,
        openTime: existing?.openTime ?? '09:00',
        closeTime: existing?.closeTime ?? '18:00',
      };
    });
    return initial;
  };

  const [schedules, setSchedules] = useState<Record<string, DaySchedule>>(() => buildSchedules());

  useEffect(() => {
    if (apiSchedules) {
      setSchedules(buildSchedules(apiSchedules));
    }
  }, [apiSchedules]);

  const [success, setSuccess] = useState(false);

  const saveMutation = useMutation({
    mutationFn: async () => {
      const requests = DAYS.map((d) => {
        const dto: AddScheduleDto = {
          dayOfWeek: d.key,
          openTime: schedules[d.key].openTime,
          closeTime: schedules[d.key].closeTime,
          isClosed: schedules[d.key].isClosed,
        };
        return api.post(`/api/v1/stores/${storeId}/schedules`, dto);
      });
      await Promise.all(requests);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['schedules', storeId] });
      setSuccess(true);
      setTimeout(() => setSuccess(false), 3000);
    },
  });

  if (isLoading) {
    return <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>;
  }

  const update = (day: string, field: keyof DaySchedule, value: string | boolean) => {
    setSchedules((prev) => ({ ...prev, [day]: { ...prev[day], [field]: value } }));
  };

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h6" fontWeight={600}>Horarios de atención</Typography>
        <Button
          variant="contained"
          onClick={() => saveMutation.mutate()}
          disabled={saveMutation.isPending}
        >
          {saveMutation.isPending ? 'Guardando...' : 'Guardar horarios'}
        </Button>
      </Box>

      {success && <Alert severity="success" sx={{ mb: 2 }}>Horarios guardados correctamente</Alert>}
      {saveMutation.isError && <Alert severity="error" sx={{ mb: 2 }}>Error al guardar</Alert>}

      <List disablePadding>
        {DAYS.map((day) => {
          const s = schedules[day.key];
          return (
            <ListItem
              key={day.key}
              sx={{
                mb: 1,
                borderRadius: 2,
                border: '1px solid',
                borderColor: 'divider',
                bgcolor: 'background.paper',
                flexWrap: { xs: 'wrap', sm: 'nowrap' },
                gap: 2,
              }}
            >
              <Box display="flex" alignItems="center" gap={1} minWidth={130}>
                <Switch
                  checked={!s.isClosed}
                  onChange={(e) => update(day.key, 'isClosed', !e.target.checked)}
                  size="small"
                />
                <ListItemText
                  primary={day.label}
                  secondary={s.isClosed ? 'Cerrado' : 'Abierto'}
                  primaryTypographyProps={{ fontWeight: 600 }}
                />
              </Box>
              <Box display="flex" gap={2} flex={1}>
                <TextField
                  label="Apertura"
                  type="time"
                  value={s.openTime}
                  onChange={(e) => update(day.key, 'openTime', e.target.value)}
                  disabled={s.isClosed}
                  size="small"
                  InputLabelProps={{ shrink: true }}
                  sx={{ flex: 1 }}
                />
                <TextField
                  label="Cierre"
                  type="time"
                  value={s.closeTime}
                  onChange={(e) => update(day.key, 'closeTime', e.target.value)}
                  disabled={s.isClosed}
                  size="small"
                  InputLabelProps={{ shrink: true }}
                  sx={{ flex: 1 }}
                />
              </Box>
            </ListItem>
          );
        })}
      </List>
    </Box>
  );
}
