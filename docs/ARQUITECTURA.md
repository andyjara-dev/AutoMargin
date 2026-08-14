# Sistema de Gestión y Análisis de Compra/Reventa de Vehículos de Remate

## Contexto

Negocio real en Chile: comprar vehículos en remates por deuda bajo su valor de mercado, reacondicionarlos y revenderlos. Hoy la decisión de cuánto pujar se toma "a ojo", lo que expone al negocio a sobrepagar en subasta y a descubrir costos de reparación después de la compra, cuando ya no hay salida.

El objetivo es un sistema que convierta esa decisión en un número defendible —la **puja máxima**— calculado determinísticamente, y que además registre el ciclo completo (compra → reparación → venta) para medir rentabilidad real y, con el tiempo, aprender de las propias operaciones.

Decisiones ya tomadas con el usuario: **.NET 10 (LTS)** + **Angular 20** + **PostgreSQL 16 vía docker-compose**, **JWT + roles desde el MVP**, y entrega en **slices verticales** (motor financiero funcionando end-to-end primero).

Directorio: `C:\Andy\Trabajos\Andysoft\Gestion Compra Remates` (vacío). Toolchain verificado: Node 22.14, Angular CLI 20.1.3, .NET SDK 10.0.302, Docker 28.4 (daemon apagado, hay que iniciar Docker Desktop).

---

## 1. Análisis del modelo de negocio: riesgos que el sistema debe modelar

Estos no son riesgos teóricos: cada uno cambia el diseño de las fórmulas o del esquema.

**R1 — Maldición del ganador.** En una subasta, ganar significa que fuiste el más optimista de la sala. Si tu estimación tiene error, ganar está *sesgado* hacia haber sobrepagado. Consecuencia de diseño: el margen de seguridad **no puede ser un % fijo**; debe crecer con la incertidumbre de las estimaciones de ese vehículo en particular.

**R2 — Circularidad de costos.** La comisión del martillero (típicamente ~10% + IVA) y parte de los impuestos de transferencia son **proporcionales al precio de adjudicación**. La fórmula que planteaste resta "costos" como si fueran fijos, pero algunos dependen de la incógnita que estás despejando. Hay que resolverlo algebraicamente (ver §7), o el sistema sobrestima la puja máxima justo en los autos caros.

**R3 — El tiempo no está en tu fórmula.** Tú mismo lo notaste ("15% en 20 días es mucho mejor que 15% en 120 días"), pero lo dejaste para la Fase 3. Es un error: el capital inmovilizado tiene costo desde el día 1. **El ROI anualizado y el costo financiero entran en el MVP**, no después. Sin eso el sistema recomendará comprar autos rentables pero ilíquidos, que es la forma más común de quebrar en este negocio.

**R4 — Los precios publicados no son precios de transacción.** Chileautos/Yapo/MercadoLibre muestran precios de *lista*. La venta real ocurre 5–12% abajo. El valor de mercado debe aplicar un **descuento de negociación configurable** antes de alimentar la puja.

**R5 — Riesgo documental, que es binario y arruina el deal.** Encargo por robo, prendas o gravámenes no alzados, limitaciones al dominio, multas TAG impagas (se transfieren al vehículo), permiso de circulación y revisión técnica vencidos, retraso en inscripción en el Registro Civil. Estos no son "un poco de riesgo": son *gates* que fuerzan ROJO sin importar el score.

**R6 — Riesgo mecánico no observable.** En remate se compra sin prueba de manejo y a veces sin encendido. La estimación de reparación debe ser **un rango (min/esperado/max) por categoría**, nunca un punto, y el escenario pesimista debe evaluarse siempre.

**R7 — Riesgo de ruina por concentración.** Si el 90% del capital está en 3 autos y uno sale malo, el negocio muere aunque el ROI promedio sea bueno. El sistema necesita una regla de *sizing*: máximo % del capital total por unidad y máximo % inmovilizado.

**R8 — Garantía legal y vicios redhibitorios.** Si vendes de forma habitual, la Ley 19.496 aplica. Hay que provisionar un % del precio de venta como costo esperado de garantía/postventa.

**R9 — Tributario.** La habitualidad convierte esto en actividad comercial (impuesto a la renta sobre la utilidad; el tratamiento de IVA en vehículos usados tiene reglas específicas). **No soy tu asesor tributario y el sistema no debe asumir un régimen**: los impuestos se modelan como parámetros configurables (`tasa_impuesto_utilidad`, `iva_sobre_comision`) que tú y tu contador definen. El sistema calcula utilidad antes y después de impuestos por separado.

**R10 — Arranque en frío.** No tienes historial propio todavía. Los primeros ~20 vehículos alimentan tablas de parámetros manuales, no modelos. Por eso el MVP debe **instrumentar la comparación predicción vs. realidad desde el primer vehículo** (tabla `prediction_outcome`), o en 12 meses no tendrás con qué entrenar nada.

**R11 — Falsa precisión del score.** Un "87/100" con pesos inventados parece ciencia y no lo es. Mitigación: pesos versionados y editables en BD, y el score **nunca** decide solo — el semáforo se ancla en la relación `precio_actual vs puja_máxima`, que es una comparación de pesos, no de puntos.

**R12 — Nunca ganar.** Si la puja máxima es demasiado conservadora, no compras nada. El sistema debe registrar **pujas perdidas y a qué precio se adjudicó el lote** para calibrar la agresividad con datos reales.

---

## 2. MVP exacto (alcance cerrado)

**Entra:**
1. Auth JWT + roles (Admin/Vendedor/Mecánico/Analista) y auditoría de cambios.
2. CRUD de vehículo con ficha técnica.
3. Registro de datos del remate (martillero, fecha, lote, precio actual/mínimo, % comisión).
4. Comparables de mercado ingresados a mano → cálculo de valor optimista/esperado/**conservador** por percentiles.
5. Estimación de reparación por categoría con rango min/esperado/max (tabla de costos base precargada, editable).
6. **Motor financiero**: costo total, utilidad, margen, ROI simple, **ROI anualizado**, break-even, escenarios pesimista/esperado/optimista.
7. **Motor de puja máxima** con costos proporcionales resueltos y margen de seguridad dinámico.
8. **Motor de scoring** 0–100 con desglose explicable + gates duros.
9. **Semáforo** con razones y números que lo sustentan.
10. Registro de compra real, gastos reales por categoría (presupuestado vs. real), publicación y venta.
11. Rentabilidad real y días en inventario.
12. Dashboard básico: capital, inventario, utilidad realizada/potencial, ROI promedio, alertas.
13. Registro de resultado de puja (ganada/perdida + precio de adjudicación) → dato para calibrar.

**No entra (y por qué):** análisis IA de fotos (Fase 2 — requiere el motor de daños ya estable), asistente conversacional (Fase 2 — necesita datos que aún no existen), predicción ML (Fase 3 — necesita ~100 ventas), scraping de fuentes (Fase 4 — frágil y sujeto a términos de uso). Todos quedan con su *interfaz* definida en el MVP para que enchufarlos no requiera reescribir nada.

---

## 3. Módulos

| Módulo | Responsabilidad | IA |
|---|---|---|
| `Identity` | Usuarios, roles, JWT, auditoría | No |
| `Catalog` | Marcas/modelos/versiones normalizados | Fase 2 (normalización de texto libre) |
| `Vehicles` | Ficha, estado, historial de estados, fotos | No |
| `Auctions` | Remates, lotes, pujas, resultados | No |
| `Market` | Comparables, valuación estadística | Fase 3 (modelo de precio) |
| `Damage` | Daños por categoría/severidad → costo estimado | Fase 2 (visión) |
| `FinancialEngine` | Costos, utilidad, ROI, puja máxima, escenarios | **Nunca** |
| `ScoringEngine` | Score, gates, semáforo | **Nunca** |
| `Inventory` | Compra, gastos reales, publicación, venta | No |
| `Dashboard` | KPIs, alertas | No |
| `Learning` | `prediction_outcome`, error de estimación | Fase 3 |
| `AiServices` | Visión, asistente, explicaciones | Sí (aislado) |
| `Integrations` | Adaptadores de fuentes | Fase 4 |

---

## 4. Arquitectura

```
Angular 20 SPA  ──HTTPS/JWT──>  ASP.NET Core 10 Web API
                                      │
      ┌───────────────────────────────┼────────────────────────────┐
      │                               │                            │
  Domain (puro, sin deps)      Infrastructure                Integraciones
  ├ FinancialEngine            ├ EF Core + Npgsql            ├ IAuctionSourceAdapter (F4)
  ├ ScoringEngine              ├ Identity + JWT              └ (adaptador por fuente)
  ├ ValuationEngine            ├ Auditoría (SaveChanges)
  ├ RiskEngine                 ├ IVisionAnalyzer  ──> stub | Claude API (F2)
  └ Entidades + VOs            ├ IAssistant       ──> stub | Claude API (F2)
                               └ ISalePredictor   ──> heurística | ML.NET (F3)
                                      │
                                 PostgreSQL 16
```

4 proyectos, sin capa Application separada (evitar sobreingeniería):

```
Remates.sln
├─ src/Remates.Domain/          entidades, value objects, MOTORES (sin dependencias externas)
├─ src/Remates.Infrastructure/  EF Core, Npgsql, Identity, auditoría, adaptadores IA (stubs)
├─ src/Remates.Api/             controllers, DTOs, validación, Swagger, DI
└─ tests/Remates.Domain.Tests/  xUnit — cobertura fuerte sobre los motores
frontend/remates-web/           Angular 20 standalone + signals
docker-compose.yml              postgres:16 + pgadmin
```

Regla de oro: **los motores son clases puras y determinísticas** (entran DTOs de números, salen DTOs de números). Se testean sin base de datos, sin HTTP y sin IA. Toda la lógica de dinero vive ahí.

---

## 5–6. Modelo de datos PostgreSQL

Convenciones: `snake_case`, PK `bigint generated always as identity`, dinero `numeric(14,2)` (**nunca** float), fechas `timestamptz`, `created_at/updated_at/created_by` en todas, borrado lógico con `deleted_at` donde aplique.

**Identidad y auditoría**
- `app_user`(id, email, password_hash, full_name, is_active)
- `role`(id, code) · `user_role`(user_id, role_id)
- `audit_log`(id, entity_name, entity_id, action, changes `jsonb`, user_id, occurred_at)

**Catálogo**
- `make`(id, name) · `model`(id, make_id, name, body_type) · `trim`(id, model_id, name)
- `repair_cost_baseline`(id, category, severity, cost_min, cost_expected, cost_max, valid_from, notes) — semilla editable

**Vehículo**
- `vehicle`(id, make_id, model_id, trim_id, year, mileage_km, transmission, fuel, body_type, plate, vin, color, region, comuna, equipment `jsonb`, condition_notes, status, source_type, external_ref, url, detected_at)
- `vehicle_status_history`(id, vehicle_id, from_status, to_status, changed_at, user_id, note)
- `vehicle_photo`(id, vehicle_id, storage_path, kind, uploaded_at)

`status` ∈ `detected | analyzing | bidding | won | lost | purchased | in_transport | in_repair | ready | listed | reserved | sold | discarded`
(agrego `lost` y `discarded` — sin ellos no puedes medir tasa de conversión de pujas, R12.)

**Remate**
- `auction_house`(id, name, default_commission_pct, commission_has_vat, admin_fee_fixed, storage_fee_per_day)
- `auction`(id, auction_house_id, name, auction_date, region, terms_url)
- `auction_lot`(id, auction_id, vehicle_id, lot_number, minimum_price, current_price, deposit_required, closes_at)
- `bid`(id, auction_lot_id, max_bid_authorized, bid_placed, result `won|lost|not_bid`, winning_price, decided_at, user_id) ← alimenta calibración

**Mercado**
- `market_comparable`(id, vehicle_id, source, url, listed_price, year, mileage_km, transmission, fuel, region, condition, observed_at, is_outlier, weight)
- `market_valuation`(id, vehicle_id, method, comparable_count, value_optimistic, value_expected, value_conservative, dispersion_pct, negotiation_discount_pct, computed_at, engine_version, detail `jsonb`)

**Daños y reparación**
- `damage_item`(id, vehicle_id, category, severity, description, cost_min, cost_expected, cost_max, source `manual|ai|workshop`, confidence, ai_analysis_id)
- `repair_estimate`(id, vehicle_id, total_min, total_expected, total_max, computed_at, source, disclaimer)

**Parámetros (versionados — clave para reproducibilidad)**
- `parameter_set`(id, name, is_active, valid_from, created_by)
- `parameter_value`(id, parameter_set_id, key, numeric_value, text_value)
  Claves: `commission_pct`, `vat_pct`, `transfer_tax_pct`, `transfer_fixed`, `transport_default`, `detailing_default`, `marketing_pct`, `warranty_provision_pct`, `contingency_pct`, `capital_cost_monthly_pct`, `min_profit_abs`, `min_roi_annual`, `safety_margin_base`, `max_capital_per_unit_pct`, `negotiation_discount_pct`, `default_days_to_sell`.

**Análisis (snapshot inmutable — nunca se recalcula sobre el pasado)**
- `deal_analysis`(id, vehicle_id, auction_lot_id, parameter_set_id, engine_version, computed_at, user_id,
  sale_value_conservative, total_fixed_costs, proportional_rate, capital_cost, expected_profit, roi_simple, roi_annualized, margin_pct, breakeven_bid, **max_bid**, safety_margin_pct, estimated_days_to_sell,
  score, traffic_light, gates_triggered `jsonb`, score_breakdown `jsonb`, cost_breakdown `jsonb`, scenarios `jsonb`, inputs_snapshot `jsonb`)

**Inventario real**
- `purchase`(id, vehicle_id, auction_lot_id, hammer_price, commission_paid, purchase_date, invoice_ref)
- `expense`(id, vehicle_id, category, description, amount, expense_date, supplier, document_ref, is_over_budget, budgeted_amount)
- `listing`(id, vehicle_id, channel, list_price, published_at, unpublished_at, url)
- `price_change`(id, listing_id, old_price, new_price, changed_at, reason)
- `sale`(id, vehicle_id, sale_price, sale_date, buyer_name, payment_method, days_in_inventory, real_profit, real_roi, real_roi_annualized)

**Capital**
- `cash_movement`(id, type `contribution|withdrawal|purchase|expense|sale_income`, amount, movement_date, vehicle_id?, note)

**Aprendizaje e IA**
- `prediction_outcome`(id, vehicle_id, deal_analysis_id, predicted_sale_value, actual_sale_value, predicted_repair_cost, actual_repair_cost, predicted_days, actual_days, error_pct `jsonb`, closed_at) ← **se llena automáticamente al registrar la venta**
- `ai_analysis`(id, vehicle_id, kind `vision|description|assistant`, provider, model, prompt_version, request `jsonb`, response `jsonb`, tokens_in, tokens_out, cost, created_at)
- `alert`(id, vehicle_id?, type, severity, message, data `jsonb`, created_at, acknowledged_at)

Índices: `vehicle(status)`, `vehicle(make_id, model_id, year)`, `deal_analysis(vehicle_id, computed_at desc)`, `expense(vehicle_id)`, `market_comparable(vehicle_id)`, `alert(acknowledged_at) where acknowledged_at is null`.

---

## 7. Reglas matemáticas de rentabilidad

Todo en CLP, `decimal` en C#, redondeo a peso solo en presentación.

**Valor de mercado** (a partir de n comparables, ajustados por kilometraje y año):
```
precio_ajustado_i = precio_lista_i × (1 + ajuste_km_i + ajuste_año_i)
optimista    = P75(precio_ajustado)
esperado     = P50(precio_ajustado)
conservador  = P25(precio_ajustado) × (1 − negotiation_discount_pct)
```
Con n < 3 comparables → gate duro (`INSUFFICIENT_MARKET_DATA`).

**Precio de venta neto** usado en toda la cadena:
```
S = valor_conservador × (1 − warranty_provision_pct − marketing_pct)
```

**Costos fijos posteriores a la compra** (no dependen de la puja):
```
F = reparación_esperada + transporte + detailing + transfer_fixed
    + otros_fijos + contingency_pct × (reparación_esperada + transporte + detailing)
```

**Tasa proporcional al precio de martillo** (aquí se resuelve R2):
```
α = commission_pct × (1 + vat_pct) + admin_fee_pct + transfer_tax_pct
```

**Factor de costo de capital** para `d` días estimados de venta:
```
k = 1 + capital_cost_monthly_pct × (d / 30)
```

**Costo total** dado un precio de martillo `P`:
```
Costo_total(P) = P × (1 + α) × k + F × k
Utilidad(P)    = S − Costo_total(P)
```

**Métricas** (con `C = Costo_total`):
```
ROI_simple       = Utilidad / C
Margen_venta     = Utilidad / valor_conservador
ROI_anualizado   = (1 + ROI_simple)^(365 / d) − 1
Break-even bid   = P donde Utilidad = 0  →  P_be = (S/k − F) / (1 + α)
```

**Escenarios** — se calculan siempre los tres y el pesimista se muestra junto al esperado:

| Escenario | Venta | Reparación | Días |
|---|---|---|---|
| Optimista | valor esperado | `total_min` | `d × 0.7` |
| Esperado | valor conservador | `total_expected` | `d` |
| Pesimista | valor conservador × 0.93 | `total_max` | `d × 1.6` |

---

## 8. Algoritmo de PUJA MÁXIMA

**Utilidad mínima requerida** — corrige el problema de un monto fijo en pesos, que penaliza mal los extremos:
```
U = max( min_profit_abs,
         min_roi_annual × (d/365) × capital_estimado )
donde capital_estimado ≈ P_be × (1+α) + F   (una pasada, luego se refina)
```

**Puja máxima teórica** — se despeja `P` de `S = P(1+α)k + F·k + U`:
```
P_teórica = ( S − U − F × k ) / ( (1 + α) × k )
```

**Margen de seguridad dinámico** (mitiga R1 — crece con la incertidumbre real del vehículo):
```
σ_reparación = (total_max − total_min) / (2 × total_expected)     [0..1]
σ_mercado    = dispersión_comparables / valor_esperado             [0..1]
σ_datos      = 1 / (1 + comparable_count)                          [0..1]
riesgo_doc   = 0 | 0.25 | 0.5 | 1  (ninguno/leve/medio/alto)

MS = clamp( safety_margin_base
            + 0.20 × σ_reparación
            + 0.15 × σ_mercado
            + 0.10 × σ_datos
            + 0.10 × riesgo_doc,
            0.03, 0.25 )
```

**Resultado:**
```
PUJA_MÁXIMA = floor( P_teórica × (1 − MS) )
```
Si `PUJA_MÁXIMA ≤ 0` → gate `NOT_VIABLE`.

El sistema muestra siempre los tres números juntos: **break-even** (donde pierdes), **puja máxima** (donde ganas lo mínimo aceptable) y **precio actual del lote**. Esa terna es la decisión.

---

## 9. Algoritmo de SCORE (0–100)

Pesos en `parameter_set`, versionados. Cada componente se normaliza a 0–100 y guarda su contribución en `score_breakdown` para explicación sin LLM.

| # | Componente | Peso | Normalización |
|---|---|---|---|
| 1 | Rentabilidad | 30 | `clamp(ROI_anualizado / (2 × min_roi_annual)) × 100` |
| 2 | Holgura de puja | 15 | `clamp((max_bid − precio_actual) / max_bid × 4) × 100` |
| 3 | Liquidez esperada | 15 | `clamp(1 − (d − 15) / 75) × 100` |
| 4 | Riesgo mecánico (inv.) | 12 | `100 − severidad_mecánica_ponderada` |
| 5 | Riesgo documental (inv.) | 10 | `100 − riesgo_doc × 100` |
| 6 | Certeza de estimación (inv.) | 10 | `100 − σ_reparación × 100` |
| 7 | Calidad de evidencia | 8 | `f(comparable_count, antigüedad, dispersión)` |

`SCORE = round(Σ wᵢ × sᵢ / 100)`

**Gates duros** — fuerzan ROJO sin importar el score:
`PRICE_ABOVE_MAX_BID` · `ROI_BELOW_MINIMUM` · `INSUFFICIENT_MARKET_DATA` (n<3) · `CRITICAL_DOCUMENT_RISK` · `PESSIMISTIC_LOSS_EXCEEDS_LIMIT` · `CAPITAL_CONCENTRATION` (supera `max_capital_per_unit_pct`) · `NOT_VIABLE`.

**Semáforo:**
```
ROJO      si hay cualquier gate, o precio_actual > max_bid, o score < 50
AMARILLO  si precio_actual ≤ max_bid  y  score ≥ 50
VERDE     si precio_actual ≤ max_bid × 0.90  y  score ≥ 70  y  sin gates
```
La UI muestra siempre: semáforo + score + los 3 componentes que más suman + los que más restan + gates activos + la terna break-even/puja máxima/precio actual.

---

## 10. Pantallas Angular

Angular 20 standalone + signals + Angular Material. `@if/@for`. Estado por servicio con `signal()`, sin NgRx (sobreingeniería para este tamaño).

1. **Login** — JWT, refresh token.
2. **Dashboard** — fila de KPIs (capital disponible / invertido / inmovilizado, utilidad realizada y potencial, ROI promedio, días promedio en inventario), panel de alertas, top 5 oportunidades.
3. **Oportunidades** — tabla densa: vehículo, precio actual, puja máxima, holgura, ROI anual, días est., score, semáforo. Filtros y orden por score. Un click abre el analizador.
4. **Analizador de deal** ⭐ *la pantalla central*. Dos columnas: izquierda = inputs (ficha, comparables, daños, parámetros); derecha = resultados **en vivo** (recalcula al escribir, contra `POST /analysis/simulate`, sin guardar). Arriba y grande: **PUJA MÁXIMA**. Debajo: semáforo con razones, terna de precios en una barra visual, desglose de costos, tabla de 3 escenarios, desglose del score.
5. **Comparables** (tab del analizador) — alta rápida de comparables, marcado de outliers, percentiles calculados.
6. **Inventario** — kanban por estado + vista tabla; badges de días en inventario y sobrecosto.
7. **Detalle de vehículo comprado** — timeline de estados, gastos reales vs. presupuesto por categoría, utilidad proyectada actualizada.
8. **Registrar venta** — precio, fecha, forma de pago → dispara cálculo de rentabilidad real y llena `prediction_outcome`.
9. **Parámetros** — edición del `parameter_set` activo, con versionado.
10. **Aprendizaje** (Fase 3) — error de estimación por modelo, por tipo de daño, por región.

---

## 11. Endpoints REST

```
POST   /api/auth/login | /refresh | /logout
GET    /api/catalog/makes | /models?makeId= | /repair-baselines

GET    /api/vehicles?status=&search=&page=
POST   /api/vehicles
GET    /api/vehicles/{id}
PUT    /api/vehicles/{id}
POST   /api/vehicles/{id}/status

GET    /api/vehicles/{id}/comparables
POST   /api/vehicles/{id}/comparables
DELETE /api/comparables/{id}
POST   /api/vehicles/{id}/valuation          → calcula y persiste market_valuation

GET    /api/vehicles/{id}/damages
POST   /api/vehicles/{id}/damages            → recalcula repair_estimate
DELETE /api/damages/{id}

POST   /api/analysis/simulate                → stateless, sin persistir (usado por la UI en vivo)
POST   /api/vehicles/{id}/analysis           → persiste snapshot deal_analysis
GET    /api/vehicles/{id}/analysis/latest
GET    /api/vehicles/{id}/analysis/history

POST   /api/auctions | /auctions/{id}/lots
POST   /api/lots/{id}/bid                    → registra puja y resultado (won/lost/not_bid)

POST   /api/vehicles/{id}/purchase
GET    /api/vehicles/{id}/expenses
POST   /api/vehicles/{id}/expenses
POST   /api/vehicles/{id}/listing
POST   /api/listings/{id}/price-change
POST   /api/vehicles/{id}/sale               → cierra prediction_outcome

GET    /api/dashboard/summary | /alerts | /opportunities
GET    /api/parameters/active
PUT    /api/parameters/active

POST   /api/ai/vision/{vehicleId}            (Fase 2 — stub 501 en MVP)
POST   /api/ai/assistant/query               (Fase 2 — stub 501 en MVP)
```

---

## 12. Qué usa IA y qué NO

**Prohibido para IA (código determinístico, siempre):** puja máxima, costo total, utilidad, ROI, márgenes, break-even, score, semáforo, gates, valuación estadística, todos los agregados del dashboard, rentabilidad real.

**IA permitida (siempre como *entrada* o *explicación*, nunca como cálculo):**
- Visión → detectar daños en fotos y proponer `damage_item` con categoría/severidad/rango y `confidence`. Sale con `source='ai'`, requiere **confirmación humana** antes de entrar al cálculo, y arrastra el disclaimer de que no reemplaza inspección mecánica.
- Parsing de descripciones de remate → campos estructurados propuestos.
- Normalización marca/modelo/versión desde texto libre.
- Redacción de la explicación en lenguaje natural **a partir del JSON de `score_breakdown`/`cost_breakdown` ya calculado**.
- Asistente conversacional con *tool calling* sobre endpoints read-only: el LLM elige qué consultar, la API devuelve los números, el LLM solo los narra. Prompt de sistema con prohibición explícita de aritmética.

---

## 13. Evolución hacia Machine Learning

- **Fase 1 (MVP)**: reglas + `repair_cost_baseline`. **Instrumentación desde el día 1**: cada análisis guarda su predicción; cada venta cierra `prediction_outcome`. Sin esto no hay ML posible después.
- **Fase 2**: visión multimodal vía Claude API detrás de `IVisionAnalyzer`; asistente detrás de `IAssistant`. Ambas interfaces ya existen (stubs) desde el MVP.
- **Fase 3** (n ≥ ~100 ventas): precio de venta (gradient boosting sobre comparables + atributos) y días de venta (análisis de supervivencia). **ML.NET dentro de .NET primero** — no agrega stack; migrar a un servicio Python/FastAPI solo si los modelos lo justifican. Calibración de costos de reparación por regresión sobre `damage_item` → `expense` real.
- Cada modelo se despliega detrás de la misma interfaz, con métrica de error publicada en la pantalla de Aprendizaje y *fallback* automático a la heurística si el error supera un umbral.
- **Fase 4**: `IAuctionSourceAdapter` por fuente, con rate limiting, respeto de `robots.txt`/términos de uso y credenciales por adaptador. Ninguna fuente se integra sin revisar sus condiciones.

---

## 14. Plan de implementación (slices verticales)

### Paso 0 — Andamiaje
- `docker-compose.yml` (postgres:16 + pgadmin), `.env.example`, `.gitignore`, `git init`.
- `Remates.sln` con los 4 proyectos; `frontend/remates-web` con `ng new` (standalone, routing, SCSS).
- README con arranque.

### Paso 1 — Motores + analizador end-to-end ⭐ *(el slice que te da valor inmediato)*
- `Remates.Domain`: `MoneyMath`, `ValuationEngine`, `FinancialEngine`, `MaxBidCalculator`, `ScoringEngine`, `RiskEngine`, `ScenarioBuilder` — puros, sin dependencias.
- `Remates.Domain.Tests`: xUnit sobre todos los motores. Casos obligatorios: el ejemplo de tu enunciado, circularidad de comisión, `d` variable, márgenes de seguridad extremos, cada gate, break-even, división por cero, comparables insuficientes.
- `POST /api/analysis/simulate` sin base de datos.
- Angular: pantalla **Analizador de deal** completa contra ese endpoint.
- ✅ *Verificable*: abres la pantalla, escribes números, ves puja máxima, score y semáforo en vivo.

### Paso 2 — Persistencia, auth y ficha de vehículo
- EF Core + Npgsql, migración inicial, seeds (`repair_cost_baseline`, `parameter_set` por defecto, catálogo básico, usuario admin).
- Identity + JWT + roles + interceptor de auditoría en `SaveChanges`.
- CRUD vehículos, comparables, daños, remates/lotes; persistencia de `deal_analysis`.
- Angular: login, guards, interceptor, listado de vehículos, tabs de comparables y daños.

### Paso 3 — Inventario y ciclo real
- Compra, gastos reales vs. presupuesto, publicación, cambios de precio, venta.
- Rentabilidad real, días en inventario, cierre de `prediction_outcome`, `cash_movement`.
- Angular: kanban de inventario, detalle del vehículo comprado, registro de venta.

### Paso 4 — Dashboard y alertas
- KPIs, motor de alertas (días en inventario, margen bajo, sobrecosto de reparación, precio a ajustar, concentración de capital).
- Angular: dashboard + lista de oportunidades ordenada por score.

### Paso 5 — Cierre del MVP
- Swagger documentado, manejo global de errores, validación (FluentValidation), logging estructurado (Serilog), seeds de demo, README de operación.

---

## Verificación

- **Motores**: `dotnet test` — los tests son la especificación ejecutable de las fórmulas de §7–9. El ejemplo del enunciado del usuario es un test nombrado explícitamente.
- **API**: `dotnet run` + Swagger en `/swagger`; `POST /api/analysis/simulate` con el payload del ejemplo debe devolver la puja máxima esperada.
- **BD**: `docker compose up -d` → `dotnet ef database update` → verificar seeds con pgAdmin.
- **Front**: `ng serve` → login → analizador → cambiar precio de reparación y ver el semáforo cambiar de VERDE a ROJO al cruzar la puja máxima.
- **Flujo completo (Paso 3+)**: crear vehículo → analizar → registrar compra → cargar gastos → registrar venta → confirmar que el ROI real aparece en el dashboard y que `prediction_outcome` quedó poblado.

## Nota

El sistema entrega estimaciones y una recomendación calculada; no sustituye la inspección mecánica profesional, la verificación documental en el Registro Civil, ni la asesoría tributaria o legal. Las decisiones de compra siguen siendo tuyas.
