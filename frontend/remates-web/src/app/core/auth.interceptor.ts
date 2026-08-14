import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';

import { API_BASE_URL } from './api-config';
import { AuthService } from './services/auth.service';

/** Rutas donde un 401 es la respuesta legítima y no hay nada que renovar. */
const AUTH_ENDPOINTS = ['/api/auth/login', '/api/auth/refresh'];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken;

  // En producción la URL base es vacía porque la API va en el mismo origen. Comprobar
  // startsWith('') daría cierto para cualquier dirección y el token acabaría viajando a
  // hosts externos, así que en ese caso solo se aceptan rutas relativas.
  const isOwnApi = API_BASE_URL
    ? request.url.startsWith(API_BASE_URL)
    : request.url.startsWith('/');

  const isAuthEndpoint = AUTH_ENDPOINTS.some((path) => request.url.includes(path));

  const authorized =
    token && isOwnApi && !isAuthEndpoint
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

  return next(authorized).pipe(
    catchError((error: HttpErrorResponse) => {
      const shouldRefresh =
        error.status === 401 && !isAuthEndpoint && isOwnApi && auth.refreshToken !== null;

      if (!shouldRefresh) return throwError(() => error);

      // Un access token vencido se renueva y se reintenta la petición una sola vez.
      return auth.refresh().pipe(
        switchMap(() =>
          next(request.clone({ setHeaders: { Authorization: `Bearer ${auth.accessToken}` } }))
        ),
        catchError((refreshError) => {
          auth.logout();
          return throwError(() => refreshError);
        })
      );
    })
  );
};
