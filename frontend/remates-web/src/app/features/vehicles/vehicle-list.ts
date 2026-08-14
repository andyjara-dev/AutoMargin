import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, of, startWith, switchMap, tap } from 'rxjs';

import { clp, num } from '../../core/format';
import { describeHttpError } from '../../core/http-error';
import {
  VEHICLE_STATUS_LABELS,
  VehicleStatus,
  VehicleSummary
} from '../../core/models/auth.models';
import { VehiclesService } from '../../core/services/vehicles.service';

@Component({
  selector: 'app-vehicle-list',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './vehicle-list.html',
  styleUrl: './vehicle-list.scss'
})
export class VehicleList {
  private readonly vehicles = inject(VehiclesService);

  readonly clp = clp;
  readonly num = num;
  readonly statusLabels = VEHICLE_STATUS_LABELS;

  readonly items = signal<VehicleSummary[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly search = new FormControl('', { nonNullable: true });
  readonly statusFilter = new FormControl<VehicleStatus | ''>('', { nonNullable: true });

  readonly statuses = Object.entries(VEHICLE_STATUS_LABELS) as [VehicleStatus, string][];

  constructor() {
    this.search.valueChanges
      .pipe(debounceTime(300), startWith(''), takeUntilDestroyed())
      .subscribe(() => this.load());

    this.statusFilter.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.load());
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.vehicles
      .list({
        search: this.search.value || undefined,
        status: this.statusFilter.value || undefined
      })
      .pipe(
        tap(() => this.loading.set(false)),
        catchError((err) => {
          this.loading.set(false);
          this.error.set(this.describe(err));
          return of([] as VehicleSummary[]);
        })
      )
      .subscribe((items) => this.items.set(items));
  }

  lightClass(light: string | null | undefined): string {
    return light ? `light light--${light.toLowerCase()}` : 'light light--none';
  }

  private describe(err: unknown): string {
    return describeHttpError(err, 'No se pudo cargar el listado de vehiculos.');
  }
}
