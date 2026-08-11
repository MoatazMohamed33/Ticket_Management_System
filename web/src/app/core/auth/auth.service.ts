import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, ReplaySubject, tap } from 'rxjs';
import { jwtDecode } from 'jwt-decode';
import { environment } from '../../../environments/environment';
import { AuthResponse, UserDto, UserRole } from './auth.models';

interface JwtPayload {
  sub: string;
  email: string;
  role: string;
  displayName?: string;
  exp: number;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private _accessToken: string | null = null;
  private _refreshToken: string | null = null;
  readonly user$ = new BehaviorSubject<UserDto | null>(null);

  // C4 FIX — ReplaySubject(1) so late subscribers still see the latest emission. Previous
  // Subject dropped events if the subscribe happened after emission, causing queued 401s
  // to hang forever OR trigger a duplicate refresh with a stale token.
  private _refreshCompleted$ = new ReplaySubject<string | null>(1);
  refreshing = false;

  get accessToken(): string | null { return this._accessToken; }
  get refreshToken(): string | null { return this._refreshToken; }
  get refreshCompleted$(): ReplaySubject<string | null> { return this._refreshCompleted$; }

  get isAuthenticated(): boolean {
    return !!this._accessToken && !this.isAccessTokenExpired();
  }

  /** True if there's a refresh token, even if the access token is expired. */
  get canAttemptRefresh(): boolean {
    return !!this._refreshToken;
  }

  hasAnyRole(roles: UserRole[]): boolean {
    const user = this.user$.value;
    return !!user && roles.includes(user.role);
  }

  /** Reset the single-flight gate with a fresh ReplaySubject for the next cycle. */
  resetRefreshGate(): void {
    this._refreshCompleted$ = new ReplaySubject<string | null>(1);
    this.refreshing = false;
  }

  private isAccessTokenExpired(): boolean {
    if (!this._accessToken) return true;
    try {
      const payload = jwtDecode<JwtPayload>(this._accessToken);
      // C8 FIX — a missing or non-numeric exp claim would make `payload.exp <= nowSec`
      // evaluate `undefined <= n` = false, treating a malformed token as valid forever.
      if (typeof payload.exp !== 'number') return true;
      const nowSec = Math.floor(Date.now() / 1000);
      return payload.exp <= nowSec;
    } catch {
      return true;
    }
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiBaseUrl}/auth/login`, { email, password }).pipe(
      tap(resp => this.storeTokens(resp))
    );
  }

  register(email: string, displayName: string, password: string): Observable<UserDto> {
    return this.http.post<UserDto>(`${environment.apiBaseUrl}/auth/register`, { email, displayName, password });
  }

  refresh(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiBaseUrl}/auth/refresh`, {
      refreshToken: this._refreshToken,
    }).pipe(tap(resp => this.storeTokens(resp)));
  }

  /**
   * Returns a non-expired access token, triggering refresh if the current one is stale.
   * Used by RealtimeService.accessTokenFactory so long-lived WebSocket reconnects don't
   * silently fail after the 15-min JWT TTL. Returns null if refresh is impossible
   * (no refresh token OR refresh call errors) — caller handles by giving up.
   */
  async getFreshAccessToken(): Promise<string | null> {
    if (this._accessToken && !this.isAccessTokenExpired()) return this._accessToken;
    if (!this._refreshToken) return null;
    try {
      const resp = await new Promise<AuthResponse>((resolve, reject) => {
        this.refresh().subscribe({ next: resolve, error: reject });
      });
      return resp.accessToken;
    } catch {
      return null;
    }
  }

  logout(): void {
    const refresh = this._refreshToken;
    const access = this._accessToken;

    this.router.navigate(['/login']);

    if (refresh && access) {
      this.http.post(`${environment.apiBaseUrl}/auth/logout`, { refreshToken: refresh }).subscribe({
        next: () => this.clearTokens(),
        error: () => this.clearTokens(),
      });
    } else {
      this.clearTokens();
    }
  }

  storeTokens(resp: AuthResponse): void {
    this._accessToken = resp.accessToken;
    this._refreshToken = resp.refreshToken;
    this.user$.next(resp.user);
  }

  clearTokens(): void {
    this._accessToken = null;
    this._refreshToken = null;
    this.user$.next(null);
  }
}
