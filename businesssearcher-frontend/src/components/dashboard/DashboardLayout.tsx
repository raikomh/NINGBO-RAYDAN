import { useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { Box, AppBar, Toolbar, IconButton, Typography, useTheme, useMediaQuery, Breadcrumbs } from '@mui/material';
import { Menu as MenuIcon, NavigateNext as NextIcon } from '@mui/icons-material';
import Sidebar, { DRAWER_WIDTH, DRAWER_COLLAPSED_WIDTH } from './Sidebar';
import ChatWidget from '@/components/chat/ChatWidget';
import OpsNotificationsBell from './OpsNotificationsBell';

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

  const sidebarWidth = collapsed ? DRAWER_COLLAPSED_WIDTH : DRAWER_WIDTH;

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

        {/* Page content */}
        <Box sx={{ p: { xs: 2, md: 3 }, flex: 1 }} className="page-enter">
          <Outlet />
        </Box>
      </Box>

      <ChatWidget />
    </Box>
  );
}
