/** Contrato con Remates.Api. Los enums viajan como texto. */

export type DamageCategory =
  | 'Bodywork' | 'Paint' | 'Mechanical' | 'Electrical' | 'Tires'
  | 'Interior' | 'Glass' | 'Lights' | 'Suspension' | 'Structural'
  | 'Airbags' | 'Other';

export type DamageSeverity = 'Minor' | 'Moderate' | 'Severe' | 'Critical';
export type DamageSource = 'Manual' | 'Ai' | 'Workshop';

export type MechanicalInspectionLevel =
  | 'None' | 'VisualOnly' | 'EngineRun' | 'TestDrive' | 'WorkshopReport';

export type DocumentRiskLevel = 'None' | 'Low' | 'Medium' | 'High';
export type TrafficLight = 'Green' | 'Yellow' | 'Red';
export type ScenarioKind = 'Optimistic' | 'Expected' | 'Pessimistic';

export type GateCode =
  | 'PriceAboveMaxBid' | 'RoiBelowMinimum' | 'InsufficientMarketData'
  | 'CriticalDocumentRisk' | 'PessimisticLossExceedsLimit'
  | 'CapitalConcentration' | 'NotViable';

// ---------- Request ----------

export interface ComparableDto {
  listedPrice: number;
  year: number;
  mileageKm: number;
  ageDays: number;
  source?: string | null;
  url?: string | null;
  isOutlier: boolean;
}

export interface DamageDto {
  category: DamageCategory;
  severity: DamageSeverity;
  costMin: number;
  costExpected: number;
  costMax: number;
  description?: string | null;
  source: DamageSource;
  confidence?: number | null;
}

export interface ManualValuationDto {
  conservative: number;
  expected?: number | null;
  optimistic?: number | null;
}

export interface SimulateAnalysisRequest {
  year: number;
  mileageKm: number;
  comparables: ComparableDto[];
  manualValuation?: ManualValuationDto | null;
  damages: DamageDto[];
  inspectionLevel: MechanicalInspectionLevel;
  documentRisk: DocumentRiskLevel;
  transport?: number | null;
  detailing?: number | null;
  otherFixedCosts: number;
  estimatedDaysToSell?: number | null;
  currentAuctionPrice: number;
  totalCapital: number;
  parameters?: Partial<AnalysisParameters> | null;
}

// ---------- Parámetros ----------

export interface AnalysisParameters {
  commissionPct: number;
  commissionHasVat: boolean;
  vatPct: number;
  adminFeePct: number;
  transferTaxPct: number;
  transferFixed: number;
  transportDefault: number;
  detailingDefault: number;
  adminFeeFixed: number;
  contingencyPct: number;
  marketingPct: number;
  warrantyProvisionPct: number;
  capitalCostMonthlyPct: number;
  defaultDaysToSell: number;
  minProfitAbs: number;
  minRoiAnnual: number;
  safetyMarginBase: number;
  safetyMarginMin: number;
  safetyMarginMax: number;
  maxCapitalPerUnitPct: number;
  maxPessimisticLossPct: number;
  negotiationDiscountPct: number;
  mileageAdjustPctPer1000Km: number;
  yearAdjustPct: number;
  maxComparableAdjustmentPct: number;
  minComparables: number;
  pessimisticSaleFactor: number;
  pessimisticDaysFactor: number;
  optimisticDaysFactor: number;
  profitTaxPct: number;
  greenScoreThreshold: number;
  yellowScoreThreshold: number;
  greenPriceRatio: number;
}

// ---------- Response ----------

export interface AdjustedComparable {
  source: ComparableDto;
  mileageAdjustment: number;
  yearAdjustment: number;
  totalAdjustment: number;
  adjustedPrice: number;
  adjustmentWasCapped: boolean;
}

export interface ValuationResult {
  optimistic: number;
  expected: number;
  conservative: number;
  conservativeBeforeDiscount: number;
  dispersionPct: number;
  comparableCount: number;
  excludedCount: number;
  averageAgeDays: number;
  negotiationDiscountPct: number;
  hasEnoughEvidence: boolean;
  adjusted: AdjustedComparable[];
}

export interface RepairCategoryTotal {
  category: DamageCategory;
  min: number;
  expected: number;
  max: number;
  itemCount: number;
}

export interface RepairEstimate {
  totalMin: number;
  totalExpected: number;
  totalMax: number;
  byCategory: RepairCategoryTotal[];
  uncertaintyRatio: number;
  mechanicalRiskScore: number;
  hasStructuralDamage: boolean;
  hasAirbagDamage: boolean;
  containsUnconfirmedAiEstimates: boolean;
}

export interface CostLine {
  key: string;
  label: string;
  amount: number;
  isProportionalToBid: boolean;
}

export interface CostStructure {
  grossSaleValue: number;
  netSaleValue: number;
  fixedCosts: number;
  proportionalRate: number;
  capitalFactor: number;
  daysToSell: number;
  profitTaxPct: number;
  fixedCostLines: CostLine[];
  saleDeductionLines: CostLine[];
}

export interface SafetyMarginComponent {
  key: string;
  label: string;
  rawValue: number;
  contribution: number;
}

export interface MaxBidResult {
  breakevenBid: number;
  requiredProfit: number;
  requiredProfitDriver: 'min_profit_abs' | 'roi_annual';
  theoreticalMaxBid: number;
  safetyMarginPct: number;
  safetyMarginBreakdown: SafetyMarginComponent[];
  maxBid: number;
  isViable: boolean;
}

export interface DealMetrics {
  bidPrice: number;
  proportionalCosts: number;
  cashDeployed: number;
  capitalCost: number;
  totalCost: number;
  profit: number;
  profitAfterTax: number;
  roiSimple: number;
  roiAnnualized: number;
  marginPct: number;
  daysToSell: number;
}

export interface ScenarioResult {
  kind: ScenarioKind;
  label: string;
  saleValue: number;
  repairCost: number;
  daysToSell: number;
  metrics: DealMetrics;
}

export interface ScoreComponent {
  key: string;
  label: string;
  weight: number;
  normalized: number;
  points: number;
  pointsLost: number;
  explanation: string;
}

export interface TriggeredGate {
  code: GateCode;
  message: string;
}

export interface ScoreResult {
  score: number;
  trafficLight: TrafficLight;
  recommendation: string;
  components: ScoreComponent[];
  gates: TriggeredGate[];
  strengths: string[];
  weaknesses: string[];
}

export interface DealAnalysisResult {
  financialEngineVersion: string;
  scoringEngineVersion: string;
  valuation: ValuationResult;
  repair: RepairEstimate;
  costStructure: CostStructure;
  maxBid: MaxBidResult;
  metricsAtCurrentPrice: DealMetrics;
  metricsAtMaxBid: DealMetrics;
  scenarios: ScenarioResult[];
  score: ScoreResult;
  breakevenBid: number;
  currentAuctionPrice: number;
  headroom: number;
  disclaimers: string[];
}
