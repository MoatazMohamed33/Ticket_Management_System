import { Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';

/**
 * Non-dismissive banner shown when a ticket mutation returns 409. Only "Reload" closes it.
 * conflictAt / conflictBy are best-effort — the API deliberately doesn't leak "who edited
 * when" to prevent info disclosure, so the banner falls back to generic text when absent.
 */
@Component({
  selector: 'app-concurrency-banner',
  standalone: true,
  template: `
    <div class="concurrency-banner" role="alert">
      <span class="banner-icon" aria-hidden="true">⚠️</span>
      <span class="banner-msg">{{ message() }}</span>
      <button type="button" class="btn" (click)="reload.emit()">Reload</button>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .concurrency-banner {
      display: flex; align-items: center; gap: var(--space-3);
      padding: var(--space-3) var(--space-4);
      background: #fee2e2; color: #991b1b;
      border: 1px solid #fca5a5; border-radius: var(--radius-md);
      margin-bottom: var(--space-4);
      position: sticky; top: 0; z-index: 10;
    }
    .banner-icon { font-size: 20px; }
    .banner-msg { flex: 1; font-weight: 500; }
    .concurrency-banner .btn { min-height: 32px; padding: 4px 16px; }
  `],
})
export class ConcurrencyBannerComponent {
  @Input() conflictAt: Date | null = null;
  @Input() conflictBy: string | null = null;
  @Output() reload = new EventEmitter<void>();

  private readonly at = signal<Date | null>(null);
  private readonly by = signal<string | null>(null);

  ngOnChanges(): void {
    this.at.set(this.conflictAt);
    this.by.set(this.conflictBy);
  }

  readonly message = computed(() => {
    const at = this.at();
    const by = this.by();
    if (by && at) return `This ticket was updated by ${by} at ${at.toLocaleTimeString()}. Reload to see the latest, then re-apply your change.`;
    if (by)       return `This ticket was updated by ${by}. Reload to see the latest, then re-apply your change.`;
    return 'This ticket was updated by another user. Reload to see the latest, then re-apply your change.';
  });
}
