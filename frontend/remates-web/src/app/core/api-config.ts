import { environment } from '../../environments/environment';

/**
 * URL base de la API.
 *
 * En desarrollo apunta al puerto de Remates.Api. En producción queda vacía, porque Nginx
 * sirve el frontend y reenvía /api al contenedor de la API desde el mismo origen.
 */
export const API_BASE_URL = environment.apiBaseUrl;
