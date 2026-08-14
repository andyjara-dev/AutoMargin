import { VehicleStatus } from './auth.models';

export type ExpenseCategory =
  | 'AuctionFee' | 'Transport' | 'Repair' | 'Parts' | 'Labor' | 'Detailing'
  | 'Transfer' | 'Storage' | 'Marketing' | 'Warranty' | 'Other';

export const EXPENSE_CATEGORY_LABELS: Record<ExpenseCategory, string> = {
  AuctionFee: 'Gastos de remate',
  Transport: 'Transporte',
  Repair: 'Reparación',
  Parts: 'Repuestos',
  Labor: 'Mano de obra',
  Detailing: 'Detailing',
  Transfer: 'Transferencia',
  Storage: 'Bodegaje',
  Marketing: 'Publicación',
  Warranty: 'Garantía',
  Other: 'Otros'
};

export interface RealPerformance {
  totalCashInvested: number;
  capitalCost: number;
  netSaleProceeds: number;
  profitCash: number;
  profitEconomic: number;
  profitAfterTax: number;
  roiCash: number;
  roiEconomic: number;
  roiAnnualized: number;
  marginPct: number;
  daysInInventory: number;
  isClosed: boolean;
}

export interface ExpenseByCategory {
  category: ExpenseCategory;
  actual: number;
  budgeted?: number | null;
  variance?: number | null;
  variancePct?: number | null;
  count: number;
}

export interface RepairSummary {
  budgeted: number;
  actual: number;
  variance: number;
  variancePct?: number | null;
  overBudget: boolean;
}

export interface PredictionComparison {
  predictedSaleValue: number;
  actualSaleValue: number;
  predictedRepairCost: number;
  actualRepairCost: number;
  predictedDays: number;
  actualDays: number;
  predictedProfit: number;
  actualProfit: number;
  saleValueErrorPct: number;
  repairCostErrorPct: number;
  daysErrorPct: number;
  profitErrorPct: number;
  underPerformed: boolean;
}

export interface VehicleFinancials {
  vehicleId: number;
  label: string;
  status: VehicleStatus;
  hammerPrice?: number | null;
  commissionPaid?: number | null;
  totalExpenses: number;
  totalBudgeted: number;
  expensesByCategory: ExpenseByCategory[];
  repair: RepairSummary;
  listPrice?: number | null;
  salePrice?: number | null;
  purchaseDate?: string | null;
  saleDate?: string | null;
  performance: RealPerformance;
  comparison?: PredictionComparison | null;
}

export interface ExpenseResponse {
  id: number;
  vehicleId: number;
  category: ExpenseCategory;
  amount: number;
  expenseDate: string;
  description?: string | null;
  supplier?: string | null;
  documentRef?: string | null;
  budgetedAmount?: number | null;
}

export interface RegisterExpenseRequest {
  category: ExpenseCategory;
  amount: number;
  description?: string | null;
  supplier?: string | null;
}

export interface RegisterSaleRequest {
  salePrice: number;
  saleCosts: number;
  buyerName?: string | null;
  paymentMethod?: string | null;
}
