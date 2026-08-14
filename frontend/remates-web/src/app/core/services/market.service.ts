import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../api-config';
import {
  ImportComparablesResponse,
  MarketSearchFilters,
  MarketSearchResponse,
  MarketSearchResult,
  ParsedListing
} from '../models/market.models';

@Injectable({ providedIn: 'root' })
export class MarketService {
  private readonly http = inject(HttpClient);

  /** Extrae los datos de un aviso pegado como texto. */
  parse(text: string): Observable<ParsedListing> {
    return this.http.post<ParsedListing>(`${API_BASE_URL}/api/market/parse`, { text });
  }

  search(filters: MarketSearchFilters): Observable<MarketSearchResponse> {
    let params = new HttpParams();
    if (filters.make) params = params.set('make', filters.make);
    if (filters.model) params = params.set('model', filters.model);
    if (filters.year) params = params.set('year', filters.year);
    if (filters.yearTolerance != null) params = params.set('yearTolerance', filters.yearTolerance);
    if (filters.region) params = params.set('region', filters.region);
    if (filters.limit) params = params.set('limit', filters.limit);

    return this.http.get<MarketSearchResponse>(`${API_BASE_URL}/api/market/search`, { params });
  }

  /** Busca usando los datos que el vehículo ya tiene cargados. */
  searchForVehicle(vehicleId: number, limit = 25): Observable<MarketSearchResponse> {
    return this.http.get<MarketSearchResponse>(
      `${API_BASE_URL}/api/market/search/vehicle/${vehicleId}`,
      { params: new HttpParams().set('limit', limit) }
    );
  }

  import(vehicleId: number, results: MarketSearchResult[]): Observable<ImportComparablesResponse> {
    return this.http.post<ImportComparablesResponse>(
      `${API_BASE_URL}/api/market/import/${vehicleId}`,
      { results }
    );
  }
}
