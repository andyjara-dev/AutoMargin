# Despliegue en servidor propio

Tres contenedores: PostgreSQL, la API y un Nginx que sirve el frontend y reenvía `/api` a la API.

Ni la base ni la API quedan accesibles desde fuera: el proxy del servidor es el único punto de
entrada y se encarga del dominio y del certificado.

Con **Nginx Proxy Manager**, que corre en Docker, el tráfico ni siquiera sale de Docker:

```
internet → NPM (TLS) → [red de Docker] → automargin-web ─┬→ archivos de Angular
                                                         └→ /api → api → postgres
```

Con **Nginx instalado en el sistema**, se publica un puerto solo en la interfaz local:

```
internet → Nginx (TLS) → 127.0.0.1:8080 → contenedor web ─┬→ archivos de Angular
                                                          └→ /api → api → postgres
```

Como el frontend y la API viajan por el mismo origen, **no hay CORS** y el mismo build sirve para
cualquier dominio sin recompilar.

---

## 1. Requisitos del servidor

```bash
docker --version          # 24 o superior
docker compose version    # v2
```

Compilar Angular necesita alrededor de **2 GB de RAM**. Si el servidor tiene menos, añade swap
antes de construir:

```bash
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile
```

## 2. Clonar el repositorio

```bash
sudo mkdir -p /opt/automargin && sudo chown $USER:$USER /opt/automargin
git clone https://github.com/andyjara-dev/AutoMargin.git /opt/automargin
cd /opt/automargin
```

## 3. Configurar los secretos

```bash
cp .env.prod.example .env
```

Genera las claves en el servidor, sin escribirlas a mano ni pasarlas por chat. Se sustituyen en
su línea en vez de añadirse al final, para no dejar la clave dos veces en el archivo:

```bash
sed -i "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=$(openssl rand -base64 24)|" .env
```

```bash
sed -i "s|^JWT_SIGNING_KEY=.*|JWT_SIGNING_KEY=$(openssl rand -base64 48)|" .env
```

Edita `.env` y define `ADMIN_EMAIL`, `ADMIN_PASSWORD` y `PUBLIC_URL`. Después:

```bash
chmod 600 .env
```

> **Define los secretos antes del primer arranque.** PostgreSQL fija su contraseña cuando crea
> el directorio de datos y no vuelve a leerla: si cambias `POSTGRES_PASSWORD` después, la base
> conserva la anterior y la API deja de conectarse. Recuperarlo obliga a borrar el volumen, y con
> él todos los datos.
>
> La contraseña del administrador también se usa solo la **primera vez**, cuando se crea la
> cuenta. Cambiarla después en el `.env` no tiene efecto: el sembrado no toca un usuario existente.

## 4. Levantar

```bash
docker compose -f docker-compose.prod.yml up -d --build
```

La primera vez tarda varios minutos: descarga las imágenes base y compila la API y el frontend.

Al arrancar, la API aplica las migraciones pendientes y siembra los datos base (roles,
administrador, parámetros por defecto, costos de reparación y catálogo de marcas).

```bash
docker compose -f docker-compose.prod.yml ps          # los tres deben estar healthy
curl -s http://127.0.0.1:8080/health                  # {"status":"ok","database":"connected"}
```

## 5a. Con Nginx Proxy Manager

Primero hay que saber **cómo está conectado NPM**, porque de eso depende cómo lo alcanza:

```bash
docker inspect nginx-proxy-manager --format '{{.HostConfig.NetworkMode}}'
```

### Si responde `host`

NPM comparte la red del servidor y no resuelve nombres de contenedor. Se le apunta al puerto
local, que es lo que ya publica el compose base.

Comprueba antes que el puerto esté libre:

```bash
ss -ltnp | grep 8090       # sin salida significa que está libre
docker compose -f docker-compose.prod.yml up -d --build
```

En NPM → **Proxy Hosts** → **Add Proxy Host**, con `Forward Hostname` = `127.0.0.1` y
`Forward Port` = el valor de `PUBLIC_PORT`.

### Si responde `bridge` o el nombre de una red

NPM vive en una red de Docker y puede hablar con el contenedor directamente, sin pasar por el
host. Es preferible: el tráfico no sale de Docker y no hace falta exponer ningún puerto.

```bash
docker inspect nginx-proxy-manager --format '{{range $k,$v := .NetworkSettings.Networks}}{{$k}}{{end}}'
```

```bash
sed -i "s|^PUBLIC_PORT=.*|&\nNPM_NETWORK=LA_RED_QUE_DEVOLVIO|" .env
```

```bash
docker compose -f docker-compose.prod.yml -f docker-compose.npm.yml up -d --build
```

En NPM, `Forward Hostname` = `automargin-web` y `Forward Port` = `80`.

> Debe ser `automargin-web`, no `web`: es un alias definido a propósito, porque «web» podría
> colisionar con otro stack en la misma red.

### El resto de la configuración, en ambos casos

| Pestaña | Campo | Valor |
|---|---|---|
| Details | Domain Names | `automargin.andyjara.dev` |
| Details | Scheme | `http` |
| Details | Block Common Exploits | activado |
| Details | Websockets Support | desactivado (no se usan) |
| SSL | SSL Certificate | Request a new SSL Certificate |
| SSL | Force SSL | activado |
| SSL | HTTP/2 Support | activado |

En **Advanced**, para cuando lleguen las fotografías en la Fase 2:

```nginx
client_max_body_size 20m;
```

NPM ya envía `X-Forwarded-Proto`, `X-Forwarded-For` y `Host`, que es justo lo que la API necesita
para saber que el cliente llegó por HTTPS. No hay que configurar nada más.

Con NPM puedes saltarte los pasos 5b y 6.

## 5b. Con Nginx instalado en el sistema

Solo si **no** usas Nginx Proxy Manager. Crea `/etc/nginx/sites-available/automargin`:

```nginx
server {
    listen 80;
    server_name automargin.andyjara.dev;

    # Certbot resuelve el desafío aquí; el resto va a HTTPS.
    location /.well-known/acme-challenge/ { root /var/www/html; }
    location / { return 301 https://$host$request_uri; }
}

server {
    listen 443 ssl http2;
    server_name automargin.andyjara.dev;

    ssl_certificate     /etc/letsencrypt/live/automargin.andyjara.dev/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/automargin.andyjara.dev/privkey.pem;

    # Subir archivos no está en el MVP, pero llegará con el análisis de fotografías.
    client_max_body_size 20m;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;

        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        # Imprescindible: sin esta cabecera la API cree que la petición llegó por HTTP.
        proxy_set_header X-Forwarded-Proto $scheme;

        proxy_read_timeout 60s;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/automargin /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

## 6. Certificado

Apunta primero el registro DNS `automargin.andyjara.dev` a la IP del servidor, y luego:

```bash
sudo certbot --nginx -d automargin.andyjara.dev
```

## 7. Entrar

Abre `https://automargin.andyjara.dev` e inicia sesión con el `ADMIN_EMAIL` y `ADMIN_PASSWORD`
del `.env`.

**Cambia esa contraseña apenas entres.** Estuvo en un archivo de texto del servidor.

---

## Actualizar

```bash
cd /opt/automargin
git pull

# Con Nginx Proxy Manager
docker compose -f docker-compose.prod.yml -f docker-compose.npm.yml up -d --build

# Con Nginx del sistema
docker compose -f docker-compose.prod.yml up -d --build
```

Las migraciones nuevas se aplican solas al arrancar. Los contenedores se reemplazan uno a uno,
así que la interrupción es de unos segundos.

## Respaldos

La base vive en un volumen de Docker, que sobrevive a los despliegues pero **no** a un
`docker compose down -v`.

```bash
# Respaldo
docker compose -f docker-compose.prod.yml exec -T postgres \
  pg_dump -U automargin automargin | gzip > respaldo-$(date +%F).sql.gz

# Restauración
gunzip -c respaldo-2026-08-14.sql.gz | \
  docker compose -f docker-compose.prod.yml exec -T postgres psql -U automargin automargin
```

Conviene automatizarlo con cron. Un negocio que decide compras con estos datos no puede permitirse
perder el historial de análisis: sin él no hay con qué calibrar nada.

```bash
0 3 * * * cd /opt/automargin && docker compose -f docker-compose.prod.yml exec -T postgres pg_dump -U automargin automargin | gzip > /var/backups/automargin-$(date +\%F).sql.gz
```

## Diagnóstico

```bash
docker compose -f docker-compose.prod.yml logs -f api      # log de la API
docker compose -f docker-compose.prod.yml logs -f web      # accesos de Nginx
docker compose -f docker-compose.prod.yml ps               # estado de salud
```

| Síntoma | Causa habitual |
|---|---|
| La API no arranca | Falta `JWT_SIGNING_KEY` o tiene menos de 32 caracteres. Es deliberado: firmar tokens con una clave conocida sería peor que no arrancar. |
| `/health` responde 503 | La base no responde. Revisa el log de `postgres`. |
| La web carga pero todo da error | El Nginx del servidor no está pasando `X-Forwarded-Proto`. |
| Bucle de redirecciones | Lo mismo, o quedó activo `ForceHttpsRedirect` con el proxy ya terminando TLS. |
| Sesión que se cierra sola | Cambió `JWT_SIGNING_KEY` entre despliegues: invalida todos los tokens emitidos. |
| `password authentication failed` | Se cambió `POSTGRES_PASSWORD` después del primer arranque. La base conserva la original. |
| Una ruta inexistente devuelve 200 | Es correcto: Angular maneja el enrutado y cualquier ruta desconocida entrega el `index.html`. |
| `bind: address already in use` | El `PUBLIC_PORT` lo usa otro contenedor. Comprobar con `ss -ltnp` o `docker ps` y elegir otro. |
| NPM devuelve 502 | Si NPM corre en modo host, `Forward Hostname` debe ser `127.0.0.1`, no el nombre del contenedor. |

## Notas de seguridad

- El `.env` es el único lugar con secretos. No se versiona y debe quedar en `chmod 600`.
- PostgreSQL no publica puertos: solo se alcanza desde la red interna de Docker.
- La API corre como usuario sin privilegios dentro del contenedor.
- Los datos de demostración **no** se pueden cargar en producción: el endpoint verifica el entorno
  y responde 403.
- Swagger solo se expone en desarrollo.
