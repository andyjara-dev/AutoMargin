import { Component, input } from '@angular/core';

/**
 * Isotipo de AutoMargin.
 *
 * Va como SVG en línea y no como <img> para que herede el color cuando se necesita
 * monocromático y para que el gradiente se defina una sola vez por instancia.
 */
@Component({
  selector: 'app-logo',
  template: `
    <svg
      [attr.width]="size()"
      [attr.height]="size()"
      viewBox="0 0 120 120"
      role="img"
      aria-label="AutoMargin">
      <defs>
        <linearGradient [attr.id]="gradientId" x1="10%" y1="0%" x2="95%" y2="100%">
          <stop offset="0%" stop-color="#2563EB" />
          <stop offset="55%" stop-color="#0EA5E9" />
          <stop offset="100%" stop-color="#22D3EE" />
        </linearGradient>
      </defs>

      <!-- La A: un trazo continuo de esquina a esquina pasando por el vértice -->
      <path
        d="M 16 101 L 55 27 Q 60 18 65 27 L 104 101"
        fill="none"
        [attr.stroke]="stroke"
        stroke-width="11"
        stroke-linecap="round"
        stroke-linejoin="round" />

      <!-- La silueta del auto, que además cierra la A por abajo -->
      <path
        d="M 33 88 C 43 70, 63 64, 78 73 C 90 80, 89 95, 77 95 C 66 95, 60 87, 47 87"
        fill="none"
        [attr.stroke]="stroke"
        stroke-width="9.5"
        stroke-linecap="round"
        stroke-linejoin="round" />
    </svg>
  `,
  styles: [':host { display: inline-flex; line-height: 0; }']
})
export class Logo {
  readonly size = input(28);

  /** En monocromático hereda el color del contenedor, para fondos claros o de un solo tono. */
  readonly mono = input(false);

  /** Cada instancia necesita su propio id de gradiente: repetirlo rompe el segundo SVG. */
  protected readonly gradientId = `am-${Math.random().toString(36).slice(2, 9)}`;

  protected get stroke(): string {
    return this.mono() ? 'currentColor' : `url(#${this.gradientId})`;
  }
}
