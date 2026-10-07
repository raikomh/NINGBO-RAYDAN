# BusinessSearcher — Frontend

Aplicación web de BusinessSearcher construida con **Astro + React + Material UI**.
Landing page estática y ultrarrápida + dashboard tipo SPA para gestionar tiendas,
productos, horarios y suscripción — todo conectado al backend .NET vía API REST
y SignalR para disponibilidad en tiempo real.

## Identidad visual

El diseño se aleja del look genérico de Material UI por defecto. Paleta cálida
inspirada en mercados y fondas (carbón + parchment + llama + verde fresco),
tipografía Cabinet Grotesk + Inter + IBM Plex Mono, y un elemento de marca
propio — el **"pulse chip"** (punto que palpita + estado) — que cuenta la
historia del producto en el marketing y es, literalmente, el mismo control que
usan los dueños de negocio para marcar disponibilidad en el dashboard real.

## Stack

- **Astro 4** — páginas estáticas (landing, login, registro) para máxima velocidad
- **React 18** — interactividad vía islands de Astro
- **Material UI v5** — componentes, con tema 100% personalizado
- **React Router v6** — navegación interna del dashboard (SPA)
- **TanStack Query v5** — fetching, caché y mutaciones contra la API
- **React Hook Form + Zod** — formularios y validación
- **Axios** — cliente HTTP con refresh-token automático
- **@microsoft/signalr** — disponibilidad de productos en tiempo real
- **Recharts** — gráficos (uso futuro en reportes)

## Requisitos previos

- Node.js 18+
- El backend .NET de BusinessSearcher corriendo (ver su propio README)

## Instalación

```bash
npm install
cp .env.example .env
```

Edita `.env` con la URL real de tu backend:

```env
PUBLIC_API_URL=http://localhost:5000/api/v1
PUBLIC_HUB_URL=http://localhost:5000/hubs/availability
VITE_API_PROXY_TARGET=http://localhost:5000
```

### ⚠️ Importante: configura CORS en el backend

El backend necesita permitir el origen de este frontend. En
`BusinessSearcher.WebApi/appsettings.Development.json`, agrega el puerto de
Astro (4321 por defecto) a `Cors:AllowedOrigins`:

```json
"Cors": {
  "AllowedOrigins": ["http://localhost:4321", "http://localhost:3000"]
}
```

## Desarrollo

```bash
npm run dev
```

Abre `http://localhost:4321`. El proxy de Vite reenvía `/api` y `/hubs` al
backend automáticamente en desarrollo, así que normalmente no tendrás
problemas de CORS incluso si `PUBLIC_API_URL` apunta a una ruta relativa.

## Build de producción

```bash
npm run build
npm run preview   # para probar el build localmente
```

El sitio se genera 100% estático en `dist/`. El dashboard (`/dashboard/*`) es
un SPA de React que se hidrata en el navegador — por eso el proyecto incluye
reglas de redirección para que las rutas profundas (ej.
`/dashboard/stores/abc123`) funcionen al recargar la página:

- `public/_redirects` → para Netlify
- `vercel.json` → para Vercel

Si despliegas en otro hosting estático, necesitarás una regla equivalente:
**cualquier petición a `/dashboard/*` debe servir `/dashboard/index.html`**.

## Estructura del proyecto

```
src/
├── components/
│   ├── marketing/     ← Landing page (Hero, Features, Pricing, LiveBoard)
│   ├── auth/          ← Login, Registro, Recuperar/Restablecer contraseña
│   ├── dashboard/      ← SPA del panel de control
│   │   ├── pages/      ← Overview, Stores, StoreDetail (tabs), Billing, Account
│   │   └── dialogs/    ← Crear/editar Tienda, Producto, Categoría
│   └── shared/         ← PulseChip (signature), Logo, EmptyState, StatCard, Toast
├── context/
│   └── AuthContext.tsx ← Estado global de sesión
├── hooks/
│   ├── useStores.ts    ← React Query: CRUD de tiendas
│   └── useProducts.ts  ← React Query: productos, categorías, toggle disponibilidad
├── lib/
│   ├── apiClient.ts     ← Axios + refresh token automático (cookie httpOnly)
│   ├── signalr.ts       ← Conexión al hub de disponibilidad
│   └── types.ts         ← Tipos espejo de los DTOs del backend
├── theme/
│   └── theme.ts         ← Tema de Material UI personalizado
├── layouts/             ← BaseLayout, AuthLayout (Astro)
└── pages/               ← Rutas de Astro (index, login, register, dashboard/...)
```

## Cómo funciona la autenticación

1. El **access token (JWT)** vive solo en memoria (nunca en localStorage) para
   protegerlo de ataques XSS.
2. El **refresh token** vive en una cookie `httpOnly` que pone el backend —
   el navegador la envía solo, el JS nunca puede leerla.
3. Al cargar la app, `bootstrapSession()` intenta renovar la sesión llamando a
   `/auth/refresh-token` usando esa cookie.
4. Si una petición a la API responde `401`, el interceptor de axios intenta
   refrescar el token una vez y reintenta la petición original.

## Notas sobre el dashboard en tiempo real

El hook `useToggleProductAvailability` hace una **actualización optimista**:
el switch responde al instante en la UI mientras la petición viaja al
backend. Si falla, se revierte automáticamente. El hub de SignalR
(`src/lib/signalr.ts`) está listo para conectarse y escuchar cambios de
disponibilidad — actualmente expuesto como utilidad; conéctalo en
`ProductsTab` o `OverviewPage` si quieres reflejar cambios hechos desde otra
pestaña/dispositivo en tiempo real.

## Pendiente para llevar a producción

- [ ] Conectar el hook de SignalR en la UI (actualmente solo expuesto, no usado)
- [ ] Página pública de búsqueda para clientes finales (`/buscar`) — actualmente
      solo existe el backend (`GET /api/v1/search`), falta la UI pública
- [ ] Tests (Vitest + React Testing Library)
- [ ] Manejo de error 403 (plan vencido / suspendido) con pantalla dedicada
- [ ] Internacionalización si planeas expandirte fuera de Colombia
