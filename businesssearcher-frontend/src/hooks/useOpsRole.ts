import { useAuth } from '@/context/AuthContext';

export type OpsRoleName = 'Administrador' | 'Cajero' | 'JefeDeTurno' | 'Almacenero' | 'Comercial' | 'Auditor';

/** Rol operativo del usuario actual. El dueño del negocio (no es sub-usuario) es Administrador implícito. */
export function useOpsRole(): OpsRoleName {
  const { user } = useAuth();
  if (!user?.isOpsUser) return 'Administrador';
  return (user.opsRole as OpsRoleName) ?? 'Administrador';
}

export function useIsOpsAdmin(): boolean {
  return useOpsRole() === 'Administrador';
}

/** True si el rol actual está en la lista permitida, o si es Administrador (siempre puede todo). */
export function useHasOpsRole(...allowed: OpsRoleName[]): boolean {
  const role = useOpsRole();
  return role === 'Administrador' || allowed.includes(role);
}
