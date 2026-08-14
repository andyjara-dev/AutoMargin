# Imagen del frontend: compila Angular y lo sirve con Nginx, que además hace de proxy a la API.
#   docker build -f frontend/remates-web/Dockerfile frontend/remates-web

# ---------- Compilación ----------
FROM node:22-alpine AS build
WORKDIR /app

# Primero el manifiesto: mientras las dependencias no cambien, Docker reutiliza la capa
# del npm ci, que es lo que más tarda.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# La configuración de producción sustituye environment.ts por environment.prod.ts, que deja
# la URL de la API vacía para que las peticiones salgan al mismo origen.
RUN npm run build -- --configuration production

# ---------- Ejecución ----------
FROM nginx:1.27-alpine AS runtime

RUN rm -f /etc/nginx/conf.d/default.conf
COPY nginx.conf /etc/nginx/conf.d/automargin.conf
COPY --from=build /app/dist/remates-web/browser /usr/share/nginx/html

EXPOSE 80

# Contra 127.0.0.1 y no localhost: dentro del contenedor, localhost resuelve primero a IPv6
# y Nginx escucha solo en IPv4, así que el chequeo fallaría siempre.
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD ["/bin/sh", "-c", "wget -q --spider http://127.0.0.1/ || exit 1"]
