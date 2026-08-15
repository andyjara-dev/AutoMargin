/**
 * Definiciones de los conceptos que aparecen en las pantallas.
 *
 * Viven en un solo lugar a propósito. Una definición repetida en cada pantalla se contradice
 * sola con el tiempo: se corrige en una, se olvida en las otras, y el usuario termina leyendo
 * dos explicaciones distintas del mismo número.
 *
 * Cada entrada apunta a la sección del manual donde el concepto está desarrollado con fórmulas
 * y ejemplos. Aquí va solo lo que se puede leer de pie frente a la pantalla.
 */
export interface GlossaryEntry {
  title: string;
  description: string;
  /** Id de la sección del manual. Debe existir en tutorial.html. */
  anchor: string;
}

export const GLOSSARY = {
  // ---------- La decisión ----------
  pujaMaxima: {
    title: 'Puja máxima',
    description:
      'Lo más que puedes ofrecer sin bajar de la utilidad mínima que exiges. Ya tiene ' +
      'descontados todos los costos y el colchón de seguridad. Si la subasta pasa de ahí, ' +
      'te retiras: no es terquedad, es que arriba de ese número el negocio deja de serlo.',
    anchor: 'idea-central'
  },
  breakEven: {
    title: 'Punto de equilibrio',
    description:
      'El precio de adjudicación en el que no ganas ni pierdes. Un peso más y estás ' +
      'trabajando gratis. Siempre está por encima de la puja máxima, porque la puja máxima ' +
      'además te deja una utilidad.',
    anchor: 'terna'
  },
  precioActual: {
    title: 'Precio actual del remate',
    description:
      'Lo que va ofreciendo la sala en este momento, o el precio mínimo si aún no parte. Es ' +
      'el número que comparas contra la puja máxima para decidir si sigues o te bajas.',
    anchor: 'terna'
  },

  // ---------- Resultado ----------
  score: {
    title: 'Puntaje',
    description:
      'Resume en 0 a 100 siete factores: rentabilidad, holgura de puja, liquidez, riesgo ' +
      'mecánico, riesgo de papeles, certeza de la estimación y calidad de la evidencia. No ' +
      'decide solo — el semáforo se ancla en la comparación de precios, no en el puntaje.',
    anchor: 'score'
  },
  semaforo: {
    title: 'Semáforo',
    description:
      'Verde es comprar con holgura, amarillo es que da pero justo, rojo es no. Siempre viene ' +
      'con las razones y los números que lo sustentan: si no puedes explicar por qué está en ' +
      'ese color, no lo uses.',
    anchor: 'semaforo'
  },
  gates: {
    title: 'Bloqueos',
    description:
      'Condiciones que fuerzan rojo sin importar lo bueno que se vea el resto: menos de tres ' +
      'comparables, riesgo documental crítico, precio por encima de la puja máxima, pérdida ' +
      'pesimista intolerable. No se compensan con un buen puntaje.',
    anchor: 'gates'
  },

  // ---------- Rentabilidad ----------
  utilidad: {
    title: 'Utilidad esperada',
    description:
      'Lo que queda después de restarle al precio de venta todos los costos: compra, ' +
      'comisiones, reparación, transporte, publicación y el costo del capital inmovilizado.',
    anchor: 'roi'
  },
  roiAnual: {
    title: 'ROI anualizado',
    description:
      'La rentabilidad llevada a un año, para poder comparar operaciones de distinta duración. ' +
      'Un 15% en 20 días no es lo mismo que un 15% en 120: el primero deja el capital libre ' +
      'seis veces al año.',
    anchor: 'roi'
  },
  margen: {
    title: 'Margen sobre venta',
    description:
      'Qué porcentaje del precio de venta es utilidad. Sirve para comparar contra el resto del ' +
      'rubro; el ROI sirve para comparar contra otras formas de usar tu plata.',
    anchor: 'roi'
  },
  utilidadCaja: {
    title: 'Utilidad de caja',
    description:
      'Lo que efectivamente entró menos lo que salió. Es plata contante, pero ignora que tu ' +
      'capital estuvo meses inmovilizado.',
    anchor: 'dos-utilidades'
  },
  utilidadEconomica: {
    title: 'Utilidad económica',
    description:
      'La de caja menos el costo del capital inmovilizado. Es la única comparable contra lo ' +
      'que proyectaste, porque la proyección también lo descontaba.',
    anchor: 'dos-utilidades'
  },

  // ---------- Mercado ----------
  comparables: {
    title: 'Comparables',
    description:
      'Avisos reales de autos parecidos al que quieres comprar. De ellos sale el valor de ' +
      'mercado y de ahí la puja máxima. Con menos de tres el análisis se bloquea: con dos ' +
      'datos no se estima nada.',
    anchor: 'valor-mercado'
  },
  valorConservador: {
    title: 'Valor conservador',
    description:
      'El precio de venta que el sistema usa para calcular. Es deliberadamente pesimista: sale ' +
      'del cuartil bajo de los comparables y además le descuenta la brecha de negociación.',
    anchor: 'valor-mercado'
  },
  brechaNegociacion: {
    title: 'Brecha de negociación',
    description:
      'Los avisos muestran lo que el vendedor pide, no lo que recibe. La venta real ocurre por ' +
      'debajo, y ese descuento se aplica antes de calcular nada.',
    anchor: 'valor-mercado'
  },
  dispersion: {
    title: 'Dispersión',
    description:
      'Qué tan separados están los precios de tus comparables. Mucha dispersión significa que ' +
      'el mercado no tiene claro cuánto vale ese auto, y el sistema responde exigiendo más ' +
      'colchón de seguridad.',
    anchor: 'margen-seguridad'
  },
  mediana: {
    title: 'Mediana',
    description:
      'El precio del medio. Es la referencia que manda, porque un aviso extremo no la mueve. ' +
      'El sistema valoriza con la mediana y los cuartiles, nunca con el promedio.',
    anchor: 'pantalla-mercado'
  },
  moda: {
    title: 'Moda',
    description:
      'El precio que más se repite. Cuando varios vendedores publican el mismo número, ese es ' +
      'el que el mercado da por bueno. Si ningún precio se repite, no existe.',
    anchor: 'pantalla-mercado'
  },

  // ---------- Costos y tiempo ----------
  costosProporcionales: {
    title: 'Costos proporcionales',
    description:
      'Los que suben si pujas más alto: comisión del martillero con su IVA e impuesto de ' +
      'transferencia. Por eso no se pueden restar como monto fijo — el sistema los despeja.',
    anchor: 'costos'
  },
  costosFijos: {
    title: 'Costos fijos',
    description:
      'Lo que gastas después de comprar y no depende del precio de adjudicación: reparación, ' +
      'transporte, preparación, trámites y un colchón para imprevistos.',
    anchor: 'costos'
  },
  costoCapital: {
    title: 'Costo del capital',
    description:
      'Lo que te cuesta tener la plata metida en el auto en vez de disponible. Corre desde el ' +
      'día uno, y es la razón por la que un auto rentable pero lento puede ser mal negocio.',
    anchor: 'tiempo'
  },
  diasVenta: {
    title: 'Días estimados de venta',
    description:
      'Cuánto crees que tardará en venderse. Alarga esto y verás caer la puja máxima: cada día ' +
      'extra es capital inmovilizado que hay que pagar.',
    anchor: 'tiempo'
  },

  // ---------- Riesgo ----------
  margenSeguridad: {
    title: 'Margen de seguridad',
    description:
      'El colchón que se le descuenta a la puja teórica. No es un porcentaje fijo: crece con ' +
      'la incertidumbre real de este vehículo — rango de reparación ancho, comparables ' +
      'dispersos, pocos datos o papeles dudosos.',
    anchor: 'margen-seguridad'
  },
  escenarios: {
    title: 'Escenarios',
    description:
      'El mismo negocio calculado tres veces: optimista, esperado y pesimista. El que importa ' +
      'es el pesimista, porque es el que te dice cuánto puedes perder si todo sale mal.',
    anchor: 'escenarios'
  },
  riesgoMecanico: {
    title: 'Riesgo mecánico',
    description:
      'Qué tanto pudiste revisar el auto. En remate se compra sin prueba de manejo y a veces ' +
      'sin encenderlo. Declararlo honestamente es lo que hace que el sistema te proteja.',
    anchor: 'riesgos'
  },
  riesgoDocumental: {
    title: 'Riesgo documental',
    description:
      'Prendas, encargo por robo, multas TAG impagas, limitaciones al dominio. No son un poco ' +
      'de riesgo: son problemas que arruinan la operación completa y fuerzan rojo.',
    anchor: 'riesgos'
  },

  // ---------- Capital ----------
  capitalInmovilizado: {
    title: 'Capital inmovilizado',
    description:
      'Cuánta de tu plata está metida en autos sin vender. Mientras más alto, menos margen ' +
      'tienes para entrar a un remate nuevo aunque aparezca una oportunidad buena.',
    anchor: 'ciclo'
  },
  utilidadPotencial: {
    title: 'Utilidad potencial',
    description:
      'Lo que ganarías si vendieras todo el inventario a su valor proyectado. Es una promesa, ' +
      'no plata: solo cuenta cuando la venta está hecha.',
    anchor: 'dos-utilidades'
  }
} as const satisfies Record<string, GlossaryEntry>;

export type GlossaryKey = keyof typeof GLOSSARY;
