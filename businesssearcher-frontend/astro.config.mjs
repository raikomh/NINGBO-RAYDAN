import { defineConfig } from 'astro/config';
import react from '@astrojs/react';

export default defineConfig({
  integrations: [react()],
  output: 'static',
  server: {
    port: 4321
  },
  vite: {
    server: {
      proxy: {
        // Durante desarrollo, las llamadas a /api van directo al backend .NET
        // sin problemas de CORS. En producción, configura VITE_API_URL en .env
        '/api': {
          target: process.env.VITE_API_PROXY_TARGET || 'http://localhost:5000',
          changeOrigin: true
        },
        '/hubs': {
          target: process.env.VITE_API_PROXY_TARGET || 'http://localhost:5000',
          changeOrigin: true,
          ws: true
        }
      }
    }
  }
});
