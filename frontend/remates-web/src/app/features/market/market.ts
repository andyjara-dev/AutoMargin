import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';

import { clp, num, pct } from '../../core/format';
import { describeHttpError } from '../../core/http-error';
import { VehicleSummary } from '../../core/models/auth.models';
import {
  MarketSearchResult,
  ParsedListing,
  SourceStatus
} from '../../core/models/market.models';
import { MarketService } from '../../core/services/market.service';
import { VehiclesService } from '../../core/services/vehicles.service';

/** Resumen de precios de los avisos elegidos. */
interface PriceStats {
  count: number;
  min: number;
  max: number;
  average: number;
  median: number;
  /** Precio que más se repite. Nulo cuando todos los avisos valen distinto. */
  mode: number | null;
  /** Cuántas veces se repite la moda. */
  modeCount: number;
  /** Cuánto se aparta el promedio de la mediana, en proporción a la mediana. */
  skew: number;
}

/** Un aviso en la pantalla, con su estado de selección y de dónde vino. */
interface Candidate {
  result: MarketSearchResult;
  selected: boolean;
  /** Pegado a mano o traído de una fuente. Cambia el sello que se guarda. */
  manual: boolean;
}

/**
 * Búsqueda de comparables de mercado.
 *
 * Reemplaza el trabajo que hoy se hace abriendo pestañas y anotando cifras a mano, que es
 * además donde se cometen los errores de dedo que después mueven la puja máxima.
 *
 * Nada de lo que se muestra aquí entra solo al cálculo: cada aviso se revisa y se importa
 * explícitamente. La fuente automática propone; la persona decide.
 */
@Component({
  selector: 'app-market',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './market.html',
  styleUrl: './market.scss'
})
export class Market {
  private readonly market = inject(MarketService);
  private readonly vehicles = inject(VehiclesService);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  readonly clp = clp;
  readonly num = num;
  readonly pct = pct;

  readonly candidates = signal<Candidate[]>([]);
  readonly sources = signal<SourceStatus[]>([]);
  readonly vehicleOptions = signal<VehicleSummary[]>([]);
  readonly parsed = signal<ParsedListing | null>(null);

  readonly searching = signal(false);
  readonly importing = signal(false);
  readonly fromCache = signal(false);
  readonly error = signal<string | null>(null);
  readonly parseError = signal<string | null>(null);
  readonly imported = signal<string | null>(null);

  readonly searchForm = this.fb.nonNullable.group({
    make: [''],
    model: [''],
    year: [null as number | null],
    yearTolerance: [2],
    region: [''],
    limit: [25]
  });

  readonly pasteForm = this.fb.nonNullable.group({ text: [''] });

  readonly targetForm = this.fb.nonNullable.group({ vehicleId: [null as number | null] });

  readonly selected = computed(() => this.candidates().filter((c) => c.selected));

  /**
   * Vehículo con el que se llegó desde su ficha. Se muestra en el encabezado porque si la
   * búsqueda no trae nada, el selector de destino no aparece y no quedaría rastro de para qué
   * auto se está buscando.
   */
  private readonly targetVehicleId = signal<number | null>(null);

  readonly targetVehicle = computed(() => {
    const id = this.targetVehicleId();
    return id === null ? null : (this.vehicleOptions().find((v) => v.id === id) ?? null);
  });

  /**
   * Resumen de lo seleccionado, recalculado con cada check. Sirve para darse cuenta en el
   * momento de que la muestra quedó torcida, en vez de descubrirlo cuando el análisis ya salió.
   */
  readonly selectedStats = computed<PriceStats | null>(() => {
    const prices = this.selected()
      .map((c) => c.result.listedPrice)
      .sort((a, b) => a - b);

    if (prices.length === 0) return null;

    const count = prices.length;
    const average = prices.reduce((sum, p) => sum + p, 0) / count;

    const middle = Math.floor(count / 2);
    const median = count % 2 === 0 ? (prices[middle - 1] + prices[middle]) / 2 : prices[middle];

    const { mode, modeCount } = this.calculateMode(prices);

    return {
      count,
      min: prices[0],
      max: prices[count - 1],
      average,
      median,
      mode,
      modeCount,
      // La mediana no se mueve con un aviso extremo y el promedio sí. Que se separen es la
      // señal de que hay algo tirando del conjunto.
      skew: median === 0 ? 0 : Math.abs(average - median) / median
    };
  });

  /**
   * Precio que más se repite. Sobre cifras continuas la moda muchas veces no existe, y decirlo
   * es parte del dato: si ningún precio se repite, no hay un valor al que el mercado converja.
   *
   * En los avisos chilenos sí se repiten, porque se publican en cifras redondas, y ahí la moda
   * dice algo que la mediana no: cuál es el precio que varios vendedores consideran el correcto.
   */
  private calculateMode(sortedPrices: number[]): { mode: number | null; modeCount: number } {
    const frequency = new Map<number, number>();
    for (const price of sortedPrices) frequency.set(price, (frequency.get(price) ?? 0) + 1);

    let mode: number | null = null;
    let modeCount = 0;

    for (const [price, times] of frequency) {
      // En empate se queda el más bajo: para quien compra, la referencia prudente.
      if (times > modeCount || (times === modeCount && mode !== null && price < mode)) {
        mode = price;
        modeCount = times;
      }
    }

    return modeCount < 2 ? { mode: null, modeCount: 0 } : { mode, modeCount };
  }

  constructor() {
    this.vehicles
      .list()
      .pipe(catchError(() => of([] as VehicleSummary[])))
      .subscribe((items) => this.vehicleOptions.set(items));

    // Se llega aquí desde la ficha de un vehículo: se preselecciona el destino y se busca solo.
    const vehicleId = Number(this.route.snapshot.queryParamMap.get('vehiculo'));
    if (vehicleId) {
      this.targetVehicleId.set(vehicleId);
      this.targetForm.patchValue({ vehicleId });
      this.searchForVehicle(vehicleId);
    }
  }

  search(): void {
    const raw = this.searchForm.getRawValue();

    this.run(
      this.market.search({
        make: raw.make || undefined,
        model: raw.model || undefined,
        year: raw.year ?? undefined,
        yearTolerance: raw.yearTolerance,
        region: raw.region || undefined,
        limit: raw.limit
      })
    );
  }

  searchForVehicle(vehicleId: number): void {
    this.run(this.market.searchForVehicle(vehicleId));
  }

  private run(request: ReturnType<MarketService['search']>): void {
    this.searching.set(true);
    this.error.set(null);
    this.imported.set(null);

    request.subscribe({
      next: (response) => {
        this.searching.set(false);
        this.sources.set(response.sources);
        this.fromCache.set(response.fromCache);

        // Los pegados a mano se conservan: cuestan trabajo y una búsqueda no debe borrarlos.
        const manual = this.candidates().filter((c) => c.manual);
        this.candidates.set([
          ...manual,
          ...response.results.map((result) => ({ result, selected: false, manual: false }))
        ]);
      },
      error: (err) => {
        this.searching.set(false);
        this.error.set(describeHttpError(err));
      }
    });
  }

  parse(): void {
    const text = this.pasteForm.getRawValue().text.trim();
    if (!text) return;

    this.parseError.set(null);
    this.parsed.set(null);

    this.market.parse(text).subscribe({
      next: (result) => this.parsed.set(result),
      error: (err) => this.parseError.set(describeHttpError(err))
    });
  }

  /** Sube el aviso pegado a la lista de candidatos, ya seleccionado. */
  acceptParsed(): void {
    const parsed = this.parsed();
    if (!parsed?.isUsable) return;

    const result: MarketSearchResult = {
      source: 'Pegado',
      listedPrice: parsed.price!,
      year: parsed.year!,
      mileageKm: parsed.mileageKm ?? null,
      title: [parsed.make, parsed.model, parsed.year].filter(Boolean).join(' ') || 'Aviso pegado',
      url: parsed.url ?? null,
      region: parsed.region ?? null,
      isUsable: true
    };

    this.candidates.update((items) => [{ result, selected: true, manual: true }, ...items]);
    this.parsed.set(null);
    this.pasteForm.reset({ text: '' });
  }

  toggle(index: number): void {
    this.candidates.update((items) =>
      items.map((c, i) => (i === index ? { ...c, selected: !c.selected } : c))
    );
  }

  toggleAll(select: boolean): void {
    this.candidates.update((items) => items.map((c) => ({ ...c, selected: select })));
  }

  importSelected(): void {
    const vehicleId = this.targetForm.getRawValue().vehicleId;
    const chosen = this.selected();

    if (!vehicleId || chosen.length === 0) return;

    this.importing.set(true);
    this.error.set(null);
    this.imported.set(null);

    this.market.import(vehicleId, chosen.map((c) => c.result)).subscribe({
      next: (response) => {
        this.importing.set(false);
        this.imported.set(response.message);
        this.candidates.update((items) => items.filter((c) => !c.selected));
      },
      error: (err) => {
        this.importing.set(false);
        this.error.set(describeHttpError(err));
      }
    });
  }

}
