import { useEffect, useState, useCallback } from 'react';
import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { Box, Typography, Alert, CircularProgress, Button } from '@mui/material';
import { Store as StoreIcon, Refresh as RefreshIcon } from '@mui/icons-material';
import type { Store } from '@/lib/types';
import { geocodeAddress } from '@/lib/geocode';

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

function FitBounds({ point }: { point: [number, number] }) {
  const map = useMap();
  useEffect(() => {
    map.setView(point, 15);
  }, [map, point]);
  return null;
}

interface StoreMapTabProps {
  store: Store;
}

export default function StoreMapTab({ store }: StoreMapTabProps) {
  const [geocodedPos, setGeocodedPos] = useState<{ lat: number; lng: number; query: string } | null>(null);
  const [geocoding, setGeocoding]     = useState(false);
  const [geocodeError, setGeocodeError] = useState(false);

  const hasExplicitCoords = store.address?.latitude != null && store.address?.longitude != null;
  const storeLat = hasExplicitCoords ? store.address!.latitude! : geocodedPos?.lat;
  const storeLng = hasExplicitCoords ? store.address!.longitude! : geocodedPos?.lng;
  const hasCoords = storeLat != null && storeLng != null;

  // Build a human-readable address string for display
  const addressParts = [store.address?.street, store.address?.city, store.address?.state, store.address?.country]
    .filter(Boolean);
  const addressStr = addressParts.join(', ');

  const runGeocode = useCallback(() => {
    if (hasExplicitCoords) return;
    setGeocoding(true);
    setGeocodeError(false);
    geocodeAddress(store.address).then((pos) => {
      setGeocodedPos(pos);
      setGeocodeError(pos === null && addressParts.length > 0);
      setGeocoding(false);
    });
  }, [store.address, hasExplicitCoords, addressParts.length]);

  useEffect(() => { runGeocode(); }, [runGeocode]);

  const center: [number, number] = hasCoords ? [storeLat!, storeLng!] : [4.711, -74.0721]; // Colombia default

  return (
    <Box>
      <Box display="flex" alignItems="center" gap={1} mb={2} flexWrap="wrap">
        <StoreIcon sx={{ fontSize: 18, color: 'primary.main' }} />
        <Typography variant="body2" fontWeight={600}>{store.name}</Typography>
        {addressStr && (
          <Typography variant="body2" color="text.secondary">— {addressStr}</Typography>
        )}
      </Box>

      {hasCoords && (
        <Typography variant="caption" color="text.secondary" display="block" mb={1}>
          📍 {storeLat!.toFixed(6)}, {storeLng!.toFixed(6)} ·{' '}
          {hasExplicitCoords
            ? 'ubicación fijada manualmente'
            : 'ubicación calculada automáticamente'}
        </Typography>
      )}

      {geocoding && (
        <Box display="flex" alignItems="center" gap={1} mb={2}>
          <CircularProgress size={16} />
          <Typography variant="body2" color="text.secondary">
            Buscando coordenadas para: <em>{addressStr || 'dirección…'}</em>
          </Typography>
        </Box>
      )}

      {!geocoding && addressParts.length === 0 && !hasCoords && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Agrega una dirección a tu tienda (calle, ciudad, país) para ver la ubicación en el mapa.
        </Alert>
      )}

      {!geocoding && geocodeError && (
        <Alert
          severity="warning"
          sx={{ mb: 2 }}
          action={
            <Button size="small" startIcon={<RefreshIcon />} onClick={runGeocode} color="inherit">
              Reintentar
            </Button>
          }
        >
          No se encontró la dirección <strong>"{geocodedPos?.query ?? addressStr}"</strong> en el mapa.
          Verifica que la dirección sea correcta o más específica (ej: agrega ciudad y país).
        </Alert>
      )}

      {!geocoding && geocodedPos && (
        <Alert severity="success" sx={{ mb: 2 }}>
          Ubicación encontrada para: <strong>{geocodedPos.query}</strong>
        </Alert>
      )}

      <Box sx={{ height: { xs: 250, sm: 350, md: 450 }, width: '100%', borderRadius: 2, overflow: 'hidden', border: '1px solid', borderColor: 'divider' }}>
        <MapContainer center={center} zoom={13} style={{ height: '100%', width: '100%' }}>
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />

          {hasCoords && <FitBounds point={[storeLat!, storeLng!]} />}

          {hasCoords && (
            <Marker position={[storeLat!, storeLng!]} icon={storeIcon}>
              <Popup>
                <strong>{store.name}</strong>
                {store.address?.street && <><br />{store.address.street}</>}
                {store.address?.city && <><br />{store.address.city}</>}
              </Popup>
            </Marker>
          )}
        </MapContainer>
      </Box>
    </Box>
  );
}
