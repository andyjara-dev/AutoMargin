import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, debounceTime, of, switchMap, tap } from 'rxjs';

import { annualizedPct, clp, num, pct } from '../../core/format';
import {
  DamageCategory,
  DamageSeverity,
  DealAnalysisResult,
  DocumentRiskLevel,
  MechanicalInspectionLevel,
  ScenarioKind,
  SimulateAnalysisRequest
} from '../../core/models/analysis.models';
import { AnalysisService } from '../../core/services/analysis.service';

interface Option<T> {
  value: T;
  label: string;
}

interface ComparableFormValue {
  listedPrice: number;
  year: number;
  mileageKm: number;
  ageDays: number;
  source: string;
  isOutlier: boolean;
}

interface DamageFormValue {
  category: DamageCategory;
  severity: DamageSeverity;
  costMin: number;
  costExpected: number;
  costMax: number;
  description: string;
}

@Component({
  selector: 'app-deal-analyzer',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './deal-analyzer.html',
  styleUrl: './deal-analyzer.scss'
})
export class DealAnalyzer {
  private readonly fb = inject(FormBuilder);
  private readonly analysis = inject(AnalysisService);
  private readonly destroyRef = inject(DestroyRef);

  readonly clp = clp;
  readonly num = num;
  readonly pct = pct;
  readonly annualizedPct = annualizedPct;

  readonly result = signal<DealAnalysisResult | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly inspectionLevels: Option<MechanicalInspectionLevel>[] = [
    { value: 'None', label: 'No se pudo encender' },
    { value: 'VisualOnly', label: 'Solo inspección visual' },
    { value: 'EngineRun', label: 'Encendió el motor' },
    { value: 'TestDrive', label: 'Prueba de manejo' },
    { value: 'WorkshopReport', label: 'Informe de taller' }
  ];

  readonly documentRisks: Option<DocumentRiskLevel>[] = [
    { value: 'None', label: 'Sin riesgo detectado' },
    { value: 'Low', label: 'Bajo — trámites menores' },
    { value: 'Medium', label: 'Medio — antecedentes por verificar' },
    { value: 'High', label: 'Alto — gravamen o encargo' }
  ];

  readonly damageCategories: Option<DamageCategory>[] = [
    { value: 'Bodywork', label: 'Carrocería' },
    { value: 'Paint', label: 'Pintura' },
    { value: 'Mechanical', label: 'Mecánica' },
    { value: 'Electrical', label: 'Eléctrico' },
    { value: 'Tires', label: 'Neumáticos' },
    { value: 'Interior', label: 'Interior' },
    { value: 'Glass', label: 'Vidrios' },
    { value: 'Lights', label: 'Focos' },
    { value: 'Suspension', label: 'Suspensión' },
    { value: 'Structural', label: 'Estructural' },
    { value: 'Airbags', label: 'Airbags' },
    { value: 'Other', label: 'Otros' }
  ];

  readonly severities: Option<DamageSeverity>[] = [
    { value: 'Minor', label: 'Leve' },
    { value: 'Moderate', label: 'Moderado' },
    { value: 'Severe', label: 'Grave' },
    { value: 'Critical', label: 'Crítico' }
  ];

  readonly form = this.fb.nonNullable.group({
    year: [2018, [Validators.required, Validators.min(1900)]],
    mileageKm: [80000, [Validators.required, Validators.min(0)]],
    inspectionLevel: ['VisualOnly' as MechanicalInspectionLevel],
    documentRisk: ['None' as DocumentRiskLevel],
    transport: [150000, Validators.min(0)],
    detailing: [150000, Validators.min(0)],
    otherFixedCosts: [100000, Validators.min(0)],
    estimatedDaysToSell: [35, [Validators.required, Validators.min(1)]],
    currentAuctionPrice: [5400000, Validators.min(0)],
    totalCapital: [40000000, Validators.min(0)],
    comparables: this.fb.array([
      this.comparableGroup(12300000, 2018, 78000, 3, 'Chileautos'),
      this.comparableGroup(12400000, 2018, 82000, 5, 'Yapo'),
      this.comparableGroup(12900000, 2019, 70000, 7, 'Chileautos'),
      this.comparableGroup(11800000, 2017, 95000, 2, 'MercadoLibre'),
      this.comparableGroup(12500000, 2018, 85000, 10, 'Chileautos')
    ]),
    damages: this.fb.array([
      this.damageGroup('Bodywork', 'Moderate', 350000, 420000, 500000, 'Golpe lateral derecho'),
      this.damageGroup('Paint', 'Minor', 200000, 250000, 300000, ''),
      this.damageGroup('Tires', 'Moderate', 220000, 250000, 280000, 'Dos neumáticos al límite')
    ])
  });

  get comparables(): FormArray<FormGroup> {
    return this.form.controls.comparables as FormArray<FormGroup>;
  }

  get damages(): FormArray<FormGroup> {
    return this.form.controls.damages as FormArray<FormGroup>;
  }

  /** Componentes ordenados por lo que más restan: son las razones de que el score no sea mayor. */
  readonly weakestComponents = computed(() =>
    [...(this.result()?.score.components ?? [])].sort((a, b) => b.pointsLost - a.pointsLost)
  );

  /**
   * Posición del precio actual en la barra que va de 0 al punto de equilibrio.
   * Es la lectura visual de la decisión: dónde está el mercado respecto del techo.
   */
  readonly ladder = computed(() => {
    const r = this.result();
    if (!r || r.breakevenBid <= 0) return null;

    const scale = Math.max(r.breakevenBid, r.currentAuctionPrice) * 1.05;
    return {
      maxBidPct: (r.maxBid.maxBid / scale) * 100,
      breakevenPct: (r.breakevenBid / scale) * 100,
      currentPct: Math.min((r.currentAuctionPrice / scale) * 100, 100)
    };
  });

  constructor() {
    this.form.valueChanges
      .pipe(
        debounceTime(300),
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap(() =>
          this.analysis.simulate(this.buildRequest()).pipe(
            catchError((err) => {
              this.error.set(this.describeError(err));
              return of(null);
            })
          )
        ),
        takeUntilDestroyed()
      )
      .subscribe((result) => {
        this.loading.set(false);
        if (result) this.result.set(result);
      });

    this.recalculate();
  }

  recalculate(): void {
    this.loading.set(true);
    this.error.set(null);

    this.analysis
      .simulate(this.buildRequest())
      .pipe(
        catchError((err) => {
          this.error.set(this.describeError(err));
          return of(null);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((result) => {
        this.loading.set(false);
        if (result) this.result.set(result);
      });
  }

  addComparable(): void {
    this.comparables.push(this.comparableGroup(12000000, this.form.controls.year.value, this.form.controls.mileageKm.value, 0, ''));
  }

  removeComparable(index: number): void {
    this.comparables.removeAt(index);
  }

  addDamage(): void {
    this.damages.push(this.damageGroup('Bodywork', 'Minor', 0, 0, 0, ''));
  }

  removeDamage(index: number): void {
    this.damages.removeAt(index);
  }

  /** Lleva el precio evaluado exactamente a la puja máxima, para ver el piso de rentabilidad aceptable. */
  useMaxBidAsPrice(): void {
    const maxBid = this.result()?.maxBid.maxBid;
    if (maxBid !== undefined) this.form.controls.currentAuctionPrice.setValue(maxBid);
  }

  scenarioTone(kind: ScenarioKind): string {
    return kind === 'Optimistic' ? 'good' : kind === 'Pessimistic' ? 'bad' : 'neutral';
  }

  private buildRequest(): SimulateAnalysisRequest {
    const v = this.form.getRawValue();
    const comparables = this.comparables.getRawValue() as ComparableFormValue[];
    const damages = this.damages.getRawValue() as DamageFormValue[];

    return {
      year: v.year,
      mileageKm: v.mileageKm,
      comparables: comparables.map((c) => ({
        listedPrice: Number(c.listedPrice),
        year: Number(c.year),
        mileageKm: Number(c.mileageKm),
        ageDays: Number(c.ageDays),
        source: c.source || null,
        isOutlier: Boolean(c.isOutlier)
      })),
      damages: damages.map((d) => ({
        category: d.category,
        severity: d.severity,
        costMin: Number(d.costMin),
        costExpected: Number(d.costExpected),
        costMax: Number(d.costMax),
        description: d.description || null,
        source: 'Manual' as const
      })),
      inspectionLevel: v.inspectionLevel,
      documentRisk: v.documentRisk,
      transport: v.transport,
      detailing: v.detailing,
      otherFixedCosts: v.otherFixedCosts,
      estimatedDaysToSell: v.estimatedDaysToSell,
      currentAuctionPrice: v.currentAuctionPrice,
      totalCapital: v.totalCapital
    };
  }

  private comparableGroup(
    listedPrice: number,
    year: number,
    mileageKm: number,
    ageDays: number,
    source: string
  ): FormGroup {
    return this.fb.nonNullable.group({
      listedPrice: [listedPrice, [Validators.required, Validators.min(1)]],
      year: [year, [Validators.required, Validators.min(1900)]],
      mileageKm: [mileageKm, [Validators.required, Validators.min(0)]],
      ageDays: [ageDays, Validators.min(0)],
      source: [source],
      isOutlier: [false]
    });
  }

  private damageGroup(
    category: DamageCategory,
    severity: DamageSeverity,
    costMin: number,
    costExpected: number,
    costMax: number,
    description: string
  ): FormGroup {
    return this.fb.nonNullable.group({
      category: [category],
      severity: [severity],
      costMin: [costMin, Validators.min(0)],
      costExpected: [costExpected, Validators.min(0)],
      costMax: [costMax, Validators.min(0)],
      description: [description]
    });
  }

  private describeError(err: unknown): string {
    const problem = err as { status?: number; error?: { errors?: Record<string, string[]> } };

    if (problem?.status === 0) {
      return 'No hay conexión con la API. Verifica que Remates.Api esté corriendo en http://localhost:5044.';
    }

    const validation = problem?.error?.errors;
    if (validation) {
      return Object.values(validation).flat().join(' ');
    }

    return 'No se pudo calcular el análisis.';
  }
}
