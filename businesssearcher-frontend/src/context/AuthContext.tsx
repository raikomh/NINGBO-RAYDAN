import React, { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { api, setSubscriptionExpiredHandler } from '@/lib/apiClient';
import type { User } from '@/lib/types';

interface AuthContextValue {
  user: User | null;
  token: string | null;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  loginWithToken: (token: string) => Promise<void>;
  loginOps: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  updateUser: (user: User) => void;
}

// Clave donde persistimos la sesión de sub-usuario (TPV). El endpoint /auth/profile
// solo entiende de dueños de negocio, así que para empleados hidratamos desde aquí.
const OPS_SESSION_KEY = 'opsSession';

// Mensaje que AuthPage muestra tras un logout forzado por suscripción vencida
// (lo lee y lo borra al montar).
export const AUTH_NOTICE_KEY = 'authNotice';
const SUBSCRIPTION_EXPIRED_NOTICE =
  'Tu suscripción venció. Inicia sesión de nuevo para ver el estado de tu cuenta y regularizar el pago.';

const AuthContext = createContext<AuthContextValue | null>(null);

// El backend identifica al admin por email (config Chat:AdminEmail).
// Esto solo controla la visibilidad del menú; el backend valida con 403 igualmente.
const ADMIN_EMAIL = import.meta.env.VITE_ADMIN_EMAIL || 'admin@businesssearcher.dev';

function withRole(profile: User | null): User | null {
  if (!profile) return null;
  const role = profile.email?.toLowerCase() === ADMIN_EMAIL.toLowerCase() ? 'admin' : 'tenant';
  return { ...profile, role };
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(() => localStorage.getItem('token'));
  const [isLoading, setIsLoading] = useState(true);

  // login()/loginWithToken() ya hacen su propio fetch de perfil y setUser (a propósito SIN el
  // chequeo de suscripción vencida, ver comentario en forceLogoutForExpiredSubscription). Como
  // ambos también llaman a setToken(), disparan el useEffect de abajo, que haría un SEGUNDO fetch
  // de perfil en paralelo — y ese sí aplica el chequeo y puede forzar logout. Sin esta bandera,
  // los dos fetches compiten: si el del efecto responde después, deshace el login recién hecho y
  // una cuenta bloqueada por pago nunca llega a ver SubscriptionBlockedPage.
  const justAuthenticatedRef = useRef(false);

  // Cierra la sesión por suscripción vencida: NO se usa dentro de login() a propósito — si
  // lo hiciera, un dueño bloqueado nunca podría volver a entrar para llegar a Sincronización o
  // al chat de soporte (las únicas vías para regularizar el pago), quedando en deadlock. Solo
  // se aplica a una sesión que YA estaba abierta (al recargar o al recibir un 402 en cualquier
  // llamada): esa sesión se cierra y el dueño tiene que loguearse de nuevo para continuar, punto
  // en el que login() lo deja entrar igual aunque siga bloqueado, para ver el aviso y el banner.
  const forceLogoutForExpiredSubscription = useCallback(() => {
    localStorage.setItem(AUTH_NOTICE_KEY, SUBSCRIPTION_EXPIRED_NOTICE);
    localStorage.removeItem('token');
    localStorage.removeItem(OPS_SESSION_KEY);
    setToken(null);
    setUser(null);
  }, []);

  const fetchProfile = useCallback(async () => {
    try {
      const res = await api.get('/api/v1/auth/profile');
      const profile = withRole(res.data.data);
      if (profile && profile.role !== 'admin' && profile.isSubscriptionActive === false) {
        forceLogoutForExpiredSubscription();
        return;
      }
      setUser(profile);
    } catch {
      localStorage.removeItem('token');
      setToken(null);
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  }, [forceLogoutForExpiredSubscription]);

  // Un 402 SUBSCRIPTION_EXPIRED en una sesión ya abierta cierra la sesión de inmediato la PRIMERA
  // vez que se detecta (sorpresa: el dueño no sabía que estaba bloqueado). Pero si ya lo sabemos
  // (login() lo dejó entrar con isSubscriptionActive === false para ver el banner y llegar a
  // Sincronización), un 402 de cualquier llamada de fondo que igual sigue disparándose ahí (p.ej.
  // la campanita de notificaciones, que no está exenta del bloqueo) NO debe volver a expulsarlo:
  // si lo hiciera, nunca llegaría a Sincronización — quedaría en un bucle login→402→login.
  useEffect(() => {
    setSubscriptionExpiredHandler(() => {
      if (user?.isSubscriptionActive === false) return;
      forceLogoutForExpiredSubscription();
    });
    return () => setSubscriptionExpiredHandler(null);
  }, [forceLogoutForExpiredSubscription, user]);

  useEffect(() => {
    if (token) {
      if (justAuthenticatedRef.current) {
        // login()/loginWithToken() ya resolvieron el perfil y el estado: no repetir el fetch.
        justAuthenticatedRef.current = false;
        setIsLoading(false);
        return;
      }
      // Empleado (TPV): hidratar desde la sesión guardada, sin llamar a /auth/profile.
      const opsRaw = localStorage.getItem(OPS_SESSION_KEY);
      if (opsRaw) {
        try {
          setUser(JSON.parse(opsRaw) as User);
          setIsLoading(false);
          return;
        } catch {
          localStorage.removeItem(OPS_SESSION_KEY);
        }
      }
      fetchProfile();
    } else {
      setIsLoading(false);
    }
  }, [token, fetchProfile]);

  const login = async (email: string, password: string) => {
    const res = await api.post('/api/v1/auth/login', {
      email,
      password,
      ipAddress: null,
    });
    const { token: newToken } = res.data.data;
    localStorage.setItem('token', newToken);
    justAuthenticatedRef.current = true;
    setToken(newToken);
    const profileRes = await api.get('/api/v1/auth/profile');
    setUser(withRole(profileRes.data.data));
  };

  // Usado tras el registro: el backend ya devuelve un JWT en la respuesta
  // (el login normal exige email verificado, el token de registro no).
  const loginWithToken = async (newToken: string) => {
    localStorage.setItem('token', newToken);
    justAuthenticatedRef.current = true;
    setToken(newToken);
    const profileRes = await api.get('/api/v1/auth/profile');
    setUser(withRole(profileRes.data.data));
  };

  // Login de sub-usuario (empleado) del TPV. No usa /auth/profile: el perfil viene
  // en la respuesta y se persiste para sobrevivir a recargas de página.
  const loginOps = async (email: string, password: string) => {
    const res = await api.post('/api/v1/ops/auth/login', { email, password });
    const payload = res.data?.data ?? res.data;
    const newToken: string = payload.token;
    const opsUser = payload.user;
    const mapped: User = {
      id: opsUser.id,
      email: opsUser.email,
      name: opsUser.name,
      businessName: payload.businessName,
      plan: '',
      role: 'tenant',
      isOpsUser: true,
      opsRole: opsUser.role,
    };
    localStorage.setItem('token', newToken);
    localStorage.setItem(OPS_SESSION_KEY, JSON.stringify(mapped));
    setToken(newToken);
    setUser(mapped);
  };

  const logout = async () => {
    try {
      if (!user?.isOpsUser) await api.post('/api/v1/auth/logout');
    } finally {
      localStorage.removeItem('token');
      localStorage.removeItem(OPS_SESSION_KEY);
      setToken(null);
      setUser(null);
    }
  };

  const updateUser = (updated: User) => setUser(updated);

  return (
    <AuthContext.Provider value={{ user, token, isLoading, login, loginWithToken, loginOps, logout, updateUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
