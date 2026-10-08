import { useNavigate, useLocation } from 'react-router-dom';
import {
  Box, Drawer, List, ListItemButton, ListItemIcon, ListItemText,
  Typography, Divider, Avatar, Tooltip, IconButton, Chip,
} from '@mui/material';
import {
  Dashboard as DashboardIcon,
  Store as StoreIcon,
  Person as PersonIcon,
  Logout as LogoutIcon,
  LightMode as LightModeIcon,
  DarkMode as DarkModeIcon,
  Search as SearchIcon,
  AdminPanelSettings as AdminIcon,
  ChevronLeft as ChevronLeftIcon,
  LocalShipping as WholesaleIcon,
  PointOfSale as PosIcon,
  Receipt as SalesIcon,
  Inventory2 as InventoryIcon,
  AccountBalanceWallet as CashIcon,
  Groups as UsersIcon,
  SupportAgent as ManagersIcon,
  Assignment as RequestsIcon,
  ShoppingCart as PurchasesIcon,
  SwapHoriz as MovementsIcon,
  FactCheck as CountIcon,
  ContactPhone as SuppliersIcon,
  Payments as ExpensesIcon,
  Settings as SettingsIcon,
  Security as AuditIcon,
  Assessment as ReportsIcon,
  CloudSync as SyncIcon,
} from '@mui/icons-material';
import { useAuth } from '@/context/AuthContext';
import { useThemeMode } from '@/context/ThemeContext';
import { useOpsRole } from '@/hooks/useOpsRole';
import { SPRING } from '@/lib/motion';

export const DRAWER_WIDTH = 248;
export const DRAWER_COLLAPSED_WIDTH = 64;

const navItems = [
  { label: 'Panel', icon: <DashboardIcon fontSize="small" />, path: '/dashboard' },
  { label: 'Búsqueda', icon: <SearchIcon fontSize="small" />, path: '/dashboard/search' },
  { label: 'Mi cuenta', icon: <PersonIcon fontSize="small" />, path: '/dashboard/account' },
];

const wholesaleCatalogItem = {
  label: 'Catálogo Mayorista', icon: <WholesaleIcon fontSize="small" />, path: '/dashboard/wholesale-catalog',
};

const opsItems = [
  { label: 'Punto de Venta', icon: <PosIcon fontSize="small" />, path: '/dashboard/ops/pos' },
  { label: 'Ventas', icon: <SalesIcon fontSize="small" />, path: '/dashboard/ops/sales' },
  { label: 'Gestores', icon: <ManagersIcon fontSize="small" />, path: '/dashboard/ops/managers' },
  { label: 'Inventario', icon: <InventoryIcon fontSize="small" />, path: '/dashboard/ops/inventory' },
  { label: 'Movimientos', icon: <MovementsIcon fontSize="small" />, path: '/dashboard/ops/movements' },
  { label: 'Conteo', icon: <CountIcon fontSize="small" />, path: '/dashboard/ops/counts' },
  { label: 'Solicitudes', icon: <RequestsIcon fontSize="small" />, path: '/dashboard/ops/purchase-requests' },
  { label: 'Compras', icon: <PurchasesIcon fontSize="small" />, path: '/dashboard/ops/purchases' },
  { label: 'Proveedores', icon: <SuppliersIcon fontSize="small" />, path: '/dashboard/ops/suppliers' },
  { label: 'Gastos', icon: <ExpensesIcon fontSize="small" />, path: '/dashboard/ops/expenses' },
  { label: 'Caja', icon: <CashIcon fontSize="small" />, path: '/dashboard/ops/cash' },
  { label: 'Estadísticas del Negocio', icon: <ReportsIcon fontSize="small" />, path: '/dashboard/ops/reports' },
  { label: 'Auditoría', icon: <AuditIcon fontSize="small" />, path: '/dashboard/ops/audit-log' },
  { label: 'Configuración', icon: <SettingsIcon fontSize="small" />, path: '/dashboard/ops/settings' },
  { label: 'Sincronización', icon: <SyncIcon fontSize="small" />, path: '/dashboard/ops/sync' },
];

const opsUsersItem = { label: 'Usuarios (TPV)', icon: <UsersIcon fontSize="small" />, path: '/dashboard/ops/users' };

// Qué ítems del menú TPV son relevantes para cada rol operativo (el Administrador ve todos).
// Refleja la matriz de [OpsRoles] del backend: si un rol no puede hacer nada útil en esa
// pantalla, no tiene sentido mostrársela.
const OPS_ITEM_ROLES: Record<string, string[]> = {
  'Punto de Venta': ['Cajero', 'JefeDeTurno'],
  'Ventas': ['Cajero', 'JefeDeTurno'],
  'Caja': ['Cajero', 'JefeDeTurno'],
  'Gestores': ['Observador'],
  'Inventario': ['Almacenero', 'JefeDeTurno'],
  'Movimientos': ['Almacenero', 'JefeDeTurno'],
  'Conteo': ['Cajero', 'Almacenero', 'JefeDeTurno'],
  'Solicitudes': ['Almacenero', 'JefeDeTurno'],
  'Compras': ['Comercial', 'JefeDeTurno'],
  'Proveedores': ['Comercial'],
  'Gastos': ['Comercial', 'JefeDeTurno'],
  'Estadísticas del Negocio': ['Auditor', 'JefeDeTurno'],
  'Auditoría': ['Auditor'],
  // Sincronización: el push (subir) lo puede disparar cualquier trabajador; el pull
  // (bajar) queda restringido a Administrador dentro de la propia pantalla.
  'Sincronización': ['Cajero', 'JefeDeTurno', 'Almacenero', 'Comercial', 'Auditor'],
  // 'Configuración' es de gestión exclusiva del Administrador.
};

const adminItems = [
  { label: 'Administración', icon: <AdminIcon fontSize="small" />, path: '/admin' },
];

interface SidebarProps {
  mobileOpen: boolean;
  onClose: () => void;
  collapsed: boolean;
  onToggleCollapse: () => void;
}

interface NavItemProps {
  label: string;
  icon: React.ReactNode;
  path: string;
  active: boolean;
  onClick: () => void;
  accent?: 'primary' | 'warning';
  collapsed?: boolean;
}

function NavItem({ label, icon, active, onClick, accent = 'primary', collapsed = false }: NavItemProps) {
  const colorMap = {
    primary: { bg: 'rgba(37, 99, 235, 0.09)', text: '#2563EB', hover: 'rgba(37, 99, 235, 0.05)', hoverGrad: 'linear-gradient(90deg, rgba(37,99,235,0.14) 0%, rgba(124,58,237,0.08) 100%)', glow: '0 4px 16px rgba(37,99,235,0.22)' },
    warning: { bg: 'rgba(217, 119, 6, 0.1)', text: '#D97706', hover: 'rgba(217, 119, 6, 0.05)', hoverGrad: 'linear-gradient(90deg, rgba(217,119,6,0.16) 0%, rgba(239,68,68,0.08) 100%)', glow: '0 4px 16px rgba(217,119,6,0.22)' },
  };
  const c = colorMap[accent];

  const button = (
    <ListItemButton
      onClick={onClick}
      selected={active}
      sx={{
        borderRadius: 2,
        mb: 0.25,
        px: collapsed ? 0 : 1.5,
        py: 1,
        justifyContent: collapsed ? 'center' : 'flex-start',
        position: 'relative',
        transition: `background 0.2s ease-out, box-shadow 0.25s ease-out, color 0.2s ease-out`,
        '&::before': {
          content: '""',
          position: 'absolute',
          left: 0,
          top: '20%',
          height: '60%',
          width: 3,
          borderRadius: '0 3px 3px 0',
          background: c.text,
          opacity: 0,
          transition: 'opacity 0.2s ease-out',
        },
        '&:hover': {
          background: active ? c.bg : c.hoverGrad,
          boxShadow: active ? 'none' : c.glow,
          '&::before': { opacity: active ? 0 : 0.5 },
          '& .MuiListItemIcon-root': { color: c.text, transform: 'translateX(3px) scale(1.12)' },
          '& .MuiListItemText-primary': { color: c.text },
        },
        '&.Mui-selected': {
          background: c.bg,
          color: c.text,
          '& .MuiListItemIcon-root': { color: c.text },
          '&:hover': { background: c.bg },
          '&::before': { opacity: 1 },
        },
      }}
    >
      <ListItemIcon sx={{
        minWidth: collapsed ? 0 : 34,
        color: active ? c.text : 'text.secondary',
        justifyContent: 'center',
        transition: `transform 0.35s ${SPRING}, color 0.2s ease-out`,
      }}>
        {icon}
      </ListItemIcon>
      {!collapsed && (
        <ListItemText
          primary={label}
          primaryTypographyProps={{
            fontSize: '0.875rem',
            fontWeight: active ? 700 : 500,
            color: active ? c.text : 'text.primary',
          }}
        />
      )}
    </ListItemButton>
  );

  if (collapsed) {
    return <Tooltip title={label} placement="right">{button}</Tooltip>;
  }
  return button;
}

export default function Sidebar({ mobileOpen, onClose, collapsed, onToggleCollapse }: SidebarProps) {
  const { user, logout } = useAuth();
  const { mode, toggleMode } = useThemeMode();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const opsRole = useOpsRole();
  const isOpsAdmin = opsRole === 'Administrador';
  const visibleOpsItems = isOpsAdmin
    ? opsItems
    : opsItems.filter((item) => OPS_ITEM_ROLES[item.label]?.includes(opsRole));

  const handleNav = (path: string) => { navigate(path); onClose(); };
  const handleLogout = async () => { await logout(); navigate('/login'); };

  const drawerWidth = collapsed ? DRAWER_COLLAPSED_WIDTH : DRAWER_WIDTH;

  const drawer = (
    <div className="h-full flex flex-col">
      {/* Brand header */}
      <div style={{ padding: collapsed ? '20px 0' : '20px 16px', display: 'flex', alignItems: 'center', justifyContent: collapsed ? 'center' : 'space-between', gap: 10 }}>
        {collapsed ? (
          <Tooltip title="Expandir menú" placement="right">
            <div
              className="w-8 h-8 rounded-lg bg-gradient-to-br from-blue-500 to-violet-600 flex items-center justify-center flex-shrink-0 shadow-sm cursor-pointer"
              onClick={onToggleCollapse}
            >
              <StoreIcon sx={{ fontSize: 16, color: 'white' }} />
            </div>
          </Tooltip>
        ) : (
          <>
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-blue-500 to-violet-600 flex items-center justify-center flex-shrink-0 shadow-sm">
                <StoreIcon sx={{ fontSize: 16, color: 'white' }} />
              </div>
              <Typography variant="body2" fontWeight={800} letterSpacing="-0.03em" noWrap>
                MerkaCuba
              </Typography>
            </div>
            <Tooltip title="Colapsar menú">
              <IconButton size="small" onClick={onToggleCollapse} sx={{ color: 'text.secondary', '&:hover': { bgcolor: 'action.hover' } }}>
                <ChevronLeftIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          </>
        )}
      </div>

      <Divider />

      {/* Nav */}
      <List sx={{ flex: 1, px: 1, pt: 1.5, overflow: 'auto' }}>
        {!collapsed && (
          <Typography variant="caption" sx={{ px: 1.5, mb: 0.5, display: 'block', fontWeight: 700, letterSpacing: '0.08em', color: 'text.disabled', fontSize: '0.68rem' }}>
            MENÚ PRINCIPAL
          </Typography>
        )}

        {(user?.tenantType === 'Retail' ? [...navItems, wholesaleCatalogItem] : navItems).map((item) => {
          const isActive = pathname === item.path ||
            (item.path !== '/dashboard' && pathname.startsWith(item.path));
          return (
            <NavItem
              key={item.path}
              {...item}
              active={isActive}
              onClick={() => handleNav(item.path)}
              collapsed={collapsed}
            />
          );
        })}

        <div className="mt-3 mb-0.5">
          <Divider />
        </div>

        {!collapsed && (
          <Typography variant="caption" sx={{ px: 1.5, mt: 1.5, mb: 0.5, display: 'block', fontWeight: 700, letterSpacing: '0.08em', color: 'text.disabled', fontSize: '0.68rem' }}>
            OPERACIONES (TPV)
          </Typography>
        )}

        {(user?.isOpsUser ? visibleOpsItems : [...visibleOpsItems, opsUsersItem]).map((item) => {
          const isActive = pathname === item.path || pathname.startsWith(item.path);
          return (
            <NavItem
              key={item.path}
              {...item}
              active={isActive}
              onClick={() => handleNav(item.path)}
              collapsed={collapsed}
            />
          );
        })}

        {user?.role === 'admin' && (
          <>
            <div className="mt-3 mb-0.5">
              <Divider />
            </div>

            {!collapsed && (
              <Typography variant="caption" sx={{ px: 1.5, mt: 1.5, mb: 0.5, display: 'block', fontWeight: 700, letterSpacing: '0.08em', color: 'text.disabled', fontSize: '0.68rem' }}>
                ADMINISTRACIÓN
              </Typography>
            )}

            {adminItems.map((item) => {
              const isActive = pathname.startsWith(item.path);
              return (
                <NavItem
                  key={item.path}
                  {...item}
                  active={isActive}
                  onClick={() => handleNav(item.path)}
                  accent="warning"
                  collapsed={collapsed}
                />
              );
            })}
          </>
        )}
      </List>

      <Divider />

      {/* User card */}
      <div className="p-3">
        {collapsed ? (
          <div className="flex flex-col items-center gap-1">
            <Tooltip title={user?.businessName ?? ''} placement="right">
              <Avatar sx={{ width: 34, height: 34, fontSize: 13, fontWeight: 700, background: 'linear-gradient(135deg, #2563EB 0%, #7C3AED 100%)' }}>
                {user?.businessName?.[0]?.toUpperCase() ?? 'B'}
              </Avatar>
            </Tooltip>
            <Tooltip title={mode === 'light' ? 'Modo oscuro' : 'Modo claro'} placement="right">
              <IconButton size="small" onClick={toggleMode} sx={{ '&:hover': { bgcolor: 'action.hover' } }}>
                {mode === 'light'
                  ? <DarkModeIcon sx={{ fontSize: 16, color: 'text.secondary' }} />
                  : <LightModeIcon sx={{ fontSize: 16, color: 'text.secondary' }} />}
              </IconButton>
            </Tooltip>
            <Tooltip title="Cerrar sesión" placement="right">
              <IconButton size="small" onClick={handleLogout} sx={{ '&:hover': { bgcolor: 'error.lighter' } }}>
                <LogoutIcon sx={{ fontSize: 16, color: 'error.main' }} />
              </IconButton>
            </Tooltip>
          </div>
        ) : (
          <>
            <Box
              onClick={() => handleNav('/dashboard/account')}
              sx={{
                display: 'flex',
                alignItems: 'center',
                gap: 1.25,
                px: 1,
                py: 1,
                borderRadius: 3,
                bgcolor: 'rgba(0,0,0,0.04)',
                border: '1px solid transparent',
                cursor: 'pointer',
                transition: `background 0.25s ease-out, border-color 0.25s ease-out, box-shadow 0.25s ease-out`,
                '&:hover': {
                  background: 'linear-gradient(135deg, rgba(37,99,235,0.12) 0%, rgba(124,58,237,0.12) 100%)',
                  borderColor: 'rgba(37,99,235,0.32)',
                  boxShadow: '0 4px 20px rgba(37,99,235,0.18)',
                  '& .user-avatar': {
                    boxShadow: '0 0 0 3px rgba(37,99,235,0.25)',
                    transform: 'scale(1.05)',
                  },
                },
              }}
            >
              <Avatar className="user-avatar" sx={{ width: 34, height: 34, fontSize: 13, fontWeight: 700, background: 'linear-gradient(135deg, #2563EB 0%, #7C3AED 100%)', flexShrink: 0, transition: `transform 0.4s ${SPRING}, box-shadow 0.25s ease-out` }}>
                {user?.businessName?.[0]?.toUpperCase() ?? 'B'}
              </Avatar>
              <div className="flex-1 min-w-0">
                <Typography variant="caption" fontWeight={600} noWrap display="block" lineHeight={1.3}>
                  {user?.businessName}
                </Typography>
                <Typography variant="caption" color="text.secondary" noWrap display="block" lineHeight={1.3}>
                  {user?.email}
                </Typography>
              </div>
              <div className="flex gap-0.5 flex-shrink-0">
                <Tooltip title={mode === 'light' ? 'Modo oscuro' : 'Modo claro'}>
                  <IconButton size="small" onClick={(e) => { e.stopPropagation(); toggleMode(); }} sx={{ '&:hover': { bgcolor: 'action.hover' } }}>
                    {mode === 'light'
                      ? <DarkModeIcon sx={{ fontSize: 16, color: 'text.secondary' }} />
                      : <LightModeIcon sx={{ fontSize: 16, color: 'text.secondary' }} />}
                  </IconButton>
                </Tooltip>
                <Tooltip title="Cerrar sesión">
                  <IconButton size="small" onClick={(e) => { e.stopPropagation(); handleLogout(); }} sx={{ '&:hover': { bgcolor: 'error.lighter', color: 'error.main' } }}>
                    <LogoutIcon sx={{ fontSize: 16, color: 'error.main' }} />
                  </IconButton>
                </Tooltip>
              </div>
            </Box>
            {user?.plan && (
              <div className="mt-2 px-2">
                <Chip
                  label={`Plan ${user.plan}`}
                  size="small"
                  variant="outlined"
                  color="primary"
                  sx={{ fontSize: '0.65rem', height: 20, fontWeight: 600, width: '100%', borderRadius: 1 }}
                />
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );

  return (
    <Box component="nav" sx={{ width: { md: drawerWidth }, flexShrink: { md: 0 }, transition: 'width 0.2s ease' }}>
      <Drawer
        variant="temporary"
        open={mobileOpen}
        onClose={onClose}
        ModalProps={{ keepMounted: true }}
        sx={{
          display: { xs: 'block', md: 'none' },
          '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box' },
        }}
      >
        {drawer}
      </Drawer>
      <Drawer
        variant="permanent"
        sx={{
          display: { xs: 'none', md: 'block' },
          '& .MuiDrawer-paper': {
            width: drawerWidth,
            boxSizing: 'border-box',
            borderRight: '1px solid',
            borderColor: 'divider',
            overflowX: 'hidden',
            transition: 'width 0.2s ease',
          },
        }}
        open
      >
        {drawer}
      </Drawer>
    </Box>
  );
}

