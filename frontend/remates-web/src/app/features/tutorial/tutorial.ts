import { Component, afterNextRender, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

interface TocEntry {
  id: string;
  label: string;
  group: string;
}

@Component({
  selector: 'app-tutorial',
  imports: [RouterLink],
  templateUrl: './tutorial.html',
  styleUrl: './tutorial.scss'
})
export class Tutorial {
  private readonly route = inject(ActivatedRoute);

  readonly activeId = signal<string>('el-negocio');

  constructor() {
    // Se llega aquí desde las ayudas de las otras pantallas, con la sección en el fragmento de
    // la dirección. Hay que esperar a que el navegador haya maquetado: en ngAfterViewInit el
    // manual todavía mide cero y el desplazamiento no va a ninguna parte.
    afterNextRender(() => this.scrollToFragment());
  }

  private scrollToFragment(): void {
    const fragment = this.route.snapshot.fragment;
    if (!fragment) return;

    const target = document.getElementById(fragment);
    if (!target) return;

    this.activeId.set(fragment);

    // Sin animación: son cuarenta mil píxeles de manual y ver el recorrido entero no aporta
    // nada. Quien llega por un enlace quiere estar ahí, no viajar hasta ahí.
    target.scrollIntoView({ behavior: 'auto', block: 'start' });
  }

  readonly toc: TocEntry[] = [
    { group: 'Empieza aquí', id: 'el-negocio', label: 'El negocio en simple' },
    { group: 'Empieza aquí', id: 'palabras', label: 'Las palabras del remate' },
    { group: 'Empieza aquí', id: 'idea-central', label: 'La idea central: la puja máxima' },
    { group: 'Empieza aquí', id: 'simbolos', label: 'Los cinco símbolos que verás' },
    { group: 'Empieza aquí', id: 'flujo', label: 'Cómo se usa, paso a paso' },

    { group: 'Los conceptos', id: 'valor-mercado', label: '1. Valor de mercado' },
    { group: 'Los conceptos', id: 'costos', label: '2. Costos fijos y proporcionales' },
    { group: 'Los conceptos', id: 'tiempo', label: '3. El tiempo y el costo del dinero' },
    { group: 'Los conceptos', id: 'roi', label: '4. Utilidad, margen y ROI' },
    { group: 'Los conceptos', id: 'utilidad-minima', label: '5. Cuánto quieres ganar' },
    { group: 'Los conceptos', id: 'terna', label: '6. Los tres precios clave' },
    { group: 'Los conceptos', id: 'margen-seguridad', label: '7. El colchón de seguridad' },
    { group: 'Los conceptos', id: 'escenarios', label: '8. Los tres escenarios' },
    { group: 'Los conceptos', id: 'riesgos', label: '9. Riesgo mecánico y de papeles' },
    { group: 'Los conceptos', id: 'score', label: '10. El puntaje' },
    { group: 'Los conceptos', id: 'gates', label: '11. Los bloqueos' },
    { group: 'Los conceptos', id: 'semaforo', label: '12. El semáforo' },
    { group: 'Los conceptos', id: 'dos-utilidades', label: '13. Las dos utilidades' },

    { group: 'Las pantallas', id: 'ciclo', label: 'El ciclo de un vehículo' },
    { group: 'Las pantallas', id: 'leer-pantalla', label: 'Analizador' },
    { group: 'Las pantallas', id: 'pantalla-remate', label: 'Sala de remate' },
    { group: 'Las pantallas', id: 'pantalla-estado', label: 'Estado del negocio' },
    { group: 'Las pantallas', id: 'pantalla-vehiculos', label: 'Vehículos y su ficha' },
    { group: 'Las pantallas', id: 'pantalla-mercado', label: 'Comparables de mercado' },
    { group: 'Las pantallas', id: 'pantalla-parametros', label: 'Parámetros' },

    { group: 'En la práctica', id: 'ejemplo', label: 'Ejemplo completo con números' },
    { group: 'En la práctica', id: 'errores', label: 'Errores comunes' },
    { group: 'En la práctica', id: 'faq', label: 'Preguntas frecuentes' },

    { group: 'Referencia', id: 'glosario', label: 'Glosario completo' },
    { group: 'Referencia', id: 'formulario', label: 'Todas las fórmulas juntas' },
    { group: 'Referencia', id: 'limites', label: 'Límites y advertencias' }
  ];

  /** Grupos del índice, en orden de aparición y sin repetir. */
  readonly groups = [...new Set(this.toc.map((e) => e.group))];

  entriesOf(group: string): TocEntry[] {
    return this.toc.filter((e) => e.group === group);
  }

  scrollTo(id: string, event: Event): void {
    event.preventDefault();
    this.activeId.set(id);
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
