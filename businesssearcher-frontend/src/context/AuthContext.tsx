import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { api } from '@/lib/apiClient';
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

  const fetchProfile = useCallback(async () => {
    try {
      const res = await api.get('/api/v1/auth/profile');
      setUser(withRole(res.data.data));
    } catch {
      localStorage.removeItem('token');
      setToken(null);
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (token) {
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
    setToken(newToken);
    const profileRes = await api.get('/api/v1/auth/profile');
    setUser(withRole(profileRes.data.data));
  };

  // Usado tras el registro: el backend ya devuelve un JWT en la respuesta
  // (el login normal exige email verificado, el token de registro no).
  const loginWithToken = async (newToken: string) => {
    localStorage.setItem('token', newToken);
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
