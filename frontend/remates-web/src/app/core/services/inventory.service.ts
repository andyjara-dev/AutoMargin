import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../api-config';
import {
  ExpenseResponse,
  RegisterExpenseRequest,
  RegisterSaleRequest,
  VehicleFinancials
} from '../models/inventory.models';

@Injectable({ providedIn: 'root' })
export class InventoryService {
  private readonly http = inject(HttpClient);

  financials(vehicleId: number): Observable<VehicleFinancials> {
    return this.http.get<VehicleFinancials>(`${API_BASE_URL}/api/vehicles/${vehicleId}/financials`);
  }

  expenses(vehicleId: number): Observable<ExpenseResponse[]> {
    return this.http.get<ExpenseResponse[]>(`${API_BASE_URL}/api/vehicles/${vehicleId}/expenses`);
  }

  addExpense(vehicleId: number, request: RegisterExpenseRequest): Observable<ExpenseResponse> {
    return this.http.post<ExpenseResponse>(
      `${API_BASE_URL}/api/vehicles/${vehicleId}/expenses`, request);
  }

  deleteExpense(expenseId: number): Observable<void> {
    return this.http.delete<void>(`${API_BASE_URL}/api/expenses/${expenseId}`);
  }

  registerSale(vehicleId: number, request: RegisterSaleRequest): Observable<unknown> {
    return this.http.post(`${API_BASE_URL}/api/vehicles/${vehicleId}/sale`, request);
  }
}
