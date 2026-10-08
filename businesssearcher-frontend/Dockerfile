# ── Stage 1: Build ───────────────────────────────────────────────────────────────
FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Variables VITE_* quedan incrustadas en el bundle en tiempo de build
ARG VITE_API_URL=http://localhost:5000
ARG VITE_MOCK=false
ARG VITE_ADMIN_EMAIL=admin@businesssearcher.dev
ARG VITE_GROQ_API_KEY=
ENV VITE_API_URL=$VITE_API_URL \
    VITE_MOCK=$VITE_MOCK \
    VITE_ADMIN_EMAIL=$VITE_ADMIN_EMAIL \
    VITE_GROQ_API_KEY=$VITE_GROQ_API_KEY

RUN npm run build

# ── Stage 2: Runtime (nginx sirviendo estáticos) ───────────────────────────────────
FROM nginx:alpine AS final

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://127.0.0.1:80/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
