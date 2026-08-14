# AutoMargin — Frontend

Aplicación Angular 20 (standalone + signals). Consume `Remates.Api` en `http://localhost:5044`.

```bash
npm start    # http://localhost:4200
npm run build
```

Rutas:

- `/analizador` — pantalla de decisión, recalcula en vivo contra `POST /api/analysis/simulate`.
- `/manual` — manual completo del sistema.

La URL de la API se configura en `src/app/core/api-config.ts`.
