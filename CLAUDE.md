# AutoMargin

Sistema para decidir **cuánto pujar** por un vehículo en un remate por deuda, y para seguir el
ciclo completo hasta la reventa. Negocio real en Chile, un solo operador.

Documentación funcional: [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md) ·
[docs/DESPLIEGUE.md](docs/DESPLIEGUE.md) · manual de usuario dentro de la app, en `/manual`.

---

## La regla que no se negocia

**Ningún modelo de lenguaje participa en un cálculo financiero.** Puja máxima, costos, utilidad,
ROI, márgenes, break-even, score, semáforo, gates y valuación son código determinístico que vive
en `Remates.Domain`, con tests que son su especificación ejecutable.

La IA puede proponer datos de entrada (leer una foto, parsear un aviso) o narrar un resultado ya
calculado. Nunca producir el número.

`Remates.Domain` **no tiene dependencias externas**. Si algo necesita EF, HTTP o configuración,
no va ahí.

---

## Estructura

```
src/Remates.Domain/          motores puros: financiero, puja máxima, scoring, valuación, parser
src/Remates.Infrastructure/  EF Core + Npgsql, Identity, auditoría, fuentes de mercado
src/Remates.Api/             controllers, DTOs, servicios de aplicación
tests/Remates.Domain.Tests/  xUnit sobre los motores (143 al día de hoy)
tools/Remates.LogoTracer/    vectoriza el logo desde PNG y genera el favicon.ico
frontend/remates-web/        Angular 20 standalone + signals
```

## Comandos

```bash
dotnet build Remates.slnx                    # ojo: .slnx, no .sln
dotnet test Remates.slnx
dotnet run --project src/Remates.Api         # http://localhost:5044, Swagger en /swagger
```

```bash
cd frontend/remates-web && npm start         # http://localhost:4200
```

```bash
docker compose up -d                         # PostgreSQL local
```

Las migraciones se generan **desde Infrastructure como proyecto de arranque**, no desde la API:
el paquete `EntityFrameworkCore.Design` va con `PrivateAssets`, así que no llega a la API y la
herramienta se queja. Hay un `DesignTimeDbContextFactory` justamente para esto.

```bash
dotnet ef migrations add NombreDeLaMigracion \
  --project src/Remates.Infrastructure \
  --startup-project src/Remates.Infrastructure \
  --output-dir Persistence/Migrations
```

Se aplican solas al arrancar la API.

Antes de compilar el backend, **detener la API si está corriendo**: bloquea los DLL y el build
falla con MSB3027 sin decir por qué.

---

## Convenciones

- **Todo en español**: interfaz, mensajes de error, comentarios y mensajes de commit. Español
  neutro con formas de tú — nunca voseo rioplatense («pon», no «poné»).
- Los comentarios explican **por qué**, no qué. Si el código ya lo dice, el comentario sobra.
- Base de datos en `snake_case`. Dinero en `numeric(14,2)`, jamás punto flotante. Fechas en
  `timestamptz`.
- Formato chileno en la interfaz: `es-CL`, punto de miles, `$` sin decimales.
- Angular: `@if`/`@for`, signals, componentes standalone, rutas perezosas. Sin NgRx.

---

## Trampas que ya costaron caro

Cada una de estas se descubrió rompiendo algo. Están resueltas; el comentario existe para no
volver a caer.

### Base de datos

- **Npgsql rechaza `DateTimeOffset` que no sea UTC.** Las fechas chilenas llegan en `-04:00` y
  reventaban con 500. Resuelto globalmente en `RematesDbContext.ApplyUtcNormalization`, no por
  servicio.
- **Los filtros de borrado lógico deben repetirse en las tablas hijas**, o EF advierte y las
  consultas traen filas de vehículos borrados.
- **`.Select(x => x.ToResponse())` dentro de `ToListAsync` no se traduce.** Materializar primero.
- **`EnableRetryOnFailure` es incompatible con una transacción manual.** Envolver en
  `CreateExecutionStrategy().ExecuteAsync(...)`.
- Nunca devolver entidades EF directamente: los ciclos de navegación rompen la serialización. Hay
  DTOs y mappers para eso.
- **`Bid` cuelga del vehículo, y su lote de remate es opcional.** Exigir la cadena completa
  —casa de martillo, remate, lote— obligaría a inventar dos registros por cada puja para modelar
  algo que en la práctica no se lleva.

### Autenticación y despliegue

- **La contraseña del administrador debe cumplir la política** (10+ caracteres, mayúscula,
  minúscula, dígito) o el sembrado no crea la cuenta y el login responde 401 sin explicación.
- **`#` y `$` rompen el parseo de los archivos `.env`.**
- **`docker compose restart` NO toma variables nuevas del `.env`.** Hay que usar `up -d`, que
  recrea el contenedor.
- **El frontend se compila dentro de la imagen**: `git pull` sin `--build` no cambia nada de lo
  que sirve nginx. Y después el navegador cachea: Ctrl+F5 antes de dar un despliegue por fallido.
- **El Nginx Proxy Manager del servidor corre en modo `host`**, así que el comando de despliegue
  lleva **solo** `-f docker-compose.prod.yml`. Agregar `docker-compose.npm.yml` falla con
  «network declared as external» y los contenedores no se reemplazan.
- **Los healthchecks usan `127.0.0.1`, no `localhost`**: dentro del contenedor resuelve a IPv6
  primero y nginx escucha en IPv4, dejando todo permanentemente unhealthy.

### Frontend

- **`request.url.startsWith('')` es verdadero para cualquier URL.** En producción `API_BASE_URL`
  es `''`, así que el interceptor comprobaba mal y habría mandado el token a cualquier host. Ver
  `core/auth.interceptor.ts`.
- Para desplazar a un fragmento en una página larga hace falta `afterNextRender`: en
  `ngAfterViewInit` el documento todavía mide cero.
- Para intercalar tarjetas de dos columnas al apilarse en móvil se usa `display: contents` sobre
  las columnas y `order` sobre las tarjetas. Está en `deal-analyzer.scss`.
- Los campos que se tocan en la sala seleccionan todo al tocarlos, y se decide por
  `pointer: coarse` y no por ancho de ventana: importa cómo se escribe, no cuánto mide la
  pantalla. La selección va en el ciclo siguiente porque el móvil repone el cursor tras el foco.
  Ver `core/select-on-touch.ts`.
- **El `favicon.ico` que trae Angular se sirve igual aunque el HTML declare un SVG**, porque el
  navegador pide `/favicon.ico` por su cuenta. Se regenera con
  `dotnet run --project tools/Remates.LogoTracer -- --favicon <png>`. Y el navegador lo cachea
  con insistencia: Ctrl+F5 o incógnito para comprobarlo.

### Fuentes de mercado

- **La API de MercadoLibre no sirve para comparables.** Solo permite listar publicaciones de un
  vendedor concreto, y sin ese parámetro responde `403 forbidden`. Verificado con credenciales
  válidas. Se lee su HTML público en su lugar. El detalle está en `docs/DESPLIEGUE.md`.
- **`listado.mercadolibre.cl` y `autos.mercadolibre.cl` prohíben a ClaudeBot** en su robots.txt.
  Un agente no puede pedir esas páginas ni para probar; hay que pedirle al usuario que verifique.
- **`TextContent` de AngleSharp concatena los elementos sin separador**: el año «2021» y
  «88.000 Km» quedan como «202188.000 Km», que se descarta por absurdo y deja todos los avisos
  sin kilometraje sin que nada falle a la vista. Armar el texto elemento por elemento.
- **El título de un aviso de Yapo empieza con el nombre del vendedor.** Por eso el reconocedor de
  regiones deja fuera las comunas que son apellidos chilenos frecuentes —Castro, Linares, Ovalle,
  Coronel—: una región inventada es peor que ninguna, porque hace parecer comparable un auto que
  está a mil kilómetros.
- **Chileautos no se integra**: su robots.txt prohíbe las rutas necesarias.
- Toda fuente debe **fallar ruidosamente**. Si no reconoce nada o el portal ignoró la búsqueda,
  se descarta la respuesta y se informa el motivo. Devolver cero avisos se confunde con «no hay
  autos de ese modelo», y devolver avisos de otro auto es peor: nadie lo nota mirando la puja
  máxima resultante.

---

## Dónde vive cada cosa

| Qué | Dónde |
|---|---|
| Fórmulas de dinero | `Remates.Domain/Financial`, `/Bidding`, `/Scoring`, `/Market` |
| Calibración de la puja | `Remates.Domain/Learning/BidCalibration.cs` |
| Parámetros del negocio | `Remates.Domain/Parameters/AnalysisParameters.cs`, versionados en BD |
| Parser de avisos pegados | `Remates.Domain/Market/ListingParser.cs` |
| Adaptadores de portales | `Remates.Infrastructure/MarketSources/` |
| Definiciones de conceptos | `frontend/.../shared/glossary.ts` — fuente única, enlaza al manual |
| Ayuda «(?)» en pantalla | `frontend/.../shared/help-tip.ts` |
| Manual de usuario | `frontend/.../features/tutorial/tutorial.html` |

Las **anclas del glosario deben existir** como `id` en `tutorial.html`. Una rota no falla: no
hace nada, y nadie se entera.

### Las pantallas y para qué momento son

| Pantalla | Cuándo se usa |
|---|---|
| Analizador | Preparando. Es donde **nacen los vehículos**: se llenan y se guardan como lote. |
| Mercado | Preparando. Reúne los comparables que sostienen la valuación. |
| Remate | Durante la subasta. Los lotes en juego con su puja máxima y el precio que canta la sala. |
| Vehículos | El archivo completo, en cualquier estado. Abre la ficha de cada uno. |
| Estado | Capital, inventario, rentabilidad y calibración de la puja. |
| Parámetros | Los umbrales del negocio, versionados. |

**No hay formulario para crear un vehículo.** Se crean analizándolos y guardando, a propósito:
uno sin comparables ni daños no tiene puja máxima, y una ficha vacía no sirve para decidir nada.

---

## Al agregar algo

- Fórmula nueva → al dominio, con test. El test es la especificación.
- Pantalla nueva → agregarla al manual, al índice de `tutorial.ts` y al menú de `app.html`.
- Métrica que se mide sobre el historial → al dominio, y **que se abstenga con muestra corta**.
  Con tres datos cualquier proporción parece una tendencia, y actuar sobre ruido es peor que no
  actuar. `CalibrationCalculator` es el ejemplo: bajo ocho remates no opina, y lo dice.
- Concepto nuevo en pantalla → al glosario, con su ancla, y un `<app-help>` al lado.
- Fuente de mercado nueva → revisar su `robots.txt` **antes** de escribir código, identificarse
  con contacto en el User-Agent, respetar el intervalo por host, y que falle ruidosamente.
