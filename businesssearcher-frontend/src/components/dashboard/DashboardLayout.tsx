import { useState } from 'react';
import { Outlet, useLocation, Link as RouterLink } from 'react-router-dom';
import { Box, AppBar, Toolbar, IconButton, Typography, useTheme, useMediaQuery, Breadcrumbs, Alert, Link } from '@mui/material';
import { Menu as MenuIcon, NavigateNext as NextIcon } from '@mui/icons-material';
import Sidebar, { DRAWER_WIDTH, DRAWER_COLLAPSED_WIDTH } from './Sidebar';
import ChatWidget from '@/components/chat/ChatWidget';
import OpsNotificationsBell from './OpsNotificationsBell';
import { useAuth } from '@/context/AuthContext';
import { isLocalDeployment } from '@/lib/deployment';

const ROUTE_LABELS: Record<string, string> = {
  '/dashboard': 'Panel',
  '/dashboard/search': 'Búsqueda',
  '/dashboard/account': 'Mi cuenta',
  '/dashboard/ops/reports': 'Estadísticas del Negocio',
  '/admin': 'Administración',
};

function usePageTitle(pathname: string) {
  const exact = ROUTE_LABELS[pathname];
  if (exact) return exact;
  return 'Panel';
}

export default function DashboardLayout() {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(false);
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const { pathname } = useLocation();
  const pageTitle = usePageTitle(pathname);
  const { user } = useAuth();

  const sidebarWidth = collapsed ? DRAWER_COLLAPSED_WIDTH : DRAWER_WIDTH;

  // Local y Online: bloqueo total (no solo lectura) cuando venció el mes — coincide con el 402
  // de SubscriptionEnforcementMiddleware (backend), que aplica en ambos modos. No aplica al
  // admin (nunca se bloquea) ni a las sesiones de empleado del TPV (isSubscriptionActive solo
  // viene en el perfil del dueño).
  const showExpiredBanner =
    user?.role !== 'admin' && !user?.isOpsUser && user?.isSubscriptionActive === false;

  // Aviso previo al corte (no bloqueante): se calcula al vuelo con el nextPaymentDate que ya
  // trae el perfil, sin depender de ningún job periódico (ver AccountPage para el detalle).
  const daysUntilExpiry = user?.nextPaymentDate
    ? Math.ceil((new Date(user.nextPaymentDate).getTime() - Date.now()) / 86_400_000)
    : null;
  const showExpiringSoonBanner =
    user?.role !== 'admin' && !user?.isOpsUser && user?.isSubscriptionActive !== false &&
    daysUntilExpiry !== null && daysUntilExpiry >= 0 && daysUntilExpiry <= 5;

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <Sidebar
        mobileOpen={mobileOpen}
        onClose={() => setMobileOpen(false)}
        collapsed={collapsed}
        onToggleCollapse={() => setCollapsed(c => !c)}
      />

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          width: { md: `calc(100% - ${sidebarWidth}px)` },
          transition: 'width 0.2s ease',
          bgcolor: 'background.default',
          minHeight: '100vh',
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        {/* Top bar */}
        <AppBar
          position="sticky"
          elevation={0}
          sx={{
            bgcolor: 'background.paper',
            borderBottom: '1px solid',
            borderColor: 'divider',
            color: 'text.primary',
          }}
        >
          <Toolbar sx={{ minHeight: '52px !important', gap: 1.5 }}>
            {isMobile && (
              <IconButton
                edge="start"
                onClick={() => setMobileOpen(true)}
                sx={{ color: 'text.primary', mr: 0.5 }}
              >
                <MenuIcon />
              </IconButton>
            )}

            <Typography variant="subtitle1" fontWeight={700} sx={{ flex: 1 }}>
              {pageTitle}
            </Typography>

            <OpsNotificationsBell />
          </Toolbar>
        </AppBar>

        {showExpiredBanner && (
          <Alert severity="error" sx={{ borderRadius: 0 }}>
            Tu suscripción venció y tu cuenta está bloqueada — no puedes crear ni editar nada
            hasta que se regularice el pago.{' '}
            {isLocalDeployment ? (
              <Link component={RouterLink} to="/dashboard/ops/sync" underline="hover" fontWeight={600}>
                Paga en línea y sincroniza aquí
              </Link>
            ) : (
              <>
                <Link component={RouterLink} to="/dashboard/account" underline="hover" fontWeight={600}>
                  Ver mi cuenta
                </Link>{' '}
                o escríbenos por el chat de soporte para coordinar el pago.
              </>
            )}
          </Alert>
        )}

        {showExpiringSoonBanner && (
          <Alert severity="warning" sx={{ borderRadius: 0 }}>
            Tu plan vence en {daysUntilExpiry} día{daysUntilExpiry !== 1 ? 's' : ''}.{' '}
            <Link component={RouterLink} to="/dashboard/account" underline="hover" fontWeight={600}>
              Reporta tu pago aquí
            </Link>{' '}
            para que no se bloquee tu cuenta.
          </Alert>
        )}

        {/* Page content */}
        <Box sx={{ p: { xs: 2, md: 3 }, flex: 1 }} className="page-enter">
          <Outlet />
        </Box>
      </Box>

      <ChatWidget />
    </Box>
  );
}
