import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './services/auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) return true;

  // Se recuerda a dónde iba para volver ahí después de entrar.
  return router.createUrlTree(['/login'], { queryParams: { redirect: state.url } });
};
