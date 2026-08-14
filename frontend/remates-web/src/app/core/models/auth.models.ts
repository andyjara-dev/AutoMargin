export interface AuthenticatedUser {
  id: number;
  email: string;
  fullName?: string | null;
  roles: string[];
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  user: AuthenticatedUser;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export type VehicleStatus =
  | 'Detected' | 'Analyzing' | 'Bidding' | 'Won' | 'Lost' | 'Purchased'
  | 'InTransport' | 'InRepair' | 'Ready' | 'Listed' | 'Reserved' | 'Sold' | 'Discarded';

export interface VehicleSummary {
  id: number;
  label: string;
  year: number;
  mileageKm: number;
  status: VehicleStatus;
  region?: string | null;
  comparableCount: number;
  damageCount: number;
  lastMaxBid?: number | null;
  lastScore?: number | null;
  lastTrafficLight?: 'Green' | 'Yellow' | 'Red' | null;
  lastAnalyzedAt?: string | null;
}

export const VEHICLE_STATUS_LABELS: Record<VehicleStatus, string> = {
  Detected: 'Detectado',
  Analyzing: 'Analizando',
  Bidding: 'En remate',
  Won: 'Adjudicado',
  Lost: 'Puja perdida',
  Purchased: 'Comprado',
  InTransport: 'En transporte',
  InRepair: 'En reparación',
  Ready: 'Listo para venta',
  Listed: 'Publicado',
  Reserved: 'Reservado',
  Sold: 'Vendido',
  Discarded: 'Descartado'
};
