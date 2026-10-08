import { useState } from 'react';
import { useNavigate, Link as RouterLink } from 'react-router-dom';
import { Box, Typography, Button, Alert, CircularProgress, Link } from '@mui/material';
import { Lock, Refresh, Logout as LogoutIcon } from '@mui/icons-material';
import { api } from '@/lib/apiClient';
import { useAuth } from '@/context/AuthContext';
import { useSyncPush } from '@/hooks/useOps';
import { isLocalDeployment } from '@/lib/deployment';
import ChatWidget from '@/components/chat/ChatWidget';

// Pantalla dedicada a la que SubscriptionGate manda a cualquier dueño/trabajador bloqueado por
// suscripción vencida, en vez de dejar montar el dashboard completo: así ningún widget de fondo
// (campanita de notificaciones, reportes, etc.) dispara llamadas bloqueadas que reinicien el
// bucle de logout forzado (ver AuthContext.onSubscriptionExpired).
export default function SubscriptionBlockedPage() {
  const { user, updateUser, logout } = useAuth();
  const navigate = useNavigate();
  const push = useSyncPush();
  const [checking, setChecking] = useState(false);
  const [stillBlocked, setStillBlocked] = useState(false);
  const [checkError, setCheckError] = useState<string | null>(null);

  const checkStatus = async () => {
    setChecking(true);
    setStillBlocked(false);
    setCheckError(null);
    try {
      if (isLocalDeployment) {
        // El push sube lo pendiente y, como efecto secundario best-effort, refresca el estado de
        // suscripción tomando el snapshot del backend online (ver TriggerTenantSyncCommandHandler
        // -> RefreshTenantSubscriptionFromOnlineCommand). Si falla (nunca conectada, sin red),
        // igual seguimos y comprobamos el perfil tal cual está.
        await push.mutateAsync().catch(() => null);
      }
      const res = await api.get('/api/v1/auth/profile');
      const profile = res.data?.data;
      if (profile?.isSubscriptionActive) {
        updateUser({ ...(user as NonNullable<typeof user>), ...profile });
        navigate('/dashboard', { replace: true });
        return;
      }
      setStillBlocked(true);
    } catch {
      setCheckError('No se pudo comprobar el estado. Revisa tu conexión e inténtalo de nuevo.');
    } finally {
      setChecking(false);
    }
  };

  return (
    <Box
      display="flex" flexDirection="column" alignItems="center" justifyContent="center"
      minHeight="100vh" gap={3} px={2} textAlign="center"
    >
      <Lock sx={{ fontSize: 64, color: 'error.main' }} />
      <Typography variant="h5" fontWeight={700}>Suscripción vencida</Typography>
      <Typography variant="body1" color="text.secondary" maxWidth={480}>
        {isLocalDeployment
          ? 'Esta instalación está bloqueada por falta de pago. Paga en línea desde tu cuenta y toca el botón para comprobar el estado: se desbloquea sola en cuanto se confirme el pago.'
          : 'Tu cuenta está bloqueada por falta de pago. Escríbenos por el chat (esquina inferior) para coordinar el pago, o toca el botón para comprobar si ya se regularizó.'}
      </Typography>

      {stillBlocked && (
        <Alert severity="warning" sx={{ maxWidth: 480 }}>
          Sigue apareciendo vencida.{' '}
          {isLocalDeployment
            ? 'Si ya pagaste, espera unos segundos e inténtalo de nuevo.'
            : 'Si ya coordinaste el pago, espera a que el administrador lo registre.'}
        </Alert>
      )}
      {checkError && <Alert severity="error" sx={{ maxWidth: 480 }}>{checkError}</Alert>}

      <Button
        variant="contained" size="large"
        startIcon={checking ? <CircularProgress size={18} color="inherit" /> : <Refresh />}
        onClick={checkStatus} disabled={checking}
      >
        Comprobar estado
      </Button>

      {isLocalDeployment && (
        <Link component={RouterLink} to="/dashboard/ops/sync" underline="hover" fontWeight={600}>
          Ir a Sincronización
        </Link>
      )}

      <Button variant="text" color="inherit" startIcon={<LogoutIcon />} onClick={() => logout()}>
        Cerrar sesión
      </Button>

      {!isLocalDeployment && <ChatWidget />}
    </Box>
  );
}
