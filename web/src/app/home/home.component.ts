import { AsyncPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { DashboardService } from '../admin/dashboard/dashboard.service';
import { DashboardSummary } from '../admin/dashboard/dashboard.models';
import { TicketsService } from '../customer/tickets/tickets.service';
import { PagedResult, TicketListItem } from '../customer/tickets/tickets.models';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [AsyncPipe, RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly dashboard = inject(DashboardService);
  private readonly tickets = inject(TicketsService);

  readonly loading = signal(true);
  readonly summary = signal<DashboardSummary | null>(null);
  readonly myTickets = signal<PagedResult<TicketListItem> | null>(null);

  readonly role = computed(() => this.auth.user$.value?.role ?? null);

  readonly totalOpen = computed(() => {
    const s = this.summary();
    if (!s) return null;
    return (s.ticketCountsByStatus.open ?? 0) + (s.ticketCountsByStatus.inProgress ?? 0);
  });

  readonly avgResolutionLabel = computed(() => {
    const s = this.summary();
    if (!s || s.averageResolutionMinutes == null) return '—';
    const mins = s.averageResolutionMinutes;
    if (mins < 60) return `${mins}m`;
    const h = Math.floor(mins / 60);
    const m = mins % 60;
    return `${h}h ${m}m`;
  });

  ngOnInit(): void {
    const role = this.role();
    if (role === 'Admin') {
      this.dashboard.getSummary('30d').subscribe({
        next: s => { this.summary.set(s); this.loading.set(false); },
        error: () => this.loading.set(false),
      });
    } else if (role === 'Customer') {
      this.tickets.listMine({ page: 1, pageSize: 5 }).subscribe({
        next: r => { this.myTickets.set(r); this.loading.set(false); },
        error: () => this.loading.set(false),
      });
    } else if (role === 'SupportAgent') {
      this.tickets.listAssignedToMe({ page: 1, pageSize: 5 }).subscribe({
        next: r => { this.myTickets.set(r); this.loading.set(false); },
        error: () => this.loading.set(false),
      });
    } else {
      this.loading.set(false);
    }
  }
}
