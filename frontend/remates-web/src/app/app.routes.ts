import { Routes } from '@angular/router';

import { authGuard } from './core/auth.guard';

/**
 * Todo requiere sesión salvo el propio login.
 *
 * El analizador y el manual estuvieron abiertos mientras el sistema vivía en localhost, para
 * poder usarlos aunque la base no estuviera disponible. Publicado en internet eso deja a la
 * vista la herramienta de decisión y, sobre todo, la metodología completa del negocio.
 */
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
    title: 'Iniciar sesión · AutoMargin'
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
    title: 'Estado del negocio · AutoMargin'
  },
  {
    path: 'analizador',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/deal-analyzer/deal-analyzer').then((m) => m.DealAnalyzer),
    title: 'Analizador · AutoMargin'
  },
  {
    path: 'vehiculos',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/vehicles/vehicle-list').then((m) => m.VehicleList),
    title: 'Vehículos · AutoMargin'
  },
  {
    path: 'vehiculos/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/vehicles/vehicle-detail').then((m) => m.VehicleDetail),
    title: 'Ficha del vehículo · AutoMargin'
  },
  {
    path: 'parametros',
    canActivate: [authGuard],
    loadComponent: () => import('./features/parameters/parameters').then((m) => m.Parameters),
    title: 'Parámetros · AutoMargin'
  },
  {
    path: 'cuenta',
    canActivate: [authGuard],
    loadComponent: () => import('./features/account/account').then((m) => m.Account),
    title: 'Mi cuenta · AutoMargin'
  },
  {
    path: 'manual',
    canActivate: [authGuard],
    loadComponent: () => import('./features/tutorial/tutorial').then((m) => m.Tutorial),
    title: 'Manual · AutoMargin'
  },
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: '**', redirectTo: 'dashboard' }
];
