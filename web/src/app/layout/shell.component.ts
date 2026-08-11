import { AsyncPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { RealtimeService } from '../core/realtime/realtime.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, AsyncPipe],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  readonly auth = inject(AuthService);
  readonly realtime = inject(RealtimeService);
  private readonly router = inject(Router);

  // Story 7.2 — signal exposed to template for the connection-status badge.
  readonly connectionState = this.realtime.connectionState;

  retryConnection(): void {
    this.realtime.ensureConnected().catch(() => { /* remains offline */ });
  }

  readonly menuOpen = signal(false);

  constructor() {
    // Close the mobile drawer on any navigation.
    this.router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(() => this.menuOpen.set(false));
  }

  toggleMenu(): void { this.menuOpen.update(v => !v); }
  closeMenu(): void { this.menuOpen.set(false); }

  initials(displayName: string): string {
    return displayName
      .split(/\s+/)
      .filter(Boolean)
      .map(w => w[0]!.toUpperCase())
      .slice(0, 2)
      .join('');
  }
}
