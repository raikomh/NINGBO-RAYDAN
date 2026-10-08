import { useState } from 'react';
import {
  Box, Typography, TextField, Button, Grid, Card, CardContent,
  Chip, CircularProgress, InputAdornment, Collapse, Slider,
  Divider, Avatar,
} from '@mui/material';
import {
  Search as SearchIcon, Store as StoreIcon, FilterList, Clear,
  LocationOn, Phone,
} from '@mui/icons-material';
import { useSearch, type SearchParams } from '@/hooks/useSearch';
import type { SearchProduct } from '@/lib/types';

export default function SearchPage() {
  const [query, setQuery] = useState('');
  const [city, setCity] = useState('');
  const [available, setAvailable] = useState<boolean | undefined>(undefined);
  const [priceRange, setPriceRange] = useState<[number, number]>([0, 1000]);
  const [showFilters, setShowFilters] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  const params: SearchParams = {
    q: query || undefined,
    city: city || undefined,
    available: available,
    minPrice: priceRange[0] > 0 ? priceRange[0] : undefined,
    maxPrice: priceRange[1] < 1000 ? priceRange[1] : undefined,
  };

  const { data: results, isLoading, isError } = useSearch(params, submitted);

  const handleSearch = () => {
    setSubmitted(true);
  };

  const handleClear = () => {
    setQuery('');
    setCity('');
    setAvailable(undefined);
    setPriceRange([0, 1000]);
    setSubmitted(false);
  };

  return (
    <Box>
      <Typography variant="h5" fontWeight={700} mb={0.5}>Búsqueda de tiendas</Typography>
      <Typography variant="body2" color="text.secondary" mb={3}>
        Encuentra tiendas y productos disponibles en tu área
      </Typography>

      {/* Search bar */}
      <Card variant="outlined" sx={{ borderRadius: 3, p: 2, mb: 3 }}>
        <Box display="flex" gap={1.5} flexWrap="wrap" alignItems="center">
          <TextField
            size="small"
            placeholder="Buscar productos o tiendas..."
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
            sx={{ width: { xs: '100%', sm: 180 } }}
            InputProps={{
              startAdornment: (
                <InputAdornment position="start">
                  <LocationOn fontSize="small" color="action" />
                </InputAdornment>
              ),
            }}
          />
          <Button
            variant="contained"
            onClick={handleSearch}
            disabled={!query && !city}
            startIcon={<SearchIcon />}
          >
            Buscar
          </Button>
          <Button
            variant="outlined"
            startIcon={<FilterList />}
            onClick={() => setShowFilters((v) => !v)}
          >
            Filtros
          </Button>
          {(query || city || submitted) && (
            <Button startIcon={<Clear />} onClick={handleClear} color="inherit">
              Limpiar
            </Button>
          )}
        </Box>

        <Collapse in={showFilters}>
          <Divider sx={{ my: 2 }} />
          <Grid container spacing={2} alignItems="center">
            <Grid item xs={12} sm={4}>
              <Typography variant="caption" color="text.secondary" fontWeight={600} gutterBottom>
                DISPONIBILIDAD
              </Typography>
              <Box display="flex" gap={1} mt={0.5} flexWrap="wrap">
                {[
                  { label: 'Todos', value: undefined },
                  { label: 'Disponibles', value: true },
                  { label: 'No disponibles', value: false },
                ].map((opt) => (
                  <Chip
                    key={String(opt.value)}
                    label={opt.label}
                    size="small"
                    variant={available === opt.value ? 'filled' : 'outlined'}
                    color={available === opt.value ? 'primary' : 'default'}
                    onClick={() => setAvailable(opt.value)}
                    sx={{ cursor: 'pointer' }}
                  />
                ))}
              </Box>
            </Grid>
            <Grid item xs={12} sm={8}>
              <Typography variant="caption" color="text.secondary" fontWeight={600} gutterBottom>
                RANGO DE PRECIO (CUP): {priceRange[0]} — {priceRange[1] >= 1000 ? '1000+' : priceRange[1]}
              </Typography>
              <Slider
                value={priceRange}
                onChange={(_, v) => setPriceRange(v as [number, number])}
                min={0}
                max={1000}
                step={10}
                valueLabelDisplay="auto"
                sx={{ mt: 1 }}
              />
            </Grid>
          </Grid>
        </Collapse>
      </Card>

      {/* Results */}
      {isLoading && (
        <Box display="flex" justifyContent="center" py={8}>
          <CircularProgress />
        </Box>
      )}

      {isError && (
        <Typography color="error" textAlign="center" py={4}>
          Error al buscar. Intenta de nuevo.
        </Typography>
      )}

      {submitted && !isLoading && !isError && results?.length === 0 && (
        <Box textAlign="center" py={8}>
          <SearchIcon sx={{ fontSize: 56, color: 'text.disabled', mb: 2 }} />
          <Typography variant="h6" color="text.secondary">Sin resultados</Typography>
          <Typography variant="body2" color="text.disabled">
            Prueba con otros términos de búsqueda o cambia los filtros.
          </Typography>
        </Box>
      )}

      {!submitted && !isLoading && (
        <Box textAlign="center" py={8}>
          <SearchIcon sx={{ fontSize: 64, color: 'text.disabled', mb: 2 }} />
          <Typography variant="h6" color="text.secondary">
            Ingresa un término para buscar
          </Typography>
          <Typography variant="body2" color="text.disabled">
            Busca por nombre de producto, tienda o ciudad.
          </Typography>
        </Box>
      )}

      {results && results.length > 0 && (
        <Grid container spacing={2.5}>
          {results.map((result) => (
            <Grid item xs={12} md={6} key={result.storeId}>
              <Card
                sx={{
                  borderRadius: 2.5,
                  height: '100%',
                  border: '1px solid rgba(0,0,0,0.08)',
                  borderLeft: '4px solid #2563EB',
                  transition: `transform 0.30s cubic-bezier(0.34,1.56,0.64,1), box-shadow 0.25s ease`,
                  '&:hover': {
                    transform: 'translateY(-10px)',
                    boxShadow: '0 24px 48px rgba(37,99,235,0.30), 0 6px 12px rgba(0,0,0,0.08)',
                  },
                }}
              >
                <CardContent>
                  {/* Store header */}
                  <Box display="flex" alignItems="center" gap={2} mb={2}>
                    <Avatar sx={{ bgcolor: 'primary.main', width: 44, height: 44 }}>
                      <StoreIcon />
                    </Avatar>
                    <Box flex={1} minWidth={0}>
                      <Box display="flex" alignItems="center" gap={1}>
                        <Typography variant="subtitle1" fontWeight={700} noWrap>
                          {result.storeName}
                        </Typography>
                        {result.isOpen !== undefined && (
                          <Chip
                            label={result.isOpen ? 'Abierta' : 'Cerrada'}
                            size="small"
                            color={result.isOpen ? 'success' : 'default'}
                          />
                        )}
                      </Box>
                      {result.storeAddress?.city && (
                        <Typography variant="caption" color="text.secondary" display="flex" alignItems="center" gap={0.5}>
                          <LocationOn sx={{ fontSize: 12 }} />
                          {result.storeAddress.city}
                          {result.storeAddress.country ? `, ${result.storeAddress.country}` : ''}
                        </Typography>
                      )}
                      {result.phone && (
                        <Typography variant="caption" color="text.secondary" display="flex" alignItems="center" gap={0.5}>
                          <Phone sx={{ fontSize: 12 }} />
                          {result.phone}
                        </Typography>
                      )}
                    </Box>
                  </Box>

                  {/* Products */}
                  {result.products && result.products.length > 0 && (
                    <>
                      <Divider sx={{ mb: 1.5 }} />
                      <Typography variant="caption" color="text.secondary" fontWeight={600} display="block" mb={1}>
                        PRODUCTOS ENCONTRADOS ({result.products.length})
                      </Typography>
                      <Box display="flex" flexDirection="column" gap={1}>
                        {result.products.map((p: SearchProduct) => (
                          <Box
                            key={p.id}
                            display="flex"
                            alignItems="center"
                            justifyContent="space-between"
                            sx={{
                              px: 1.5,
                              py: 1,
                              borderRadius: 1.5,
                              bgcolor: 'action.hover',
                            }}
                          >
                            <Box minWidth={0} flex={1}>
                              <Typography variant="body2" fontWeight={600} noWrap>
                                {p.name}
                              </Typography>
                              {p.categoryName && (
                                <Typography variant="caption" color="text.secondary">
                                  {p.categoryName}
                                </Typography>
                              )}
                            </Box>
                            <Box display="flex" alignItems="center" gap={1} flexShrink={0}>
                              <Typography variant="body2" fontWeight={700} color="primary.main">
                                {p.currency ?? 'CUP'} {p.price.toFixed(2)}
                              </Typography>
                              <Chip
                                label={p.isAvailable ? 'Disponible' : 'No disp.'}
                                size="small"
                                color={p.isAvailable ? 'success' : 'default'}
                                variant="outlined"
                                sx={{ fontSize: '0.65rem' }}
                              />
                            </Box>
                          </Box>
                        ))}
                      </Box>
                    </>
                  )}
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}
    </Box>
  );
}
