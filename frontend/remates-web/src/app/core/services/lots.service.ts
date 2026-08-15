import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, forkJoin, of, switchMap } from 'rxjs';

import { API_BASE_URL } from '../api-config';
import { VehicleStatus } from '../models/auth.models';

/** Lo que hace falta para dejar un lote guardado y listo para el día del remate. */
export interface SaveLotRequest {
  displayName: string;
  year: number;
  mileageKm: number;
  region?: string | null;
  conditionNotes?: string | null;

  comparables: {
    listedPrice: number;
    year: number;
    mileageKm: number;
    source: string;
    ageDays: number;
  }[];

  damages: {
    category: string;
    severity: string;
    costMin: number;
    costExpected: number;
    costMax: number;
    description: string;
  }[];

  analysis: {
    currentAuctionPrice: number;
    transport: number;
    detailing: number;
    otherFixedCosts: number;
    estimatedDaysToSell: number;
    totalCapital: number;
  };
}

@Injectable({ providedIn: 'root' })
export class LotsService {
  private readonly http = inject(HttpClient);

  /**
   * Guarda el lote completo: el vehículo, sus comparables, sus daños y el análisis.
   *
   * Va en ese orden a propósito. El análisis lee los comparables y los daños desde la base, así
   * que si se ejecutara antes se guardaría un lote sin evidencia y con la puja máxima bloqueada
   * por falta de comparables.
   */
  save(request: SaveLotRequest): Observable<{ vehicleId: number }> {
    return this.http
      .post<{ id: number }>(`${API_BASE_URL}/api/vehicles`, {
        displayName: request.displayName,
        year: request.year,
        mileageKm: request.mileageKm,
        region: request.region ?? null,
        conditionNotes: request.conditionNotes ?? null
      })
      .pipe(
        switchMap((vehicle) => this.attachEvidence(vehicle.id, request)),
        switchMap((vehicleId) =>
          this.http
            .post(`${API_BASE_URL}/api/vehicles/${vehicleId}/analysis`, request.analysis)
            .pipe(switchMap(() => of({ vehicleId })))
        )
      );
  }

  private attachEvidence(vehicleId: number, request: SaveLotRequest): Observable<number> {
    const calls: Observable<unknown>[] = [
      ...request.comparables.map((c) =>
        this.http.post(`${API_BASE_URL}/api/vehicles/${vehicleId}/comparables`, {
          listedPrice: c.listedPrice,
          year: c.year,
          mileageKm: c.mileageKm,
          source: c.source,
          observedAt: this.observedAt(c.ageDays)
        })
      ),
      ...request.damages.map((d) =>
        this.http.post(`${API_BASE_URL}/api/vehicles/${vehicleId}/damages`, d)
      )
    ];

    return calls.length === 0
      ? of(vehicleId)
      : forkJoin(calls).pipe(switchMap(() => of(vehicleId)));
  }

  /** El analizador maneja antigüedad en días; la base guarda la fecha de observación. */
  private observedAt(ageDays: number): string {
    const date = new Date();
    date.setDate(date.getDate() - Math.max(0, ageDays));
    return date.toISOString();
  }

  changeStatus(vehicleId: number, status: VehicleStatus, note?: string): Observable<void> {
    return this.http.post<void>(`${API_BASE_URL}/api/vehicles/${vehicleId}/status`, {
      status,
      note: note ?? null
    });
  }

  remove(vehicleId: number): Observable<void> {
    return this.http.delete<void>(`${API_BASE_URL}/api/vehicles/${vehicleId}`);
  }
}
