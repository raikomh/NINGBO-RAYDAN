import { api } from './apiClient';

const SESSION_FLAG = 'marketing_visit_sent';

/**
 * Si la URL trae ?src=nombre_grupo (ej. compartido en un grupo de Facebook), lo
 * registra una vez por sesión de pestaña. Fire-and-forget: nunca bloquea el render
 * ni rompe la carga de la app si el backend no responde.
 */
export function captureMarketingSource(): void {
  const src = new URLSearchParams(window.location.search).get('src');
  if (!src) return;
  if (sessionStorage.getItem(SESSION_FLAG) === src) return;

  sessionStorage.setItem(SESSION_FLAG, src);
  api.post('/api/v1/track/visit', { source: src }).catch(() => {
    // No es crítico: si falla, simplemente no queda registrada esta visita.
  });
}
