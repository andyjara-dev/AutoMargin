import { Routes } from '@angular/router';

import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
    title: 'Iniciar sesión · AutoMargin'
  },
  {
    // El analizador queda accesible sin sesión: es lo único que funciona sin base de datos y
    // conviene tenerlo a mano desde el recinto del remate.
    path: 'analizador',
    loadComponent: () =>
      import('./features/deal-analyzer/deal-analyzer').then((m) => m.DealAnalyzer),
    title: 'Analizador · AutoMargin'
  },
  {
    path: 'manual',
    loadComponent: () => import('./features/tutorial/tutorial').then((m) => m.Tutorial),
    title: 'Manual · AutoMargin'
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
  { path: '', pathMatch: 'full', redirectTo: 'analizador' },
  { path: '**', redirectTo: 'analizador' }
];
