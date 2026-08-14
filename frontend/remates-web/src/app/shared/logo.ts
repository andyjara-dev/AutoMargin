import { Component, input } from '@angular/core';

import { LOGO_OFFSET, LOGO_PATH } from './logo-path';

/**
 * Isotipo de AutoMargin.
 *
 * Va como SVG en línea y no como <img> para poder teñirlo con el gradiente de marca o con el
 * color del contenedor. El trazo vive en logo-path.ts, generado desde el PNG original.
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
        <linearGradient [attr.id]="gradientId" x1="8%" y1="5%" x2="92%" y2="95%">
          <stop offset="0%" stop-color="#2563EB" />
          <stop offset="52%" stop-color="#0EA5E9" />
          <stop offset="100%" stop-color="#22D3EE" />
        </linearGradient>
      </defs>

      <g [attr.transform]="transform" [attr.fill]="fill" fill-rule="evenodd">
        <path [attr.d]="path" />
      </g>
    </svg>
  `,
  styles: [':host { display: inline-flex; line-height: 0; }']
})
export class Logo {
  readonly size = input(28);

  /** En monocromático hereda el color del contenedor, para fondos claros o de un solo tono. */
  readonly mono = input(false);

  protected readonly path = LOGO_PATH;
  protected readonly transform = `translate(${LOGO_OFFSET.x} ${LOGO_OFFSET.y})`;

  /** Cada instancia necesita su propio id de gradiente: repetirlo rompe el segundo SVG. */
  protected readonly gradientId = `am-${Math.random().toString(36).slice(2, 9)}`;

  protected get fill(): string {
    return this.mono() ? 'currentColor' : `url(#${this.gradientId})`;
  }
}
