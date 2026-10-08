import { useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Box, Card, CardActionArea, CardContent, Typography, CircularProgress } from '@mui/material';
import { Storefront } from '@mui/icons-material';
import { useWarehouses } from '@/hooks/useOps';
import { useActiveStore } from '@/context/StoreContext';
import { useAuth } from '@/context/AuthContext';

/**
 * Pantalla intermedia tras el login: una MiPyme puede tener varias tiendas (almacenes) cuyos
 * datos no se comparten entre sí. Si solo hay 0 o 1, se salta directo (no tiene sentido elegir).
 * Si hay 2+, el usuario elige con cuál va a trabajar en esta sesión antes de entrar al dashboard.
 */
export default function StoreSelectPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const next = params.get('next') || '/dashboard';
  const { data: warehouses, isLoading } = useWarehouses();
  const { setStoreId } = useActiveStore();
  const { user } = useAuth();

  const pick = (id: string) => {
    setStoreId(id);
    navigate(next, { replace: true });
  };

  // Nada que elegir: pasa de largo automáticamente.
  useEffect(() => {
    if (!isLoading && (warehouses?.length ?? 0) < 2) {
      setStoreId(warehouses?.[0]?.id ?? null);
      navigate(next, { replace: true });
    }
  }, [isLoading, warehouses]);

  if (isLoading || (warehouses?.length ?? 0) < 2) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh">
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        p: 3,
        bgcolor: 'background.default',
      }}
    >
      <Typography variant="h4" fontWeight={800} textAlign="center">
        ¿Con cuál tienda vas a trabajar?
      </Typography>
      <Typography variant="body1" color="text.secondary" textAlign="center" mt={1} mb={5}>
        {user?.businessName ? `${user.businessName} — ` : ''}cada tienda tiene su propio inventario, ventas y estadísticas.
      </Typography>

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', md: 'repeat(3, 1fr)' },
          gap: 3,
          width: '100%',
          maxWidth: 900,
        }}
      >
        {warehouses!.map((w) => (
          <Card
            key={w.id}
            variant="outlined"
            sx={{
              borderRadius: 3,
              transition: 'transform 0.25s ease, box-shadow 0.25s ease, border-color 0.25s ease',
              '&:hover': {
                transform: 'translateY(-6px)',
                boxShadow: '0 16px 32px -12px rgba(37, 99, 235, 0.35)',
                borderColor: 'primary.main',
              },
            }}
          >
            <CardActionArea onClick={() => pick(w.id)} sx={{ height: '100%' }}>
              <CardContent sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center', py: 5, px: 3 }}>
                <Box
                  sx={{
                    width: 64, height: 64, borderRadius: '50%', display: 'flex',
                    alignItems: 'center', justifyContent: 'center', bgcolor: 'primary.main',
                    color: 'primary.contrastText', mb: 2,
                  }}
                >
                  <Storefront fontSize="large" />
                </Box>
                <Typography variant="h6" fontWeight={700}>{w.name}</Typography>
                {w.location && (
                  <Typography variant="body2" color="text.secondary" mt={0.5}>{w.location}</Typography>
                )}
              </CardContent>
            </CardActionArea>
          </Card>
        ))}
      </Box>
    </Box>
  );
}
