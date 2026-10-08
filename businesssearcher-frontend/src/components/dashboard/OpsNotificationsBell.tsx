import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  IconButton, Badge, Popover, Box, Typography, List, ListItemButton, ListItemIcon, ListItemText, Divider,
} from '@mui/material';
import { Notifications as NotificationsIcon, Warning, TrendingUp } from '@mui/icons-material';
import { useOperationalNotifications, useNotificationsUnreadCount, useMarkNotificationsSeen } from '@/hooks/useOps';

const TYPE_ICON: Record<string, React.ReactNode> = {
  LowStock: <Warning fontSize="small" color="warning" />,
  PriceChange: <TrendingUp fontSize="small" color="info" />,
};

function timeAgo(dateStr: string) {
  const diffMs = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diffMs / 60_000);
  if (mins < 1) return 'ahora';
  if (mins < 60) return `hace ${mins} min`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `hace ${hours} h`;
  return `hace ${Math.floor(hours / 24)} d`;
}

export default function OpsNotificationsBell() {
  const { data: notifications } = useOperationalNotifications();
  const { data: unreadCount } = useNotificationsUnreadCount();
  const markSeen = useMarkNotificationsSeen();
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const navigate = useNavigate();
  const count = notifications?.length ?? 0;

  const handleOpen = (e: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(e.currentTarget);
    if ((unreadCount ?? 0) > 0) markSeen.mutate();
  };

  return (
    <>
      <IconButton onClick={handleOpen} sx={{ color: 'text.primary' }}>
        <Badge badgeContent={unreadCount ?? 0} color="error" max={99}>
          <NotificationsIcon />
        </Badge>
      </IconButton>

      <Popover
        open={!!anchorEl}
        anchorEl={anchorEl}
        onClose={() => setAnchorEl(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        <Box sx={{ width: { xs: 'calc(100vw - 32px)', sm: 340 }, maxWidth: 340, maxHeight: 420, overflow: 'auto' }}>
          <Box sx={{ p: 1.5 }}>
            <Typography variant="subtitle2" fontWeight={700}>Notificaciones operativas</Typography>
          </Box>
          <Divider />
          {count === 0 ? (
            <Box sx={{ p: 3, textAlign: 'center' }}>
              <Typography variant="body2" color="text.secondary">Sin novedades</Typography>
            </Box>
          ) : (
            <List disablePadding>
              {notifications!.map((n, i) => (
                <ListItemButton
                  key={i}
                  onClick={() => {
                    setAnchorEl(null);
                    navigate(n.type === 'LowStock' ? '/dashboard/ops/inventory' : '/dashboard/ops/inventory');
                  }}
                  sx={{ alignItems: 'flex-start', borderBottom: '1px solid', borderColor: 'divider' }}
                >
                  <ListItemIcon sx={{ minWidth: 32, mt: 0.5 }}>{TYPE_ICON[n.type]}</ListItemIcon>
                  <ListItemText
                    primary={n.title}
                    secondary={<>{n.message}<br /><Typography component="span" variant="caption" color="text.disabled">{timeAgo(n.date)}</Typography></>}
                    primaryTypographyProps={{ variant: 'body2', fontWeight: 600 }}
                  />
                </ListItemButton>
              ))}
            </List>
          )}
        </Box>
      </Popover>
    </>
  );
}
