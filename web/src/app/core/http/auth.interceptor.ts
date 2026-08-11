import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, filter, finalize, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';

/**
 * Functional HTTP interceptor with single-flight refresh.
 *
 * - Attaches Bearer to outbound requests when authenticated.
 * - On 401: first offender triggers refresh; subsequent 401s queue on refreshCompleted$
 *   (ReplaySubject, so late subscribers still see the emission).
 * - Never refreshes for /login, /register, /refresh (would loop).
 * - DOES refresh for /logout so an expired-access-token logout still cleanly revokes.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const token = auth.accessToken;
  const authed = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authed).pipe(
    catchError((err: HttpErrorResponse) => {
      const isAuthNonRefreshable =
        req.url.includes('/api/auth/login') ||
        req.url.includes('/api/auth/register') ||
        req.url.includes('/api/auth/refresh');

      if (err.status !== 401 || isAuthNonRefreshable) {
        return throwError(() => err);
      }

      if (auth.refreshing) {
        // Queue on the shared ReplaySubject — late subscribers still get the latest value.
        return auth.refreshCompleted$.pipe(
          filter(t => t !== undefined),
          take(1),
          switchMap(newToken => {
            if (!newToken) return throwError(() => err);
            return next(req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } }));
          }),
        );
      }

      auth.refreshing = true;
      return auth.refresh().pipe(
        switchMap(resp => {
          auth.refreshCompleted$.next(resp.accessToken);
          return next(req.clone({ setHeaders: { Authorization: `Bearer ${resp.accessToken}` } }));
        }),
        catchError(refreshErr => {
          auth.refreshCompleted$.next(null);
          auth.clearTokens();
          router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
          return throwError(() => refreshErr);
        }),
        // C4 FIX — always reset the single-flight gate after the cycle. Prior code only
        // reset in the two branches above; a synchronous throw from refresh() would leak
        // `refreshing = true` and hang every subsequent 401 forever.
        finalize(() => auth.resetRefreshGate()),
      );
    }),
  );
};
