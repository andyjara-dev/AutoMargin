import { environment } from '../../environments/environment';

interface ApiError {
  status?: number;
  error?: {
    title?: string;
    errors?: Record<string, string[]>;
  };
}

/**
 * Traduce un error HTTP a un mensaje para el usuario.
 *
 * Está centralizado porque cada pantalla tenía su propia versión, y eso hacía que el mismo
 * fallo se explicara distinto según dónde ocurriera. Además, el mensaje de «sin conexión»
 * mencionaba el puerto local, que en el sitio publicado no significa nada para quien lo lee.
 */
export function describeHttpError(
  error: unknown,
  fallback = 'No se pudo completar la operación.'
): string {
  const err = error as ApiError;

  if (err?.status === 0) {
    return environment.production
      ? 'No hay conexión con el servidor. Revisa tu conexión e inténtalo de nuevo.'
      : `No hay conexión con la API. Verifica que esté corriendo en ${environment.apiBaseUrl}.`;
  }

  // Los errores de validación son los más útiles: dicen exactamente qué campo está mal.
  const validation = err?.error?.errors;
  if (validation) return Object.values(validation).flat().join(' ');

  if (err?.error?.title) return err.error.title;

  switch (err?.status) {
    case 401:
      return 'Tu sesión expiró. Vuelve a iniciar sesión.';
    case 403:
      return 'No tienes permiso para realizar esta acción.';
    case 404:
      return 'No se encontró lo que buscabas.';
    case 409:
      return 'La operación entra en conflicto con el estado actual.';
    case 500:
    case 502:
    case 503:
      return 'El servidor respondió con un error. Si persiste, revisa el estado del servicio.';
    default:
      return fallback;
  }
}
