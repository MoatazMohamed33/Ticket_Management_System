import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="page">
      <div class="card wrap">
        <div class="icon" aria-hidden="true">🚫</div>
        <h1>403 · Forbidden</h1>
        <p class="muted">You don't have access to this page.</p>
        <a routerLink="/" class="btn">Back to dashboard</a>
      </div>
    </div>
  `,
  styles: [`
    .page {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 60vh;
      padding: var(--space-6);
    }
    .wrap {
      text-align: center;
      max-width: 420px;
      padding: var(--space-8);
    }
    .icon { font-size: 48px; margin-bottom: var(--space-4); }
    h1 { margin-bottom: var(--space-2); }
    p  { margin-bottom: var(--space-5); }
    a  { text-decoration: none; }
  `],
})
export class ForbiddenComponent {}
