# Vectorizador del isotipo

Convierte un PNG del símbolo de AutoMargin en un SVG con curvas suaves y el gradiente de marca.
Traza los píxeles reales, así que el resultado sigue el dibujo original y no una interpretación.

No requiere instalar nada: usa ImageSharp 2.1 (Apache 2.0) como paquete del proyecto.

## Cómo preparar el PNG

1. Exporta **solo el símbolo**, sin el texto «AutoMargin».
2. **Fondo transparente** si puedes. Si no, un fondo plano y parejo también funciona.
3. Lo más grande posible: **1024 px o más** por lado. De ahí sale el detalle de las curvas.
4. Sin sombras, brillos ni bordes difuminados: el trazador los interpreta como forma.

Guárdalo en `frontend/remates-web/public/logo-source.png`.

## Uso

```bash
dotnet run --project tools/Remates.LogoTracer -- frontend/remates-web/public/logo-source.png frontend/remates-web/public/logo.svg
```

Parámetros opcionales, en este orden después de la salida:

| Parámetro | Por defecto | Qué hace |
|---|---|---|
| tolerancia | 0,25 | Cuánto debe despegarse un píxel del fondo para contar como dibujo. Bájala si pierde partes; súbela si captura el fondo. |
| suavizado | 1,2 | Cuánto detalle se descarta. Súbelo si el trazo sale con ruido; bájalo si pierde definición. |

```bash
dotnet run --project tools/Remates.LogoTracer -- entrada.png salida.svg 0.15 0.8
```

El programa avisa si detectó casi nada o casi todo, que son los dos síntomas de una tolerancia mal
ajustada.

## Verificar que funciona

```bash
dotnet run --project tools/Remates.LogoTracer -- --selftest
```

Traza figuras de geometría conocida: un disco debe dar un contorno y un anillo dos, el exterior y
el del agujero.

## El favicon.ico

Aparte del SVG hace falta un `.ico`: el navegador pide `/favicon.ico` aunque el HTML declare un
SVG, y si ahí queda el archivo que trae Angular por defecto, en la pestaña aparece su logo.

```bash
dotnet run --project tools/Remates.LogoTracer -- --favicon frontend/remates-web/public/logo-source.png
```

Recorta el margen transparente —sin eso el isotipo queda diminuto y a 16 px no se distingue—,
lo centra en un lienzo cuadrado y guarda seis tamaños: 16, 32, 48, 64, 128 y 256.

> Los navegadores guardan el favicon con mucha insistencia. Después de reemplazarlo hay que
> recargar con Ctrl+F5, y a veces abrir la pestaña en una ventana de incógnito para comprobarlo.

## Después de generar el SVG

El isotipo se usa en cuatro lugares:

- `frontend/remates-web/public/logo.svg` — archivo suelto
- `frontend/remates-web/public/favicon.svg` — versión con fondo oscuro para la pestaña
- `frontend/remates-web/public/favicon.ico` — para navegadores que piden el .ico
- `frontend/remates-web/src/app/shared/logo.ts` — componente Angular, con el trazo en línea

Copiar el contenido del `<path>` generado a los otros mantiene todo consistente.
