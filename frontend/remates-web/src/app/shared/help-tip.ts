import { Component, ElementRef, HostListener, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { GLOSSARY, GlossaryKey } from './glossary';

/**
 * Signo de interrogación que explica el concepto de al lado.
 *
 * El texto sale del glosario, no de la plantilla que lo usa: así la misma definición aparece
 * igual en todas las pantallas y se corrige en un solo lugar. Cada globo lleva además el enlace
 * a la sección del manual donde el concepto está desarrollado con fórmulas y ejemplos.
 *
 * El globo se posiciona con coordenadas fijas y no dentro del flujo. Varias de estas ayudas
 * viven dentro de tablas con desplazamiento propio, y ahí un panel absoluto quedaría recortado
 * por el contenedor justo cuando hace falta leerlo.
 */
@Component({
  selector: 'app-help',
  imports: [RouterLink],
  template: `
    <button
      type="button"
      class="help__trigger"
      [attr.aria-label]="'Qué es ' + entry().title"
      [attr.aria-expanded]="open()"
      (click)="toggle($event)">?</button>

    @if (open()) {
      <div class="help__panel" role="dialog" [attr.aria-label]="entry().title" [style]="panelStyle()">
        <strong class="help__title">{{ entry().title }}</strong>
        <p class="help__text">{{ entry().description }}</p>
        <a
          class="help__link"
          routerLink="/manual"
          [fragment]="entry().anchor"
          (click)="close()">Ver en el manual →</a>
      </div>
    }
  `,
  styles: [`
    :host { display: inline-flex; vertical-align: middle; }

    .help__trigger {
      width: 15px;
      height: 15px;
      margin-left: 5px;
      padding: 0;
      border: 1px solid var(--border);
      border-radius: 50%;
      background: none;
      color: var(--text-faint);
      font-size: 10px;
      font-weight: 700;
      line-height: 1;
      cursor: help;
      transition: border-color .12s ease, color .12s ease;
    }

    .help__trigger:hover,
    .help__trigger[aria-expanded='true'] {
      border-color: var(--brand);
      color: var(--brand);
    }

    .help__panel {
      position: fixed;
      z-index: 200;
      width: min(320px, calc(100vw - 32px));
      padding: 12px 14px;
      background: var(--surface);
      border: 1px solid var(--brand);
      border-radius: var(--radius);
      box-shadow: 0 12px 32px rgba(0, 0, 0, .45);
      text-align: left;
      cursor: default;
    }

    .help__title { display: block; font-size: 13px; color: var(--text); margin-bottom: 5px; }

    .help__text {
      margin: 0 0 10px;
      font-size: 12.5px;
      line-height: 1.6;
      color: var(--text-dim);
      font-weight: 400;
      text-transform: none;
      letter-spacing: normal;
    }

    .help__link { font-size: 12px; color: var(--brand); text-decoration: none; }
    .help__link:hover { text-decoration: underline; }
  `]
})
export class HelpTip {
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly key = input.required<GlossaryKey>();

  protected readonly open = signal(false);
  protected readonly entry = computed(() => GLOSSARY[this.key()]);

  /** Coordenadas del globo, calculadas al abrirlo desde la posición del signo. */
  protected readonly panelStyle = signal<Record<string, string>>({});

  protected toggle(event: MouseEvent): void {
    event.stopPropagation();

    if (this.open()) {
      this.close();
      return;
    }

    this.panelStyle.set(this.placeNear(
      (event.currentTarget as HTMLElement).getBoundingClientRect()));

    this.open.set(true);
  }

  protected close(): void {
    this.open.set(false);
  }

  /**
   * Deja el globo bajo el signo, corrido a la izquierda si no cabe por la derecha y encima si
   * no cabe por abajo. Sin esto, las ayudas del borde de la pantalla se salen de la ventana.
   */
  private placeNear(anchor: DOMRect): Record<string, string> {
    const margin = 12;
    const width = Math.min(320, window.innerWidth - 2 * margin);
    const estimatedHeight = 170;

    const left = Math.min(
      Math.max(margin, anchor.left - width / 2),
      window.innerWidth - width - margin);

    const fitsBelow = anchor.bottom + estimatedHeight + margin < window.innerHeight;

    return fitsBelow
      ? { left: `${left}px`, top: `${anchor.bottom + 6}px` }
      : { left: `${left}px`, bottom: `${window.innerHeight - anchor.top + 6}px` };
  }

  /** Cualquier clic fuera lo cierra: es una ayuda de paso, no un panel que haya que administrar. */
  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) this.close();
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.close();
  }

  /** Al desplazar la página el globo quedaría flotando lejos de su signo. */
  @HostListener('window:scroll')
  @HostListener('window:resize')
  protected onViewportChange(): void {
    this.close();
  }
}
