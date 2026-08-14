import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../api-config';
import { VehicleStatus, VehicleSummary } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class VehiclesService {
  private readonly http = inject(HttpClient);

  list(filters: { status?: VehicleStatus; search?: string } = {}): Observable<VehicleSummary[]> {
    let params = new HttpParams();
    if (filters.status) params = params.set('status', filters.status);
    if (filters.search) params = params.set('search', filters.search);

    return this.http.get<VehicleSummary[]>(`${API_BASE_URL}/api/vehicles`, { params });
  }
}
