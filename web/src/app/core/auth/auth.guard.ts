import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * C5 FIX — try silent refresh before redirecting to login. Previous version redirected
 * whenever the access token was expired, ignoring a still-valid refresh token entirely
 * and forcing re-login on every full page reload. Now: if there's a refresh token,
 * attempt refresh; only redirect on refresh failure.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated) return true;

  if (auth.canAttemptRefresh) {
    return auth.refresh().pipe(
      map(() => true),
      catchError(() => of(router.createUrlTree(['/login'], {
        queryParams: { returnUrl: state.url },
      }))),
    );
  }

  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });
};
