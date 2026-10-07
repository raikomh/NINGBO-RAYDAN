import axios from 'axios';
import { mockAdapter } from './mockApi';

// En producción, si no se define VITE_API_URL en el build, se apunta al backend
// desplegado; en dev se deja vacío para que las llamadas /api pasen por el proxy de Vite.
const BASE_URL =
  import.meta.env.VITE_API_URL ||
  (import.meta.env.PROD ? 'https://businesssearcher-api.onrender.com' : '');
const MOCK = import.meta.env.VITE_MOCK === 'true';

export const api = axios.create({
  baseURL: BASE_URL,
  withCredentials: true,
  ...(MOCK && { adapter: mockAdapter }),
});

// Instance without interceptors — used only for the refresh-token call
// to avoid the recursive 401 deadlock when the refresh itself returns 401.
const rawApi = axios.create({
  baseURL: BASE_URL,
  withCredentials: true,
  ...(MOCK && { adapter: mockAdapter }),
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Los 401 de estos endpoints son credenciales incorrectas, no una sesión caducada:
// no deben disparar el refresh ni la redirección a /login (perderían el mensaje de error).
const AUTH_ENTRY_PATHS = ['/api/v1/auth/login', '/api/v1/ops/auth/login', '/api/v1/client/auth/login'];

let isRefreshing = false;
let failedQueue: Array<{
  resolve: (value: unknown) => void;
  reject: (reason?: unknown) => void;
}> = [];

const processQueue = (error: unknown, token: string | null = null) => {
  failedQueue.forEach((p) => {
    if (error) p.reject(error);
    else p.resolve(token);
  });
  failedQueue = [];
};

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const originalRequest = error.config;

    const isAuthEntry = AUTH_ENTRY_PATHS.some((p) => originalRequest.url?.includes(p));

    if (error.response?.status === 401 && !originalRequest._retry && !isAuthEntry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        }).then((token) => {
          originalRequest.headers.Authorization = `Bearer ${token}`;
          return api(originalRequest);
        });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      try {
        const res = await rawApi.post('/api/v1/auth/refresh-token');
        const token: string = res.data?.data?.token ?? res.data?.token;
        if (token) {
          localStorage.setItem('token', token);
          processQueue(null, token);
          originalRequest.headers.Authorization = `Bearer ${token}`;
          return api(originalRequest);
        }
        throw new Error('No token in refresh response');
      } catch (err) {
        processQueue(err, null);
        localStorage.removeItem('token');
        window.location.href = '/login';
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  }
);
