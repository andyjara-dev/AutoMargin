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

## 3b. Fuentes de mercado (opcional)

La pantalla **Mercado** busca avisos comparables en portales de venta de autos. Todo lo de esta
sección es opcional: sin encender nada, la pantalla funciona igual y la búsqueda informa qué
fuente está apagada, en vez de mostrar cero resultados sin explicación. El pegado manual de
avisos no depende de ninguna credencial ni de ninguna fuente.

**Ninguna fuente necesita credenciales.** Las dos leen el HTML público del portal.

| Fuente | Estado |
|---|---|
| **MercadoLibre** | Funciona. Lee `autos.mercadolibre.cl/{marca}/{modelo}/usados/`, que su `robots.txt` no prohíbe para un cliente genérico. Se activa con `ML_ENABLED=true`. |
| **Yapo** | Funciona. Lee su página de resultados, que su `robots.txt` permite, ordenada por publicación reciente. Se activa con `YAPO_ENABLED=true`. |
| **Chileautos** | No se integra. Su `robots.txt` prohíbe la lectura automatizada de las rutas necesarias, y el sistema no las pide. Para esos avisos se usa el pegado manual. |

Las dos son lectura de HTML, así que **se rompen cuando el portal cambia de maquetado**. Por eso
vienen apagadas: encenderlas es una decisión, no un valor por defecto. Y cuando dejan de entender
la página lo dicen con todas sus letras en vez de devolver cero avisos, que se confundiría con
«no hay autos de ese modelo».

Las consultas salen con un agente que se identifica y da un contacto, con un mínimo de segundos
entre peticiones a un mismo sitio (`MARKET_MIN_SECONDS`, por defecto 3) y un tope bajo de
resultados. Bajar ese intervalo es pedir un bloqueo.

### Por qué MercadoLibre no usa su API oficial

Se probó primero y no sirve. Su API no ofrece búsqueda abierta del marketplace:
[Ítems y Búsquedas](https://developers.mercadolibre.cl/es_ar/items-y-busquedas) solo documenta
el endpoint de búsqueda por sitio acotado a un vendedor (`seller_id` o `nickname`). Sin ese
parámetro responde `403`, y para comparar precios hacen falta avisos de muchos vendedores.

> Verificado el 14-08-2026 con credenciales válidas: el token se emite sin problema y la búsqueda
> devuelve `{"message":"forbidden","error":"forbidden","status":403,"cause":[]}`. El `cause` vacío
> es la negativa genérica; cuando falta un scope concreto, MercadoLibre lo nombra ahí. Sin token
> la respuesta es idéntica. No hay permiso del panel de desarrollador que lo destrabe.
>
> Su guía de vehículos es enteramente para vendedores —publicar, paquetes, leads, créditos
> preaprobados—, y la página «Localiza vehículos» trata de IDs de ubicación para publicar, no de
> buscar.
>
> Queda una vía teórica que **no conviene usar**: consultar vendedor por vendedor con
> `/users/{user_id}/items/search` sobre un puñado de automotoras conocidas. Daría una muestra
> sesgada hacia precios de automotora, que son más altos que los de particular. Eso inflaría el
> valor de mercado y con él la puja máxima, que es justo el error que este sistema existe para
> evitar. Es preferible no tener la fuente que tenerla sesgada.

### Encenderlas

```bash
# Editar .env y dejar:
#   ML_ENABLED=true
#   YAPO_ENABLED=true
```

Los cambios se aplican recreando el contenedor, no reiniciándolo:

```bash
docker compose -f docker-compose.prod.yml up -d api
```

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

Usa **el mismo comando con el que instalaste**, ni más ni menos. Lo que decide es cómo está
conectado tu proxy, no que uses NPM o no:

```bash
cd /opt/automargin
git pull

# NPM en modo host, o Nginx del sistema
docker compose -f docker-compose.prod.yml up -d --build

# NPM en una red de Docker (solo si configuraste NPM_NETWORK en el paso 5a)
docker compose -f docker-compose.prod.yml -f docker-compose.npm.yml up -d --build
```

> Si agregas `-f docker-compose.npm.yml` sin tener esa red, verás
> `network nginxproxymanager_default declared as external, but could not be found` y los
> contenedores no se reemplazan. Quita el segundo `-f` y listo.

Las migraciones nuevas se aplican solas al arrancar. Los contenedores se reemplazan uno a uno,
así que la interrupción es de unos segundos.

El frontend se compila **dentro** de la imagen, así que `git pull` por sí solo no cambia nada de
lo que sirve nginx: hace falta el `--build`. Y una vez reconstruido, el navegador puede seguir
mostrando la versión anterior desde su caché — recarga con Ctrl+F5 antes de dar por fallido un
despliegue. Para saber si el problema está en el servidor o en el navegador, pregúntale al
contenedor por algo que solo exista en la versión nueva:

```bash
docker compose -f docker-compose.prod.yml exec web grep -rlo "mercado" /usr/share/nginx/html/ | head -3
```

Si devuelve archivos, el servidor ya está actualizado y lo que falta es limpiar la caché.

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
| El login responde 401 con las credenciales del `.env` | La cuenta no llegó a crearse. Ver abajo. |
| Un cambio en el `.env` no surte efecto | Se usó `restart` en vez de `up -d`. Las variables se fijan al crear el contenedor. |
| Una ruta inexistente devuelve 200 | Es correcto: Angular maneja el enrutado y cualquier ruta desconocida entrega el `index.html`. |
| `bind: address already in use` | El `PUBLIC_PORT` lo usa otro contenedor. Comprobar con `ss -ltnp` o `docker ps` y elegir otro. |
| NPM devuelve 502 | Si NPM corre en modo host, `Forward Hostname` debe ser `127.0.0.1`, no el nombre del contenedor. |

## El login responde 401 con las credenciales del `.env`

Significa que la cuenta de administrador **no llegó a crearse**. El log lo dice:

```bash
docker compose -f docker-compose.prod.yml logs api | grep -i -E "administrador|admin"
```

Las tres causas posibles:

| Mensaje en el log | Causa |
|---|---|
| `NO SE CREÓ EL ADMINISTRADOR` | La contraseña no cumple la política: mínimo 10 caracteres, con mayúscula, minúscula y dígito. |
| `No hay Seed:AdminPassword configurada` | El `.env` no tenía la variable en el primer arranque. |
| `Administrador creado: ...` | Sí se creó. Entonces la contraseña que estás usando no es la que había en ese momento. |

Ojo con dos caracteres en el `.env`: **`#` inicia un comentario** y trunca el valor, y **`$` se
interpreta como variable**. Si la contraseña los lleva, quedó guardada distinta de lo que crees.
Comprueba qué recibió realmente el contenedor:

```bash
docker compose -f docker-compose.prod.yml exec api printenv Seed__AdminPassword
```

### Recrear la cuenta

Corrige la contraseña en el `.env` y **recrea** el contenedor:

```bash
docker compose -f docker-compose.prod.yml up -d api
```

> Tiene que ser `up -d`, **no `restart`**. Un reinicio arranca el mismo contenedor con las
> variables que ya tenía: las del `.env` se fijan al crearlo, así que el cambio no se aplicaría
> y el error se repetiría idéntico.

Si la cuenta **sí llegó a crearse** antes con otra contraseña, hay que borrarla primero, porque
el sembrado no toca un usuario existente:

```bash
docker compose -f docker-compose.prod.yml exec -T postgres psql -U automargin automargin -c "delete from app_user;"
```

Eso solo borra la cuenta de acceso. Los vehículos, análisis e historial no se tocan.

## Notas de seguridad

- El `.env` es el único lugar con secretos. No se versiona y debe quedar en `chmod 600`.
- PostgreSQL no publica puertos: solo se alcanza desde la red interna de Docker.
- La API corre como usuario sin privilegios dentro del contenedor.
- Los datos de demostración **no** se pueden cargar en producción: el endpoint verifica el entorno
  y responde 403.
- Swagger solo se expone en desarrollo.
