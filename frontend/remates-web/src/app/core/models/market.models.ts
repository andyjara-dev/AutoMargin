/** Datos reconocidos en el texto de un aviso pegado. */
export interface ParsedListing {
  price?: number | null;
  year?: number | null;
  mileageKm?: number | null;
  make?: string | null;
  model?: string | null;
  transmission?: string | null;
  fuel?: string | null;
  region?: string | null;
  url?: string | null;
  /** Campos que no se reconocieron y hay que completar a mano. */
  missing: string[];
  isUsable: boolean;
}

/** Un aviso encontrado, ya normalizado al formato del sistema. */
export interface MarketSearchResult {
  source: string;
  listedPrice: number;
  year: number;
  mileageKm?: number | null;
  title?: string | null;
  url?: string | null;
  region?: string | null;
  publishedAt?: string | null;
  isUsable: boolean;
}

/** Cómo le fue a cada fuente por separado. Una caída no invalida a las demás. */
export interface SourceStatus {
  name: string;
  configured: boolean;
  results: number;
  problem?: string | null;
}

export interface MarketSearchResponse {
  results: MarketSearchResult[];
  sources: SourceStatus[];
  fromCache: boolean;
}

export interface ImportComparablesResponse {
  imported: number;
  skipped: number;
  message: string;
}

export interface MarketSearchFilters {
  make?: string;
  model?: string;
  year?: number;
  yearTolerance?: number;
  region?: string;
  limit?: number;
}
