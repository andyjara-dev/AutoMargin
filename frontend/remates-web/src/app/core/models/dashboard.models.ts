export type AlertType =
  | 'StaleInventory' | 'PriceNeedsAdjustment' | 'RepairOverBudget' | 'LowMargin'
  | 'CapitalConcentration' | 'PurchasedWithoutAnalysis' | 'LowAvailableCapital';

export type AlertSeverity = 'Info' | 'Warning' | 'Critical';

export interface Alert {
  type: AlertType;
  severity: AlertSeverity;
  message: string;
  suggestion: string;
  vehicleId?: number | null;
  vehicleLabel?: string | null;
  magnitude: number;
}

export interface CapitalSummary {
  totalContributed: number;
  available: number;
  immobilized: number;
  immobilizedPct: number;
}

export interface InventorySummary {
  inInventory: number;
  sold: number;
  analyzing: number;
  inventoryCostValue: number;
  inventoryExpectedValue: number;
  potentialProfit: number;
  averageDaysInInventory: number;
  unvaluedCount: number;
  unvaluedCost: number;
}

export interface ProfitSummary {
  realizedProfitCash: number;
  realizedProfitEconomic: number;
  averageRoiEconomic: number;
  averageMarginPct: number;
  averageDaysToSell: number;
  profitLast30Days: number;
  salesLast30Days: number;
}

export interface ModelPerformance {
  model: string;
  sales: number;
  totalProfit: number;
  averageProfit: number;
  averageRoi: number;
  averageDays: number;
}

export interface OpportunityRow {
  vehicleId: number;
  label: string;
  maxBid: number;
  currentPrice: number;
  headroom: number;
  score: number;
  trafficLight: 'Green' | 'Yellow' | 'Red';
  roiAnnualized: number;
  estimatedDays: number;
  analyzedAt: string;
}

export interface DashboardSummary {
  capital: CapitalSummary;
  inventory: InventorySummary;
  profit: ProfitSummary;
  bestModels: ModelPerformance[];
  worstModels: ModelPerformance[];
  opportunities: OpportunityRow[];
  alerts: Alert[];
  closedOperations: number;
}

export const ALERT_TYPE_LABELS: Record<AlertType, string> = {
  StaleInventory: 'Inventario estancado',
  PriceNeedsAdjustment: 'Revisar precio',
  RepairOverBudget: 'Reparación sobre presupuesto',
  LowMargin: 'Margen bajo',
  CapitalConcentration: 'Concentración de capital',
  PurchasedWithoutAnalysis: 'Compra sin análisis',
  LowAvailableCapital: 'Capital disponible bajo'
};
