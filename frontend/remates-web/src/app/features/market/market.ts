import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';

import { clp, num } from '../../core/format';
import { describeHttpError } from '../../core/http-error';
import { VehicleSummary } from '../../core/models/auth.models';
import {
  MarketSearchResult,
  ParsedListing,
  SourceStatus
} from '../../core/models/market.models';
import { MarketService } from '../../core/services/market.service';
import { VehiclesService } from '../../core/services/vehicles.service';

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

  /** Referencia rápida mientras se eligen avisos: si la muestra es corta, hay que seguir buscando. */
  readonly selectedMedian = computed(() => {
    const prices = this.selected()
      .map((c) => c.result.listedPrice)
      .sort((a, b) => a - b);

    if (prices.length === 0) return null;

    const middle = Math.floor(prices.length / 2);
    return prices.length % 2 === 0 ? (prices[middle - 1] + prices[middle]) / 2 : prices[middle];
  });

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
