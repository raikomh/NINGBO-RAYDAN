import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Dialog, DialogTitle, DialogContent, DialogActions,
  Button, TextField, Grid, Typography, Divider, Alert, Box, CircularProgress,
} from '@mui/material';
import { MyLocation as MyLocationIcon } from '@mui/icons-material';
import { MapContainer, TileLayer, Marker, useMap, useMapEvents } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useCreateStore, useUpdateStore } from '@/hooks/useStores';
import { useAuth } from '@/context/AuthContext';
import { geocodeAddress } from '@/lib/geocode';
import type { Store } from '@/lib/types';

// Fix default marker icons broken by bundlers
delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

const storeIcon = new L.Icon({
  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-blue.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
});

// Default view when the store has no coordinates yet (central Havana).
const DEFAULT_CENTER: [number, number] = [23.1136, -82.3666];

interface LatLng { lat: number; lng: number }

// Places / moves the pin on click and supports dragging it to fine-tune.
function LocationMarker({ position, onChange }: { position: LatLng | null; onChange: (p: LatLng) => void }) {
  useMapEvents({
    click(e) { onChange({ lat: e.latlng.lat, lng: e.latlng.lng }); },
  });
  if (!position) return null;
  return (
    <Marker
      position={[position.lat, position.lng]}
      icon={storeIcon}
      draggable
      eventHandlers={{
        dragend(e) {
          const p = (e.target as L.Marker).getLatLng();
          onChange({ lat: p.lat, lng: p.lng });
        },
      }}
    />
  );
}

// Recenters the map when a geocode lands (via `trigger`) and fixes tile sizing
// after the dialog's open transition (map mounts while the container is still 0px).
function MapController({ center, trigger }: { center: [number, number]; trigger: number }) {
  const map = useMap();
  useEffect(() => {
    const t = setTimeout(() => map.invalidateSize(), 200);
    return () => clearTimeout(t);
  }, [map]);
  useEffect(() => {
    map.setView(center, 16);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trigger]);
  return null;
}

// El backend exige dirección completa y teléfono (Address es un value object requerido)
const schema = z.object({
  name: z.string().min(2, 'Mínimo 2 caracteres'),
  description: z.string().optional(),
  phone: z.string().min(5, 'El teléfono es requerido'),
  street: z.string().min(2, 'La calle es requerida'),
  city: z.string().min(2, 'La ciudad es requerida'),
  state: z.string().min(2, 'El estado es requerido'),
  country: z.string().min(2, 'El país es requerido'),
});
type FormData = z.infer<typeof schema>;

interface Props {
  open: boolean;
  onClose: () => void;
  store?: Store;
}

export default function StoreDialog({ open, onClose, store }: Props) {
  const isEditing = !!store;
  const { user } = useAuth();
  const createMutation = useCreateStore();
  const updateMutation = useUpdateStore(store?.id ?? '');
  const mutation = isEditing ? updateMutation : createMutation;
  const serverError = (mutation.error as { response?: { data?: { message?: string } } } | null)
    ?.response?.data?.message;

  // Pin coordinates: kept outside react-hook-form since they come from the map, not a text field.
  const [position, setPosition] = useState<LatLng | null>(null);
  const [geocoding, setGeocoding] = useState(false);
  const [geoMsg, setGeoMsg] = useState<{ severity: 'info' | 'warning'; text: string } | null>(null);
  // Bumped whenever we want the map to recenter (open + successful geocode).
  const [recenter, setRecenter] = useState(0);

  const { register, handleSubmit, reset, getValues, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: '', description: '', phone: '',
      street: '', city: '', state: '', country: '',
    },
  });

  useEffect(() => {
    if (open) {
      reset({
        name: store?.name ?? user?.businessName ?? '',
        description: store?.description ?? '',
        phone: store?.phone ?? '',
        street: store?.address?.street ?? '',
        city: store?.address?.city ?? '',
        state: store?.address?.state ?? '',
        country: store?.address?.country ?? '',
      });
      const lat = store?.address?.latitude;
      const lng = store?.address?.longitude;
      setPosition(lat != null && lng != null ? { lat, lng } : null);
      setGeoMsg(null);
      setRecenter((n) => n + 1);
    }
  }, [open, store, user, reset]);

  const handleClose = () => {
    reset();
    setPosition(null);
    setGeoMsg(null);
    onClose();
  };

  // Geocode the currently typed address and drop a pin the user can then fine-tune.
  const handleLocate = async () => {
    const v = getValues();
    setGeocoding(true);
    setGeoMsg(null);
    try {
      const res = await geocodeAddress({
        street: v.street, city: v.city, state: v.state, country: v.country,
      });
      if (res) {
        setPosition({ lat: res.lat, lng: res.lng });
        setRecenter((n) => n + 1);
        setGeoMsg({
          severity: 'info',
          text: `Ubicación aproximada encontrada. Arrastra el pin (o haz clic en el mapa) para ajustarlo con precisión.`,
        });
      } else {
        setGeoMsg({
          severity: 'warning',
          text: 'No se encontró la dirección automáticamente. Haz clic en el mapa para ubicar tu tienda manualmente.',
        });
      }
    } finally {
      setGeocoding(false);
    }
  };

  const onSubmit = async (data: FormData) => {
    const dto = {
      name: data.name,
      description: data.description || null,
      phone: data.phone,
      logoUrl: store?.logoUrl ?? null,
      address: {
        street: data.street,
        city: data.city,
        state: data.state,
        country: data.country,
        // `position` is seeded from the stored coords on open, so it is the single
        // source of truth: a pin means explicit coords, no pin means "geocode it".
        latitude: position?.lat ?? null,
        longitude: position?.lng ?? null,
      },
    };
    try {
      if (isEditing) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        await updateMutation.mutateAsync(dto as any);
      } else {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        await createMutation.mutateAsync(dto as any);
      }
      handleClose();
    } catch {
      // error shown via serverError
    }
  };

  const center: [number, number] = position ? [position.lat, position.lng] : DEFAULT_CENTER;

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <DialogTitle sx={{ fontWeight: 700 }}>
        {isEditing ? 'Editar tienda' : 'Configurar tienda'}
      </DialogTitle>
      <DialogContent dividers>
        {serverError && <Alert severity="error" sx={{ mb: 2 }}>{serverError}</Alert>}
        <Grid container spacing={2} component="form" id="store-form" onSubmit={handleSubmit(onSubmit)}>
          <Grid item xs={12}>
            <Typography variant="subtitle2" fontWeight={600} color="text.secondary" mb={1}>
              INFORMACIÓN GENERAL
            </Typography>
          </Grid>
          <Grid item xs={12}>
            <TextField
              label="Nombre de la tienda *"
              fullWidth
              {...register('name')}
              error={!!errors.name}
              helperText={errors.name?.message}
            />
          </Grid>
          <Grid item xs={12}>
            <TextField
              label="Descripción"
              fullWidth
              multiline
              rows={2}
              {...register('description')}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <TextField
              label="Teléfono *"
              fullWidth
              {...register('phone')}
              error={!!errors.phone}
              helperText={errors.phone?.message}
            />
          </Grid>

          <Grid item xs={12}>
            <Divider />
            <Typography variant="subtitle2" fontWeight={600} color="text.secondary" mt={2} mb={1}>
              DIRECCIÓN
            </Typography>
          </Grid>
          <Grid item xs={12}>
            <TextField
              label="Calle / Dirección *"
              fullWidth
              {...register('street')}
              error={!!errors.street}
              helperText={errors.street?.message}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <TextField
              label="Ciudad *"
              fullWidth
              {...register('city')}
              error={!!errors.city}
              helperText={errors.city?.message}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <TextField
              label="Estado *"
              fullWidth
              {...register('state')}
              error={!!errors.state}
              helperText={errors.state?.message}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <TextField
              label="País *"
              fullWidth
              {...register('country')}
              error={!!errors.country}
              helperText={errors.country?.message}
            />
          </Grid>

          <Grid item xs={12}>
            <Divider />
            <Box display="flex" alignItems="center" justifyContent="space-between" mt={2} mb={1} gap={1} flexWrap="wrap">
              <Typography variant="subtitle2" fontWeight={600} color="text.secondary">
                UBICACIÓN EN EL MAPA
              </Typography>
              <Box display="flex" gap={1} flexWrap="wrap">
                {position && (
                  <Button
                    size="small"
                    color="inherit"
                    onClick={() => { setPosition(null); setGeoMsg(null); }}
                  >
                    Usar ubicación automática
                  </Button>
                )}
                <Button
                  size="small"
                  variant="outlined"
                  startIcon={geocoding ? <CircularProgress size={14} /> : <MyLocationIcon />}
                  onClick={handleLocate}
                  disabled={geocoding}
                >
                  {geocoding ? 'Buscando…' : 'Ubicar desde la dirección'}
                </Button>
              </Box>
            </Box>
            <Typography variant="caption" color="text.secondary" display="block" mb={1}>
              Para direcciones informales (ej. "entre Garrido y Carolina"), la búsqueda automática es
              aproximada. Haz clic en el mapa o arrastra el pin para marcar la ubicación exacta de tu tienda.
            </Typography>
            {geoMsg && <Alert severity={geoMsg.severity} sx={{ mb: 1 }}>{geoMsg.text}</Alert>}
            <Box sx={{ height: { xs: 250, sm: 300 }, width: '100%', borderRadius: 2, overflow: 'hidden', border: '1px solid', borderColor: 'divider' }}>
              <MapContainer center={center} zoom={position ? 16 : 12} style={{ height: '100%', width: '100%' }}>
                <TileLayer
                  attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                  url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                />
                <MapController center={center} trigger={recenter} />
                <LocationMarker position={position} onChange={(p) => setPosition(p)} />
              </MapContainer>
            </Box>
            {position && (
              <Typography variant="caption" color="text.secondary" display="block" mt={0.5}>
                Coordenadas: {position.lat.toFixed(6)}, {position.lng.toFixed(6)}
              </Typography>
            )}
          </Grid>
        </Grid>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose}>Cancelar</Button>
        <Button
          type="submit"
          form="store-form"
          variant="contained"
          disabled={isSubmitting || createMutation.isPending || updateMutation.isPending}
        >
          {isSubmitting || createMutation.isPending || updateMutation.isPending
            ? 'Guardando...'
            : isEditing ? 'Guardar cambios' : 'Configurar tienda'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
