import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { UserRole } from './auth.models';

export function roleGuard(allowed: UserRole[]): CanActivateFn {
  return (_route, _state) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated) {
      return router.createUrlTree(['/login']);
    }
    if (!auth.hasAnyRole(allowed)) {
      return router.createUrlTree(['/forbidden']);
    }
    return true;
  };
}
