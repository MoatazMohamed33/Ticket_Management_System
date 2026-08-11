import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { TicketsService } from '../../customer/tickets/tickets.service';
import {
  ListMyTicketsParams,
  PagedResult,
  TicketListItem,
  TicketPriority,
  TicketStatus,
} from '../../customer/tickets/tickets.models';

@Component({
  selector: 'app-assigned-tickets',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, RouterLink],
  templateUrl: './assigned-tickets.component.html',
  styleUrl: './assigned-tickets.component.scss',
})
export class AssignedTicketsComponent implements OnInit, OnDestroy {
  private static readonly LastSeenKey = 'agent-last-seen-tickets';

  private readonly fb = inject(FormBuilder);
  private readonly service = inject(TicketsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly realtime = inject(RealtimeService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  readonly result = signal<PagedResult<TicketListItem> | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly page = signal(1);
  readonly pageSize = 20;

  // Timestamp used for the unread-badge comparison. Captured once on init so rows don't
  // flicker "read" mid-view; the localStorage write happens on component destroy.
  private readonly lastSeen: Date | null;

  readonly statusOptions: TicketStatus[] = ['Open', 'InProgress', 'Resolved', 'Closed'];
  readonly priorityOptions: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical'];

  readonly filters = this.fb.nonNullable.group({
    search: [''],
    status: [[] as TicketStatus[]],
    priority: [[] as TicketPriority[]],
  });

  constructor() {
    const raw = typeof localStorage !== 'undefined'
      ? localStorage.getItem(AssignedTicketsComponent.LastSeenKey)
      : null;
    this.lastSeen = raw ? new Date(raw) : null;
  }

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.filters.patchValue({
      search: qp.get('search') ?? '',
      status: (qp.get('status')?.split(',').filter(Boolean) ?? []) as TicketStatus[],
      priority: (qp.get('priority')?.split(',').filter(Boolean) ?? []) as TicketPriority[],
    }, { emitEvent: false });
    this.page.set(Number(qp.get('page') ?? '1'));

    this.filters.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged((a, b) =>
        a.search === b.search &&
        (a.status ?? []).join(',') === (b.status ?? []).join(',') &&
        (a.priority ?? []).join(',') === (b.priority ?? []).join(',')),
    ).subscribe(() => {
      this.page.set(1);
      this.syncUrl();
      this.load();
    });

    this.load();

    // Story 7.2 — Agent's list gains/loses rows on TicketAssigned; refreshes on any
    // in-scope TicketUpdated. Refetch is the simplest correct strategy.
    const selfId = this.auth.user$.value?.id;
    this.realtime.subscribeToMyTickets().catch(() => { /* offline handled by shell badge */ });

    this.realtime.onTicketAssigned()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.newAssigneeId === selfId || e.oldAssigneeId === selfId) this.load();
      });

    this.realtime.onTicketUpdated()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.assignedAgentId === selfId) this.load();
      });

    this.realtime.reconnected
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.realtime.subscribeToMyTickets().catch(() => { /* silent */ });
        this.load();
      });
  }

  ngOnDestroy(): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(AssignedTicketsComponent.LastSeenKey, new Date().toISOString());
    }
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    const { search, status, priority } = this.filters.getRawValue();
    const params: ListMyTicketsParams = {
      page: this.page(),
      pageSize: this.pageSize,
      search: search || undefined,
      status: status?.length ? status : undefined,
      priority: priority?.length ? priority : undefined,
    };
    this.service.listAssignedToMe(params).subscribe({
      next: r => { this.result.set(r); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.status === 0 ? 'Cannot reach the server.' : 'Failed to load tickets.');
      },
    });
  }

  isUnread(t: TicketListItem): boolean {
    if (!this.lastSeen) return true;
    return new Date(t.updatedAt) > this.lastSeen;
  }

  toggleStatus(s: TicketStatus): void {
    const cur = this.filters.controls.status.value;
    this.filters.controls.status.setValue(cur.includes(s) ? cur.filter(x => x !== s) : [...cur, s]);
  }

  togglePriority(p: TicketPriority): void {
    const cur = this.filters.controls.priority.value;
    this.filters.controls.priority.setValue(cur.includes(p) ? cur.filter(x => x !== p) : [...cur, p]);
  }

  private syncUrl(): void {
    const { search, status, priority } = this.filters.getRawValue();
    const p = this.page();
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        search: search || null,
        status: status?.length ? status.join(',') : null,
        priority: priority?.length ? priority.join(',') : null,
        page: p > 1 ? p : null,
      },
      queryParamsHandling: 'merge',
    });
  }

  canGoNext(): boolean {
    const r = this.result();
    return !!r && this.page() < r.totalPages;
  }
  nextPage(): void { if (this.canGoNext()) { this.page.update(p => p + 1); this.syncUrl(); this.load(); } }
  prevPage(): void { if (this.page() > 1) { this.page.update(p => p - 1); this.syncUrl(); this.load(); } }
}
