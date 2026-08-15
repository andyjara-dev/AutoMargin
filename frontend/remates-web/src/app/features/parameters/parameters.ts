import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { catchError, of, tap } from 'rxjs';

import { API_BASE_URL } from '../../core/api-config';
import { AnalysisParameters } from '../../core/models/analysis.models';
import { GlossaryKey } from '../../shared/glossary';
import { HelpTip } from '../../shared/help-tip';

interface ParameterSetResponse {
  id: number;
  name: string;
  isActive: boolean;
  validFrom: string;
  note?: string | null;
  parameters: AnalysisParameters;
}

interface ParameterVersionRow {
  id: number;
  name: string;
  isActive: boolean;
  validFrom: string;
  note?: string | null;
  createdBy?: string | null;
}

/** Cómo se edita y se muestra cada parámetro. */
type FieldKind = 'rate' | 'money' | 'int' | 'points';

interface Field {
  key: keyof AnalysisParameters;
  label: string;
  kind: FieldKind;
  help: string;
}

interface FieldGroup {
  title: string;
  intro: string;
  /**
   * Concepto del glosario al que pertenece el grupo. La ayuda va en el encabezado y no campo por
   * campo: cada campo ya tiene su explicación debajo, y quince interrogaciones seguidas dejarían
   * de leerse. Lo que falta aquí es el enlace al manual, y con uno por grupo basta.
   */
  helpKey?: GlossaryKey;
  fields: Field[];
}

@Component({
  selector: 'app-parameters',
  imports: [ReactiveFormsModule, DatePipe, HelpTip],
  templateUrl: './parameters.html',
  styleUrl: './parameters.scss'
})
export class Parameters {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);

  readonly current = signal<ParameterSetResponse | null>(null);
  readonly history = signal<ParameterVersionRow[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly saved = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    name: [''],
    note: ['']
  });

  /** Valores editables, aparte del formulario de metadatos. */
  readonly values = signal<Record<string, number>>({});

  readonly groups: FieldGroup[] = [
    {
      title: 'Costos del remate',
      helpKey: 'costosProporcionales',
      intro: 'Son proporcionales al precio de adjudicación: suben si pujas más alto. Por eso el ' +
             'sistema los despeja algebraicamente en vez de restarlos como monto fijo.',
      fields: [
        { key: 'commissionPct', label: 'Comisión del martillero', kind: 'rate', help: 'Porcentaje sobre el precio de adjudicación.' },
        { key: 'vatPct', label: 'IVA', kind: 'rate', help: 'Se aplica sobre la comisión del martillero.' },
        { key: 'transferTaxPct', label: 'Impuesto de transferencia', kind: 'rate', help: 'Porcentaje sobre el precio de compra.' },
        { key: 'adminFeePct', label: 'Gastos administrativos', kind: 'rate', help: 'Porcentaje adicional que cobre el martillero.' },
        { key: 'adminFeeFixed', label: 'Gastos administrativos fijos', kind: 'money', help: 'Monto fijo, independiente del precio.' },
        { key: 'transferFixed', label: 'Trámite de transferencia', kind: 'money', help: 'Notaría y gestoría.' }
      ]
    },
    {
      title: 'Costos posteriores',
      helpKey: 'costosFijos',
      intro: 'Lo que se gasta después de comprar y no depende del precio de adjudicación.',
      fields: [
        { key: 'transportDefault', label: 'Transporte por defecto', kind: 'money', help: 'Se usa cuando no se informa uno específico.' },
        { key: 'detailingDefault', label: 'Detailing por defecto', kind: 'money', help: 'Preparación estética antes de publicar.' },
        { key: 'contingencyPct', label: 'Imprevistos', kind: 'rate', help: 'Colchón sobre reparación, transporte y detailing. Las reparaciones rara vez salen en lo presupuestado.' },
        { key: 'marketingPct', label: 'Publicación', kind: 'rate', help: 'Porcentaje del precio de venta destinado a avisos.' },
        { key: 'warrantyProvisionPct', label: 'Provisión de garantía', kind: 'rate', help: 'Se aparta del precio de venta para responder por fallas posteriores.' }
      ]
    },
    {
      title: 'Tiempo y capital',
      helpKey: 'costoCapital',
      intro: 'Lo que mueve la diferencia entre un negocio rentable y uno rentable pero lento.',
      fields: [
        { key: 'capitalCostMonthlyPct', label: 'Costo mensual del capital', kind: 'rate', help: 'Interés real u oportunidad de tener la plata inmovilizada.' },
        { key: 'defaultDaysToSell', label: 'Días de venta por defecto', kind: 'int', help: 'Se usa cuando no se estima uno específico.' },
        { key: 'profitTaxPct', label: 'Impuesto sobre la utilidad', kind: 'rate', help: 'Definir con tu contador. Se informa aparte, no altera la puja máxima.' }
      ]
    },
    {
      title: 'Umbrales de decisión',
      helpKey: 'pujaMaxima',
      intro: 'Definen cuándo una operación vale la pena. Subirlos te hace más exigente y comprarás menos.',
      fields: [
        { key: 'minProfitAbs', label: 'Utilidad mínima', kind: 'money', help: 'Piso absoluto por operación. Manda en los vehículos baratos.' },
        { key: 'minRoiAnnual', label: 'ROI anual objetivo', kind: 'rate', help: 'Rentabilidad anual exigida al capital. Manda en los caros o lentos.' },
        { key: 'safetyMarginBase', label: 'Margen de seguridad base', kind: 'rate', help: 'Piso del colchón. Crece con la incertidumbre del vehículo.' },
        { key: 'safetyMarginMin', label: 'Margen de seguridad mínimo', kind: 'rate', help: 'Nunca baja de aquí.' },
        { key: 'safetyMarginMax', label: 'Margen de seguridad máximo', kind: 'rate', help: 'Nunca sube de aquí.' },
        { key: 'maxCapitalPerUnitPct', label: 'Máximo capital por unidad', kind: 'rate', help: 'Un solo auto malo no puede hundir el negocio.' },
        { key: 'maxPessimisticLossPct', label: 'Pérdida pesimista tolerada', kind: 'rate', help: 'Sobre este límite en el peor escenario, se bloquea la compra.' }
      ]
    },
    {
      title: 'Mercado',
      helpKey: 'brechaNegociacion',
      intro: 'Cómo se traduce lo publicado en los portales a un valor de venta realista.',
      fields: [
        { key: 'negotiationDiscountPct', label: 'Brecha de negociación', kind: 'rate', help: 'Diferencia entre el precio pedido y el pagado.' },
        { key: 'mileageAdjustPctPer1000Km', label: 'Ajuste por cada 1.000 km', kind: 'rate', help: 'Cuánto cambia el valor por diferencia de kilometraje.' },
        { key: 'yearAdjustPct', label: 'Ajuste por año', kind: 'rate', help: 'Cuánto cambia el valor por cada año de diferencia.' },
        { key: 'maxComparableAdjustmentPct', label: 'Tope de ajuste', kind: 'rate', help: 'Impide que un comparable lejano domine el resultado.' },
        { key: 'minComparables', label: 'Comparables mínimos', kind: 'int', help: 'Bajo este número, el análisis se bloquea.' }
      ]
    },
    {
      title: 'Escenarios',
      intro: 'Cuánto se castiga el caso malo y cuánto se premia el bueno.',
      fields: [
        { key: 'pessimisticSaleFactor', label: 'Venta en escenario pesimista', kind: 'rate', help: 'Fracción del valor conservador. 0,93 significa un castigo del 7%.' },
        { key: 'pessimisticDaysFactor', label: 'Días en escenario pesimista', kind: 'rate', help: 'Multiplicador. 1,6 significa 60% más de tiempo.' },
        { key: 'optimisticDaysFactor', label: 'Días en escenario optimista', kind: 'rate', help: 'Multiplicador. 0,7 significa 30% menos de tiempo.' }
      ]
    },
    {
      title: 'Semáforo',
      intro: 'Dónde están los cortes entre verde, amarillo y rojo.',
      fields: [
        { key: 'greenScoreThreshold', label: 'Puntaje para verde', kind: 'points', help: 'Score mínimo para recomendar comprar.' },
        { key: 'yellowScoreThreshold', label: 'Puntaje para amarillo', kind: 'points', help: 'Bajo este puntaje, siempre rojo.' },
        { key: 'greenPriceRatio', label: 'Holgura exigida para verde', kind: 'rate', help: 'El precio debe estar bajo esta fracción de la puja máxima.' }
      ]
    },
    {
      title: 'Alertas',
      intro: 'Cuándo el dashboard avisa que algo requiere atención.',
      fields: [
        { key: 'maxDaysInInventory', label: 'Días máximos en inventario', kind: 'int', help: 'Sobre este número, el vehículo se considera estancado.' },
        { key: 'listedTooLongDays', label: 'Días publicado sin vender', kind: 'int', help: 'Sugiere revisar el precio.' },
        { key: 'minMarginPct', label: 'Margen mínimo', kind: 'rate', help: 'Bajo este margen proyectado, avisa.' },
        { key: 'repairOverBudgetTolerancePct', label: 'Tolerancia de reparación', kind: 'rate', help: 'Cuánto puede excederse antes de avisar.' }
      ]
    },
    {
      title: 'Pesos del score',
      intro: 'Cuánto pesa cada componente. No es obligatorio que sumen 100: se normalizan al calcular.',
      fields: []
    }
  ];

  readonly weightFields: Field[] = [
    { key: 'profitability' as never, label: 'Rentabilidad', kind: 'points', help: '' },
    { key: 'bidHeadroom' as never, label: 'Holgura de puja', kind: 'points', help: '' },
    { key: 'liquidity' as never, label: 'Liquidez', kind: 'points', help: '' },
    { key: 'mechanicalRisk' as never, label: 'Riesgo mecánico', kind: 'points', help: '' },
    { key: 'documentRisk' as never, label: 'Riesgo documental', kind: 'points', help: '' },
    { key: 'estimateCertainty' as never, label: 'Certeza de la estimación', kind: 'points', help: '' },
    { key: 'evidenceQuality' as never, label: 'Calidad de la evidencia', kind: 'points', help: '' }
  ];

  readonly weights = signal<Record<string, number>>({});

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.http
      .get<ParameterSetResponse>(`${API_BASE_URL}/api/parameters/active`)
      .pipe(
        tap(() => this.loading.set(false)),
        catchError((err: { status?: number }) => {
          this.loading.set(false);
          this.error.set(err?.status === 0 ? 'No hay conexión con la API.' : 'No se pudieron cargar los parámetros.');
          return of(null);
        })
      )
      .subscribe((set) => {
        if (!set) return;
        this.current.set(set);
        this.hydrate(set.parameters);
      });

    this.http
      .get<ParameterVersionRow[]>(`${API_BASE_URL}/api/parameters/history`)
      .pipe(catchError(() => of([] as ParameterVersionRow[])))
      .subscribe((rows) => this.history.set(rows));
  }

  /** Los porcentajes se editan como número entero para no escribir 0,015 a mano. */
  displayValue(field: Field): number {
    const raw = this.values()[field.key as string] ?? 0;
    return field.kind === 'rate' ? Math.round(raw * 10000) / 100 : raw;
  }

  setValue(field: Field, event: Event): void {
    const input = Number((event.target as HTMLInputElement).value);
    const stored = field.kind === 'rate' ? input / 100 : input;

    this.values.update((v) => ({ ...v, [field.key as string]: stored }));
  }

  weightValue(field: Field): number {
    return this.weights()[field.key as string] ?? 0;
  }

  setWeight(field: Field, event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.weights.update((w) => ({ ...w, [field.key as string]: value }));
  }

  weightTotal(): number {
    return Object.values(this.weights()).reduce((a, b) => a + b, 0);
  }

  suffix(kind: FieldKind): string {
    return kind === 'rate' ? '%' : kind === 'money' ? 'CLP' : kind === 'int' ? 'días' : 'pts';
  }

  save(): void {
    this.saving.set(true);
    this.error.set(null);
    this.saved.set(null);

    const payload = {
      name: this.form.controls.name.value || null,
      note: this.form.controls.note.value || null,
      parameters: { ...this.values(), weights: this.weights() }
    };

    this.http.post<ParameterSetResponse>(`${API_BASE_URL}/api/parameters`, payload).subscribe({
      next: (set) => {
        this.saving.set(false);
        this.saved.set(`Guardado como "${set.name}". Los análisis anteriores no cambian.`);
        this.form.reset({ name: '', note: '' });
        this.load();
      },
      error: (err: { status?: number; error?: { errors?: Record<string, string[]> } }) => {
        this.saving.set(false);
        const validation = err?.error?.errors;
        this.error.set(
          validation
            ? Object.values(validation).flat().join(' ')
            : err?.status === 403
              ? 'Solo un administrador puede cambiar los parámetros.'
              : 'No se pudieron guardar los parámetros.'
        );
      }
    });
  }

  restoreDefaults(): void {
    this.http
      .get<AnalysisParameters>(`${API_BASE_URL}/api/parameters/defaults`)
      .subscribe((defaults) => {
        this.hydrate(defaults);
        this.saved.set('Valores de fábrica cargados. Aún no se han guardado.');
      });
  }

  private hydrate(parameters: AnalysisParameters): void {
    const { weights, ...rest } = parameters as AnalysisParameters & { weights: Record<string, number> };

    const numeric: Record<string, number> = {};
    for (const [key, value] of Object.entries(rest)) {
      if (typeof value === 'number') numeric[key] = value;
    }

    this.values.set(numeric);
    this.weights.set({ ...weights });
  }
}
