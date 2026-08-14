import { Component, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { catchError, of, tap } from 'rxjs';

import { API_BASE_URL } from '../../core/api-config';
import { annualizedPct, clp, num, pct } from '../../core/format';
import {
  ALERT_TYPE_LABELS,
  Alert,
  DashboardSummary
} from '../../core/models/dashboard.models';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard {
  private readonly http = inject(HttpClient);

  readonly clp = clp;
  readonly num = num;
  readonly pct = pct;
  readonly annualizedPct = annualizedPct;
  readonly alertLabels = ALERT_TYPE_LABELS;

  readonly data = signal<DashboardSummary | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  /**
   * Con pocas operaciones cerradas los promedios no son concluyentes, y conviene decirlo
   * antes de que alguien tome una decisión apoyándose en ellos.
   */
  readonly hasEnoughHistory = computed(() => (this.data()?.closedOperations ?? 0) >= 10);

  readonly criticalCount = computed(
    () => this.data()?.alerts.filter((a) => a.severity === 'Critical').length ?? 0
  );

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.http
      .get<DashboardSummary>(`${API_BASE_URL}/api/dashboard/summary`)
      .pipe(
        tap(() => this.loading.set(false)),
        catchError((err: { status?: number }) => {
          this.loading.set(false);
          this.error.set(
            err?.status === 0
              ? 'No hay conexión con la API.'
              : 'No se pudo cargar el estado del negocio.'
          );
          return of(null);
        })
      )
      .subscribe((summary) => {
        if (summary) this.data.set(summary);
      });
  }

  alertClass(alert: Alert): string {
    return `alert alert--${alert.severity.toLowerCase()}`;
  }

  lightClass(light: string): string {
    return `light light--${light.toLowerCase()}`;
  }
}
