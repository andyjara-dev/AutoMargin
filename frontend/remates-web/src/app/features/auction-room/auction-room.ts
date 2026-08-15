import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';

import { clp, num } from '../../core/format';
import { describeHttpError } from '../../core/http-error';
import { VEHICLE_STATUS_LABELS, VehicleStatus, VehicleSummary } from '../../core/models/auth.models';
import { LotsService } from '../../core/services/lots.service';
import { VehiclesService } from '../../core/services/vehicles.service';
import { HelpTip } from '../../shared/help-tip';

/** Un lote en la sala, con el precio que va cantando el martillero. */
interface Lot {
  vehicle: VehicleSummary;
  /** Lo que va ofreciendo la sala ahora mismo. Vive solo en esta pantalla. */
  currentPrice: number | null;
}

/**
 * Estados en los que un lote todavía corre. Comprados, vendidos y descartados no aparecen: la
 * sala muestra lo que está en juego hoy, no el historial.
 */
const ACTIVE_STATUSES: VehicleStatus[] = ['Detected', 'Analyzing', 'Bidding'];

/**
 * Sala de remate.
 *
 * Es la pantalla del día de la subasta. El análisis ya está hecho y guardado; aquí solo se
 * compara lo que canta el martillero contra la puja máxima de cada lote, y se anota cómo terminó.
 *
 * Deliberadamente no recalcula nada. La puja máxima que muestra es la que quedó guardada con su
 * análisis, con sus comparables y sus parámetros del momento. Recalcularla acá, con el martillo
 * corriendo y sin la evidencia a la vista, sería cambiar la regla en medio del juego.
 */
@Component({
  selector: 'app-auction-room',
  imports: [RouterLink, HelpTip],
  templateUrl: './auction-room.html',
  styleUrl: './auction-room.scss'
})
export class AuctionRoom {
  private readonly vehicles = inject(VehiclesService);
  private readonly lots = inject(LotsService);

  readonly clp = clp;
  readonly num = num;
  readonly statusLabels = VEHICLE_STATUS_LABELS;

  readonly items = signal<Lot[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly busyId = signal<number | null>(null);

  /** Cuántos lotes están sobre su techo ahora mismo. Es la lectura de un vistazo. */
  readonly overLimit = computed(() =>
    this.items().filter((l) => this.headroom(l) !== null && this.headroom(l)! < 0).length
  );

  readonly ready = computed(() => this.items().filter((l) => l.vehicle.lastMaxBid != null).length);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.vehicles
      .list()
      .pipe(
        catchError((err) => {
          this.error.set(describeHttpError(err));
          return of([] as VehicleSummary[]);
        })
      )
      .subscribe((all) => {
        this.loading.set(false);

        const active = all.filter((v) => ACTIVE_STATUSES.includes(v.status));

        // Se conserva el precio ya tecleado de los lotes que siguen ahí: recargar en plena
        // subasta no puede costar volver a escribir lo que la sala ya cantó.
        const typed = new Map(this.items().map((l) => [l.vehicle.id, l.currentPrice]));

        this.items.set(
          active.map((vehicle) => ({
            vehicle,
            currentPrice: typed.get(vehicle.id) ?? null
          }))
        );
      });
  }

  setPrice(vehicleId: number, value: string): void {
    const parsed = Number(value.replace(/\D/g, ''));

    this.items.update((items) =>
      items.map((l) =>
        l.vehicle.id === vehicleId
          ? { ...l, currentPrice: Number.isFinite(parsed) && parsed > 0 ? parsed : null }
          : l
      )
    );
  }

  /** Cuánto falta para tocar el techo. Negativo significa que la sala ya lo pasó. */
  headroom(lot: Lot): number | null {
    const max = lot.vehicle.lastMaxBid;
    if (max == null || lot.currentPrice === null) return null;

    return max - lot.currentPrice;
  }

  /**
   * Semáforo del precio, no del negocio.
   *
   * Solo compara el precio contra la puja máxima. El semáforo completo del análisis pesa además
   * el puntaje y los bloqueos, y ese sigue estando en la ficha: aquí se responde la única
   * pregunta que se hace con el martillo en el aire, que es si todavía se puede subir.
   */
  priceTone(lot: Lot): 'green' | 'yellow' | 'red' | 'none' {
    const max = lot.vehicle.lastMaxBid;
    const headroom = this.headroom(lot);

    if (max == null || headroom === null) return 'none';
    if (headroom < 0) return 'red';

    return headroom > max * 0.1 ? 'green' : 'yellow';
  }

  mark(lot: Lot, status: VehicleStatus): void {
    this.busyId.set(lot.vehicle.id);
    this.error.set(null);

    this.lots.changeStatus(lot.vehicle.id, status).subscribe({
      next: () => {
        this.busyId.set(null);
        // Sale de la lista porque deja de estar en juego, no porque se borre.
        this.items.update((items) => items.filter((l) => l.vehicle.id !== lot.vehicle.id));
      },
      error: (err) => {
        this.busyId.set(null);
        this.error.set(describeHttpError(err));
      }
    });
  }

  remove(lot: Lot): void {
    if (!confirm(`¿Quitar «${lot.vehicle.label}» de la sala? Se borra el lote y su análisis.`)) return;

    this.busyId.set(lot.vehicle.id);

    this.lots.remove(lot.vehicle.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.items.update((items) => items.filter((l) => l.vehicle.id !== lot.vehicle.id));
      },
      error: (err) => {
        this.busyId.set(null);
        this.error.set(describeHttpError(err));
      }
    });
  }
}
