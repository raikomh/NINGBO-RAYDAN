import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

// Destino del proxy de /api y /hubs en desarrollo. Por defecto: API de producción (Render).
// Para trabajar contra el backend local, define VITE_DEV_API_TARGET=http://localhost:62560 en .env.local.
const DEFAULT_API_TARGET = 'https://businesssearcher-api.onrender.com';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const apiTarget = env.VITE_DEV_API_TARGET || DEFAULT_API_TARGET;
  const secure = apiTarget.startsWith('https://');

  return {
    plugins: [react()],
    resolve: {
      alias: {
        '@': path.resolve(__dirname, './src'),
      },
    },
    build: {
      rollupOptions: {
        output: {
          manualChunks: {
            'react-vendor': ['react', 'react-dom', 'react-router-dom'],
            'mui-vendor': ['@mui/material', '@mui/icons-material', '@emotion/react', '@emotion/styled'],
            'leaflet-vendor': ['leaflet', 'react-leaflet'],
            'recharts-vendor': ['recharts'],
          },
        },
      },
    },
    server: {
      port: 4321,
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true,
          secure,
        },
        '/hubs': {
          target: apiTarget,
          changeOrigin: true,
          secure,
          ws: true,
        },
      },
    },
  };
});
