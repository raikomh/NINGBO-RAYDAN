import { Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '@/context/AuthContext';
import { isLocalDeployment } from '@/lib/deployment';
import SubscriptionBlockedPage from './SubscriptionBlockedPage';

// En Local, Sincronización es la única vía de autoservicio para reactivar (pagar en línea y
// sincronizar), así que se deja pasar aunque la suscripción esté vencida. En Online no hace
// falta ninguna excepción: el chat de soporte va embebido en la propia SubscriptionBlockedPage.
const EXEMPT_PATHS = ['/dashboard/ops/sync'];

export default function SubscriptionGate() {
  const { user } = useAuth();
  const { pathname } = useLocation();

  const isBlocked =
    user?.role !== 'admin' && !user?.isOpsUser && user?.isSubscriptionActive === false;
  const isExempt = isLocalDeployment && EXEMPT_PATHS.includes(pathname);

  if (isBlocked && !isExempt) {
    return <SubscriptionBlockedPage />;
  }
  return <Outlet />;
}
