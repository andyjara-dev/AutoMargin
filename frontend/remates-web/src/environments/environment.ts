/**
 * Configuración de desarrollo: la API corre aparte, en su propio puerto.
 * En producción se reemplaza por environment.prod.ts al compilar.
 */
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5044'
};
