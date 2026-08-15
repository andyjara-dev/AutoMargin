/**
 * Selecciona todo el contenido de un campo cuando se toca con el dedo.
 *
 * En el remate el precio no se edita, se reescribe entero: la sala canta una cifra nueva y hay
 * que ponerla completa. Sin esto toca borrar dígito por dígito con el teclado del teléfono
 * tapando media pantalla, que es justo lo que no hay tiempo de hacer.
 *
 * Solo con puntero grueso. En un teclado y ratón se espera poder poner el cursor donde uno
 * pinchó, y seleccionar todo sería una molestia en vez de una ayuda.
 */
export function selectOnTouch(event: Event): void {
  if (!matchMedia('(pointer: coarse)').matches) return;

  const input = event.target as HTMLInputElement | null;
  if (input === null || typeof input.select !== 'function') return;

  // Los navegadores móviles reponen el cursor después de dar el foco, así que la selección se
  // pide en el siguiente ciclo: hacerlo aquí mismo no sobreviviría.
  setTimeout(() => input.select(), 0);
}
