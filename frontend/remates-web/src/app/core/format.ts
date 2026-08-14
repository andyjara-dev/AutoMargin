const clpFormatter = new Intl.NumberFormat('es-CL', {
  style: 'currency',
  currency: 'CLP',
  maximumFractionDigits: 0
});

const numberFormatter = new Intl.NumberFormat('es-CL', { maximumFractionDigits: 0 });

export function clp(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return clpFormatter.format(value);
}

export function num(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return numberFormatter.format(value);
}

export function pct(value: number | null | undefined, decimals = 1): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return `${(value * 100).toFixed(decimals)}%`;
}

/**
 * La rentabilidad anualizada de una operación de 20 días alcanza cifras de cuatro dígitos.
 * Es matemáticamente correcta, pero mostrarla cruda no comunica nada: se acota la presentación.
 */
export function annualizedPct(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  if (value >= 10) return '> 1.000%';
  return pct(value, 0);
}
