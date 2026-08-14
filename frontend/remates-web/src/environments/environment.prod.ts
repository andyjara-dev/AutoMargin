/**
 * En producción el frontend y la API se sirven desde el mismo origen: Nginx entrega los
 * archivos estáticos y reenvía /api al contenedor de la API.
 *
 * Con la URL base vacía, las peticiones salen relativas al dominio que esté sirviendo la
 * aplicación. Eso elimina el CORS por completo y hace que el mismo build funcione en
 * cualquier dominio, sin recompilar.
 */
export const environment = {
  production: true,
  apiBaseUrl: ''
};
