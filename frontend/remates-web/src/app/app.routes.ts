import { Routes } from '@angular/router';

export const routes: Routes = [
  {
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
  { path: '', pathMatch: 'full', redirectTo: 'analizador' },
  { path: '**', redirectTo: 'analizador' }
];
