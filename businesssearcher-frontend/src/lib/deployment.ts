/**
 * Modo de despliegue de este build. El mismo código sirve a los dos despliegues:
 *
 *  - `online`: la web pública (Cloudflare Workers) contra el backend de Render. Ahí el backend
 *    es el servidor de la sincronización: solo expone la clave, ingest y export. No existen
 *    push/pull/connect, así que esos botones no deben mostrarse.
 *  - `local`: la instalación en el negocio (imagen Docker). Es el cliente: se empareja, sube y
 *    baja datos.
 *
 * Se fija en tiempo de build con VITE_DEPLOYMENT_MODE; sin variable se asume `online`, que es
 * el despliegue público y el que más builds tiene.
 */
export type DeploymentMode = 'online' | 'local';

export const DEPLOYMENT_MODE: DeploymentMode =
  import.meta.env.VITE_DEPLOYMENT_MODE === 'local' ? 'local' : 'online';

export const isLocalDeployment = DEPLOYMENT_MODE === 'local';
export const isOnlineDeployment = DEPLOYMENT_MODE === 'online';
