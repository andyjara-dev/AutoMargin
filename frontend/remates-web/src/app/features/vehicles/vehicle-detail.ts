import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, of, tap } from 'rxjs';

import { annualizedPct, clp, num, pct } from '../../core/format';
import { describeHttpError } from '../../core/http-error';
import { VEHICLE_STATUS_LABELS } from '../../core/models/auth.models';
import {
  EXPENSE_CATEGORY_LABELS,
  ExpenseCategory,
  ExpenseResponse,
  VehicleFinancials
} from '../../core/models/inventory.models';
import { InventoryService } from '../../core/services/inventory.service';
import { HelpTip } from '../../shared/help-tip';

@Component({
  selector: 'app-vehicle-detail',
  imports: [ReactiveFormsModule, RouterLink, HelpTip],
  templateUrl: './vehicle-detail.html',
  styleUrl: './vehicle-detail.scss'
})
export class VehicleDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly inventory = inject(InventoryService);
  private readonly fb = inject(FormBuilder);

  readonly clp = clp;
  readonly num = num;
  readonly pct = pct;
  readonly annualizedPct = annualizedPct;
  readonly statusLabels = VEHICLE_STATUS_LABELS;
  readonly categoryLabels = EXPENSE_CATEGORY_LABELS;

  readonly vehicleId = Number(this.route.snapshot.paramMap.get('id'));

  readonly financials = signal<VehicleFinancials | null>(null);
  readonly expenses = signal<ExpenseResponse[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly saleError = signal<string | null>(null);

  readonly categories = Object.entries(EXPENSE_CATEGORY_LABELS) as [ExpenseCategory, string][];

  /** El vehículo ya se vendió: los números dejan de ser provisionales. */
  readonly isClosed = computed(() => this.financials()?.performance.isClosed ?? false);

  readonly expenseForm = this.fb.nonNullable.group({
    category: ['Repair' as ExpenseCategory],
    amount: [0, [Validators.required, Validators.min(1)]],
    description: [''],
    supplier: ['']
  });

  readonly saleForm = this.fb.nonNullable.group({
    salePrice: [0, [Validators.required, Validators.min(1)]],
    saleCosts: [0, Validators.min(0)],
    buyerName: [''],
    paymentMethod: ['Transferencia']
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.inventory
      .financials(this.vehicleId)
      .pipe(
        tap(() => this.loading.set(false)),
        catchError((err) => {
          this.loading.set(false);
          this.error.set(this.describe(err));
          return of(null);
        })
      )
      .subscribe((data) => {
        if (data) this.financials.set(data);
      });

    this.inventory
      .expenses(this.vehicleId)
      .pipe(catchError(() => of([] as ExpenseResponse[])))
      .subscribe((items) => this.expenses.set(items));
  }

  addExpense(): void {
    if (this.expenseForm.invalid) {
      this.expenseForm.markAllAsTouched();
      return;
    }

    this.inventory.addExpense(this.vehicleId, this.expenseForm.getRawValue()).subscribe({
      next: () => {
        this.expenseForm.patchValue({ amount: 0, description: '', supplier: '' });
        this.load();
      },
      error: (err) => this.error.set(this.describe(err))
    });
  }

  removeExpense(id: number): void {
    this.inventory.deleteExpense(id).subscribe({
      next: () => this.load(),
      error: (err) => this.error.set(this.describe(err))
    });
  }

  registerSale(): void {
    if (this.saleForm.invalid) {
      this.saleForm.markAllAsTouched();
      return;
    }

    this.saleError.set(null);

    this.inventory.registerSale(this.vehicleId, this.saleForm.getRawValue()).subscribe({
      next: () => this.load(),
      error: (err) => this.saleError.set(this.describe(err))
    });
  }

  /** Signo de una desviación, para colorearla: gastar de más es malo, de menos es bueno. */
  varianceTone(value: number | null | undefined): string {
    if (value === null || value === undefined || value === 0) return '';
    return value > 0 ? 'bad' : 'good';
  }

  private describe(err: unknown): string {
    return describeHttpError(err);
  }
}
