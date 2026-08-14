import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../api-config';
import {
  AnalysisParameters,
  DealAnalysisResult,
  SimulateAnalysisRequest
} from '../models/analysis.models';

@Injectable({ providedIn: 'root' })
export class AnalysisService {
  private readonly http = inject(HttpClient);

  /** Cálculo sin persistencia. Todos los números provienen del motor determinístico del backend. */
  simulate(request: SimulateAnalysisRequest): Observable<DealAnalysisResult> {
    return this.http.post<DealAnalysisResult>(`${API_BASE_URL}/api/analysis/simulate`, request);
  }

  defaultParameters(): Observable<AnalysisParameters> {
    return this.http.get<AnalysisParameters>(`${API_BASE_URL}/api/analysis/default-parameters`);
  }
}
