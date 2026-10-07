import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Typography, Grid, CircularProgress, Avatar, Chip, Box, Button } from '@mui/material';
import {
  Store as StoreIcon, Schedule, ArrowForward as ArrowIcon, Add,
  Search as SearchIcon, BarChart as AnalyticsIcon, Inventory2 as ProductsIcon,
} from '@mui/icons-material';
import { useAuth } from '@/context/AuthContext';
import { useStores } from '@/hooks/useStores';
import { useOpsProducts } from '@/hooks/useOps';
import StatCard from '@/components/shared/StatCard';
import { SPRING } from '@/lib/motion';

export default function OverviewPage() {
  const { user } = useAuth();
  const { data: store, isLoading } = useStores();
  const { data: opsProducts } = useOpsProducts();
  const navigate = useNavigate();
  const [bannerHovered, setBannerHovered] = useState(false);

  const totalSchedules = store?.schedules?.length ?? 0;
  const totalProducts = opsProducts?.length ?? 0;
  const lowStockCount = opsProducts?.filter((p) => p.totalStock <= p.minStock).length ?? 0;

  if (isLoading) {
    return (
      <div className="flex justify-center py-20">
        <CircularProgress />
      </div>
    );
  }

  const hour = new Date().getHours();
  const greeting = hour < 12 ? 'Buenos días' : hour < 19 ? 'Buenas tardes' : 'Buenas noches';

  const quickActions = [
    {
      label: 'Buscar productos',
      icon: <SearchIcon fontSize="small" />,
      action: () => navigate('/dashboard/search'),
    },
    {
      label: 'Ver analíticas',
      icon: <AnalyticsIcon fontSize="small" />,
      action: () => navigate('/dashboard/ops/reports'),
    },
  ];

  return (
    <div className="space-y-8">
      {/* Hero welcome banner */}
      <div
        className="relative overflow-hidden rounded-2xl bg-gradient-to-r from-blue-600 to-violet-600 p-6 md:p-8 text-white"
        onMouseEnter={() => setBannerHovered(true)}
        onMouseLeave={() => setBannerHovered(false)}
        style={{
          boxShadow: bannerHovered
            ? '0 32px 64px rgba(79,70,229,0.55), 0 16px 32px rgba(79,70,229,0.30)'
            : '0 10px 25px rgba(79,70,229,0.30)',
          transform: bannerHovered ? 'translateY(-8px)' : 'translateY(0)',
          transition: `transform 0.45s ${SPRING}, box-shadow 0.35s ease-out`,
        }}
      >
        {/* Círculo 1 — escala y se desplaza hacia arriba-izquierda */}
        <div
          className="absolute -top-8 -right-8 w-40 h-40 bg-white/10 rounded-full blur-2xl pointer-events-none"
          style={{
            transform: bannerHovered ? 'scale(1.35) translate(-8px, -8px)' : 'scale(1)',
            transition: `transform 0.6s ${SPRING}`,
          }}
        />
        {/* Círculo 2 — delay 60ms, se desplaza hacia abajo-derecha */}
        <div
          className="absolute bottom-0 right-24 w-24 h-24 bg-violet-400/20 rounded-full blur-xl pointer-events-none"
          style={{
            transform: bannerHovered ? 'scale(1.5) translate(6px, 4px)' : 'scale(1)',
            transition: `transform 0.55s 0.06s ${SPRING}`,
          }}
        />
        {/* Círculo 3 — aparece desde opacidad 0 y scale 0.6, delay 120ms */}
        <div
          className="absolute bottom-2 left-8 w-28 h-28 bg-blue-300/10 rounded-full blur-2xl pointer-events-none"
          style={{
            opacity: bannerHovered ? 1 : 0,
            transform: bannerHovered ? 'scale(1)' : 'scale(0.6)',
            transition: `opacity 0.4s ease-out, transform 0.65s 0.12s ${SPRING}`,
          }}
        />

        <div className="relative">
          <p className="text-blue-200 text-sm font-medium mb-1">{greeting} 👋</p>
          <h1
            className="text-2xl md:text-3xl font-extrabold tracking-tight mb-2"
            style={{
              transform: bannerHovered ? 'scale(1.06)' : 'scale(1)',
              transformOrigin: 'left center',
              transition: `transform 0.4s ${SPRING}`,
            }}
          >
            {user?.businessName}
          </h1>
          <p className="text-blue-100 text-sm max-w-md">
            Aquí tienes un resumen de tu actividad. Gestiona tus tiendas, revisa analíticas y registra transacciones.
          </p>

          {/* Quick actions — entran desde X offset escalonado al hover del banner */}
          <div className="flex flex-wrap gap-2 mt-4">
            {quickActions.map(({ label, icon, action }, index) => (
              <Button
                key={label}
                size="small"
                startIcon={icon}
                onClick={action}
                style={{
                  transform: bannerHovered ? 'translateX(0)' : `translateX(${(index + 1) * 4}px)`,
                  transition: `transform 0.4s ${SPRING}, background-color 0.25s ease-out, border-color 0.25s ease-out`,
                  transitionDelay: `${index * 40}ms`,
                  backgroundColor: bannerHovered ? 'rgba(255,255,255,0.18)' : 'rgba(255,255,255,0.10)',
                }}
                sx={{
                  color: 'white',
                  backdropFilter: 'blur(4px)',
                  border: '1px solid rgba(255,255,255,0.25)',
                  textTransform: 'none',
                  fontWeight: 600,
                  fontSize: '0.78rem',
                  borderRadius: 2,
                  '&:hover': { bgcolor: 'rgba(255,255,255,0.30)', borderColor: 'rgba(255,255,255,0.55)' },
                  '&.Mui-disabled': { opacity: 0.4, color: 'rgba(255,255,255,0.6)' },
                }}
              >
                {label}
              </Button>
            ))}
          </div>
        </div>
      </div>

      {/* Stats */}
      <div>
        <Typography variant="caption" fontWeight={700} color="text.disabled" letterSpacing="0.1em" sx={{ textTransform: 'uppercase', display: 'block', mb: 1.5 }}>
          Resumen general
        </Typography>
        <Grid container spacing={2.5}>
          <Grid item xs={12} sm={6}>
            <StatCard
              title="Productos"
              value={totalProducts}
              icon={<ProductsIcon />}
              color="#2563EB"
              subtitle={
                totalProducts === 0
                  ? 'Sin productos aún →'
                  : lowStockCount > 0
                    ? `${lowStockCount} con stock bajo →`
                    : 'Ver inventario →'
              }
              onClick={() => navigate('/dashboard/ops/inventory')}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <StatCard
              title="Horarios configurados"
              value={totalSchedules}
              icon={<Schedule />}
              color="#10B981"
              subtitle={totalSchedules === 0 ? 'Sin horarios aún' : 'Días de apertura'}
            />
          </Grid>
        </Grid>
      </div>

      {/* Store card */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <Typography variant="caption" fontWeight={700} color="text.disabled" letterSpacing="0.1em" sx={{ textTransform: 'uppercase' }}>
            Mi tienda
          </Typography>
        </div>

        {!store ? (
          <div className="rounded-2xl border-2 border-dashed border-gray-200 dark:border-gray-700 p-8 text-center">
            <StoreIcon sx={{ fontSize: 36, color: 'text.disabled', mb: 1 }} />
            <Typography variant="body2" color="text.secondary" mb={3}>Todavía no has configurado tu tienda</Typography>
            <Button
              variant="contained"
              size="small"
              startIcon={<Add />}
              onClick={() => navigate('/dashboard/ops/settings')}
              sx={{ textTransform: 'none', fontWeight: 600, borderRadius: 2 }}
            >
              Configurar tienda
            </Button>
          </div>
        ) : (
          <Box
            onClick={() => navigate('/dashboard/ops/settings')}
            sx={{
              p: 2.5,
              borderRadius: 3,
              border: '1px solid rgba(0,0,0,0.08)',
              borderLeft: '4px solid #2563EB',
              bgcolor: 'background.paper',
              display: 'flex',
              alignItems: 'center',
              gap: 2,
              cursor: 'pointer',
              transition: `transform 0.30s ${SPRING}, box-shadow 0.25s ease, border-color 0.2s ease`,
              '&:hover': {
                transform: 'translateY(-12px)',
                boxShadow: '0 24px 48px rgba(37,99,235,0.45), 0 6px 12px rgba(0,0,0,0.08)',
                borderColor: 'rgba(37,99,235,0.6)',
                '& .store-arrow': { opacity: 1, transform: 'translateX(0)' },
                '& .store-avatar': {
                  boxShadow: '0 8px 20px -4px rgba(37,99,235,0.50)',
                  transform: 'scale(1.10)',
                },
              },
            }}
          >
            <Avatar
              className="store-avatar"
              src={store.logoUrl}
              sx={{
                width: 42,
                height: 42,
                borderRadius: 2,
                background: 'linear-gradient(135deg, #2563EB 0%, #7C3AED 100%)',
                fontSize: 16,
                fontWeight: 700,
                flexShrink: 0,
                transition: `transform 0.4s ${SPRING}, box-shadow 0.25s ease-out`,
              }}
            >
              {store?.name?.[0]?.toUpperCase()}
            </Avatar>

            <div className="flex-1 min-w-0">
              <Typography variant="body2" fontWeight={600} noWrap>
                {store.name}
              </Typography>
              <Typography variant="caption" color="text.secondary" noWrap display="block">
                {store.address?.city ?? 'Sin ciudad'} {store.phone ? `· ${store.phone}` : ''}
              </Typography>
            </div>

            {store.isOpen !== undefined && (
              <Chip
                label={store.isOpen ? 'Abierta' : 'Cerrada'}
                size="small"
                color={store.isOpen ? 'success' : 'default'}
                sx={{ fontSize: '0.65rem', height: 18, flexShrink: 0 }}
              />
            )}

            <ArrowIcon
              className="store-arrow"
              fontSize="small"
              sx={{
                color: 'primary.main',
                opacity: 0,
                transform: 'translateX(-6px)',
                transition: `opacity 0.2s ease-out, transform 0.35s ${SPRING}`,
                flexShrink: 0,
              }}
            />
          </Box>
        )}
      </div>
    </div>
  );
}
