import { useMemo, useState } from 'react';
import {
  Box, Typography, TextField, Button, Grid, Card, CardContent,
  Chip, CircularProgress, InputAdornment, Divider, Avatar,
} from '@mui/material';
import {
  Search as SearchIcon, LocalShipping, Clear, LocationOn, Phone,
} from '@mui/icons-material';
import { useWholesaleCatalog } from '@/hooks/useWholesaleCatalog';
import type { WholesaleCatalogItem } from '@/lib/types';

interface StoreGroup {
  storeId: string;
  storeName: string;
  city?: string;
  storeAddress?: string;
  storePhone?: string;
  items: WholesaleCatalogItem[];
}

export default function WholesaleCatalogPage() {
  const [query, setQuery] = useState('');
  const [city, setCity] = useState('');
  const [submitted, setSubmitted] = useState(false);

  const { data, isLoading, isError } = useWholesaleCatalog(
    { q: query || undefined, city: city || undefined },
    submitted,
  );

  const groups = useMemo<StoreGroup[]>(() => {
    if (!data?.items) return [];
    const byStore = new Map<string, StoreGroup>();
    for (const item of data.items) {
      let group = byStore.get(item.storeId);
      if (!group) {
        group = {
          storeId: item.storeId,
          storeName: item.storeName,
          city: item.city,
          storeAddress: item.storeAddress,
          storePhone: item.storePhone,
          items: [],
        };
        byStore.set(item.storeId, group);
      }
      group.items.push(item);
    }
    return Array.from(byStore.values());
  }, [data]);

  const handleSearch = () => setSubmitted(true);
  const handleClear = () => {
    setQuery('');
    setCity('');
    setSubmitted(false);
  };

  return (
    <Box>
      <Typography variant="h5" fontWeight={700} mb={0.5}>Catálogo Mayorista</Typography>
      <Typography variant="body2" color="text.secondary" mb={3}>
        Explora el directorio de proveedores mayoristas y sus productos disponibles
      </Typography>

      <Card variant="outlined" sx={{ borderRadius: 3, p: 2, mb: 3 }}>
        <Box display="flex" gap={1.5} flexWrap="wrap" alignItems="center">
          <TextField
            size="small"
            placeholder="Buscar productos o proveedores..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
            sx={{ flex: 1, minWidth: 200 }}
            InputProps={{
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" color="action" />
                </InputAdornment>
              ),
            }}
          />
          <TextField
            size="small"
            placeholder="Ciudad"
            value={city}
            onChange={(e) => setCity(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
            sx={{ width: 180 }}
            InputProps={{
              startAdornment: (
                <InputAdornment position="start">
                  <LocationOn fontSize="small" color="action" />
                </InputAdornment>
              ),
            }}
          />
          <Button variant="contained" onClick={handleSearch} startIcon={<SearchIcon />}>
            Buscar
          </Button>
          {(query || city || submitted) && (
            <Button startIcon={<Clear />} onClick={handleClear} color="inherit">
              Limpiar
            </Button>
          )}
        </Box>
      </Card>

      {isLoading && (
        <Box display="flex" justifyContent="center" py={8}>
          <CircularProgress />
        </Box>
      )}

      {isError && (
        <Typography color="error" textAlign="center" py={4}>
          Error al cargar el catálogo mayorista. Verifica que tu cuenta sea de tipo Minorista.
        </Typography>
      )}

      {submitted && !isLoading && !isError && groups.length === 0 && (
        <Box textAlign="center" py={8}>
          <LocalShipping sx={{ fontSize: 56, color: 'text.disabled', mb: 2 }} />
          <Typography variant="h6" color="text.secondary">Sin resultados</Typography>
          <Typography variant="body2" color="text.disabled">
            Prueba con otros términos de búsqueda o cambia la ciudad.
          </Typography>
        </Box>
      )}

      {!submitted && !isLoading && (
        <Box textAlign="center" py={8}>
          <LocalShipping sx={{ fontSize: 64, color: 'text.disabled', mb: 2 }} />
          <Typography variant="h6" color="text.secondary">
            Ingresa un término para buscar
          </Typography>
          <Typography variant="body2" color="text.disabled">
            Busca proveedores mayoristas por producto o ciudad.
          </Typography>
        </Box>
      )}

      {groups.length > 0 && (
        <Grid container spacing={2.5}>
          {groups.map((group) => (
            <Grid item xs={12} md={6} key={group.storeId}>
              <Card
                sx={{
                  borderRadius: 2.5,
                  height: '100%',
                  border: '1px solid rgba(0,0,0,0.08)',
                  borderLeft: '4px solid #D97706',
                }}
              >
                <CardContent>
                  <Box display="flex" alignItems="center" gap={2} mb={2}>
                    <Avatar sx={{ bgcolor: 'warning.main', width: 44, height: 44 }}>
                      <LocalShipping />
                    </Avatar>
                    <Box flex={1} minWidth={0}>
                      <Typography variant="subtitle1" fontWeight={700} noWrap>
                        {group.storeName}
                      </Typography>
                      {group.city && (
                        <Typography variant="caption" color="text.secondary" display="flex" alignItems="center" gap={0.5}>
                          <LocationOn sx={{ fontSize: 12 }} />
                          {group.storeAddress ? `${group.storeAddress}, ` : ''}{group.city}
                        </Typography>
                      )}
                      {group.storePhone && (
                        <Typography variant="caption" color="text.secondary" display="flex" alignItems="center" gap={0.5}>
                          <Phone sx={{ fontSize: 12 }} />
                          {group.storePhone}
                        </Typography>
                      )}
                    </Box>
                  </Box>

                  <Divider sx={{ mb: 1.5 }} />
                  <Typography variant="caption" color="text.secondary" fontWeight={600} display="block" mb={1}>
                    PRODUCTOS ({group.items.length})
                  </Typography>
                  <Box display="flex" flexDirection="column" gap={1}>
                    {group.items.map((item) => (
                      <Box
                        key={item.productId}
                        display="flex"
                        alignItems="center"
                        justifyContent="space-between"
                        sx={{ px: 1.5, py: 1, borderRadius: 1.5, bgcolor: 'action.hover' }}
                      >
                        <Box minWidth={0} flex={1}>
                          <Typography variant="body2" fontWeight={600} noWrap>
                            {item.productName}
                          </Typography>
                          <Box display="flex" gap={1} alignItems="center">
                            {item.categoryName && (
                              <Typography variant="caption" color="text.secondary">
                                {item.categoryName}
                              </Typography>
                            )}
                            <Typography variant="caption" color="text.secondary">
                              Stock: {item.stock}
                            </Typography>
                          </Box>
                        </Box>
                        <Box display="flex" flexDirection="column" alignItems="flex-end" gap={0.5} flexShrink={0}>
                          <Typography variant="body2" fontWeight={700} color="warning.dark">
                            {item.currency} {item.price.toFixed(2)}
                          </Typography>
                          {item.minOrderQuantity > 1 && (
                            <Chip
                              label={`Mín. ${item.minOrderQuantity}`}
                              size="small"
                              variant="outlined"
                              sx={{ fontSize: '0.65rem', height: 18 }}
                            />
                          )}
                        </Box>
                      </Box>
                    ))}
                  </Box>
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}
    </Box>
  );
}
