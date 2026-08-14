# AutoMargin

Sistema para decidir **cuánto pujar** por un vehículo en un remate por deuda, y para seguir el ciclo
completo compra → reacondicionamiento → venta midiendo rentabilidad real.

El nombre apunta a la tesis del negocio: lo que se busca no es comprar autos, es encontrar margen.

La regla que gobierna todo el diseño: **el sistema no recomienda comprar porque un vehículo esté
barato**. Calcula una puja máxima considerando todos los costos, el tiempo y la incertidumbre, y
compara el precio del lote contra ese techo.

> **Nota sobre nombres.** El producto se llama AutoMargin. Los proyectos y namespaces de código
> mantienen el prefijo `Remates.*` por decisión explícita: renombrarlos no aporta valor funcional y
> se puede hacer más adelante si alguna vez estorba.

## Estado

**Paso 1 completo** — motores de cálculo, API de simulación y pantalla de análisis funcionando
end-to-end. Sin base de datos todavía (Paso 2).

| Paso | Alcance | Estado |
|---|---|---|
| 0 | Andamiaje, docker-compose, workspace Angular | ✅ |
| 1 | Motores + tests + `POST /api/analysis/simulate` + Analizador | ✅ |
| 1b | Manual embebido en la app (`/manual`) | ✅ |
| 2 | PostgreSQL, EF Core, JWT + roles, CRUD de vehículos | pendiente |
| 3 | Inventario, gastos reales, venta, rentabilidad real | pendiente |
| 4 | Dashboard y alertas | pendiente |
| 5 | Cierre del MVP (validación, logging, seeds) | pendiente |

## Arquitectura

```
Angular 20 SPA  ──HTTP/JSON──>  ASP.NET Core 10 Web API
                                      │
      ┌───────────────────────────────┼──────────────────────┐
  Remates.Domain              Remates.Infrastructure    Integraciones (F4)
  ├ FinancialEngine           ├ EF Core + Npgsql (F2)
  ├ MaxBidCalculator          ├ Identity + JWT (F2)
  ├ ValuationEngine           ├ IVisionAnalyzer (F2)
  ├ RepairEstimator           └ ISalePredictor (F3)
  ├ ScenarioBuilder                   │
  ├ ScoringEngine                PostgreSQL 16
  └ DealAnalyzer
```

`Remates.Domain` no tiene dependencias externas: los motores son funciones puras que reciben números
y devuelven números. Se testean sin base de datos, sin HTTP y sin IA.

## Qué usa IA y qué no

**Nunca IA**: puja máxima, costo total, utilidad, ROI, márgenes, break-even, score, semáforo, gates,
valuación estadística, agregados del dashboard.

**IA (Fase 2+)**: detección de daños en fotos, parsing de descripciones de remate, normalización de
marca/modelo, redacción de explicaciones a partir del JSON ya calculado, y un asistente con
tool-calling sobre endpoints read-only. El LLM narra números que le entrega la API; no los calcula.

## Requisitos

- .NET SDK 10
- Node 22 + Angular CLI 20
- Docker Desktop (a partir del Paso 2)

## Cómo correrlo

**1. API** (puerto 5044, Swagger en `/swagger`)

```bash
dotnet run --project "src/Remates.Api" --launch-profile http
```

**2. Frontend** (puerto 4200)

```bash
npm start --prefix frontend/remates-web
```

- `/analizador` — la pantalla de decisión
- `/manual` — manual completo del sistema, escrito para alguien que parte de cero: vocabulario del
  remate, qué significa cada símbolo de las fórmulas, ejemplo numérico paso a paso, errores comunes
  y glosario. Es la mejor puerta de entrada si alguien más va a usar la herramienta.

**3. Tests**

```bash
dotnet test
```

**4. Base de datos** (aún no la usa el código; queda lista para el Paso 2)

```bash
docker compose up -d
```

Copiar `.env.example` a `.env` antes de levantar los contenedores. pgAdmin queda en `localhost:5050`.

## Las fórmulas

Notación: `S` venta neta · `F` costos fijos post-compra · `α` tasa proporcional al martillo ·
`k` factor de costo de capital · `U` utilidad mínima exigida · `P` precio de adjudicación.

```
S = valor_conservador × (1 − provisión_garantía − marketing)
F = reparación + transporte + detailing + imprevistos + transferencia + otros
α = comisión_martillero × (1 + IVA) + gastos_admin% + impuesto_transferencia%
k = 1 + costo_capital_mensual × (días / 30)

Costo_total(P) = P(1+α)k + Fk
Utilidad(P)    = S − Costo_total(P)
ROI_anualizado = (1 + ROI_simple)^(365/días) − 1

Break-even     = (S/k − F) / (1+α)
U              = max(utilidad_mínima_abs, roi_anual_objetivo × (días/365) × capital)
Puja teórica   = (S − U − F·k) / ((1+α)·k)
PUJA MÁXIMA    = Puja teórica × (1 − margen_de_seguridad)
```

El margen de seguridad **no es fijo**: crece con la incertidumbre de reparación, la dispersión del
mercado, la escasez de comparables y el riesgo documental, acotado entre 3% y 25%.

### Por qué la comisión no se resta como monto fijo

La comisión del martillero es proporcional al precio de adjudicación, que es justamente la incógnita.
Restarla como constante sobrestima la puja en los vehículos caros. Por eso entra en `α` y la fórmula
se despeja algebraicamente. Hay un test que fija esta diferencia:
`La_comision_proporcional_baja_mas_la_puja_que_tratarla_como_costo_fijo`.

### Por qué el tiempo entra desde el MVP

15% en 20 días y 15% en 120 días no son el mismo negocio. El capital inmovilizado cuesta desde el
día 1, así que el costo de capital y el ROI anualizado se calculan siempre, no en una fase futura.

## Score y semáforo

Score 0–100 con siete componentes ponderados (rentabilidad 30, holgura de puja 15, liquidez 15,
riesgo mecánico 12, riesgo documental 10, certeza de la estimación 10, calidad de la evidencia 8).
Cada componente guarda su valor normalizado, los puntos que aporta y una explicación generada por
código.

**El score no decide solo.** El semáforo se ancla en `precio_actual vs puja_máxima`, y hay *gates*
duros que fuerzan ROJO sin importar el puntaje:

`PriceAboveMaxBid` · `RoiBelowMinimum` · `InsufficientMarketData` (menos de 3 comparables) ·
`CriticalDocumentRisk` · `PessimisticLossExceedsLimit` · `CapitalConcentration` · `NotViable`

```
ROJO      cualquier gate, o precio > puja máxima, o score < 50
AMARILLO  precio ≤ puja máxima y score ≥ 50
VERDE     precio ≤ puja máxima × 0,90 y score ≥ 70 y sin gates
```

## Parámetros

Todo umbral es configurable en `AnalysisParameters` (comisión, IVA, impuesto de transferencia,
contingencia, costo de capital, utilidad mínima, ROI objetivo, margen base, brecha de negociación,
pesos del score…). En el Paso 2 pasan a `parameter_set` en PostgreSQL, versionados: cambiar un
parámetro no debe alterar análisis históricos.

Los valores por defecto son un punto de partida razonable para Chile, **no una recomendación**.
Ajústalos con tus números reales.

## Advertencias

- Las estimaciones no reemplazan una inspección mecánica profesional ni un presupuesto de taller.
- El estado documental (encargo por robo, prendas, limitaciones al dominio, multas TAG) debe
  verificarse en el Registro Civil antes de pujar.
- El sistema no asume un régimen tributario. `ProfitTaxPct` es un parámetro que defines con tu
  contador; la utilidad se informa antes y después de impuestos por separado.
