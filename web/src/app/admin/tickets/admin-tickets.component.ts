import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { debounceTime, distinctUntilChanged, merge } from 'rxjs';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { DialogService } from '../../core/ui/dialog.service';
import { AdminUsersService } from '../users/admin-users.service';
import { AdminUser } from '../users/admin-users.models';
import { TicketsService } from '../../customer/tickets/tickets.service';
import {
  ListMyTicketsParams,
  PagedResult,
  TicketListItem,
  TicketPriority,
  TicketStatus,
} from '../../customer/tickets/tickets.models';

@Component({
  selector: 'app-admin-tickets',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, RouterLink],
  templateUrl: './admin-tickets.component.html',
  styleUrl: './admin-tickets.component.scss',
})
export class AdminTicketsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(TicketsService);
  private readonly adminUsers = inject(AdminUsersService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly realtime = inject(RealtimeService);
  private readonly dialog = inject(DialogService);
  private readonly destroyRef = inject(DestroyRef);

  readonly assigning = signal<string | null>(null);

  readonly result = signal<PagedResult<TicketListItem> | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly page = signal(1);
  readonly pageSize = 20;

  readonly agents = signal<AdminUser[]>([]);
  readonly customers = signal<AdminUser[]>([]);

  readonly statusOptions: TicketStatus[] = ['Open', 'InProgress', 'Resolved', 'Closed'];
  readonly priorityOptions: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical'];

  readonly filters = this.fb.nonNullable.group({
    search: [''],
    status: [[] as TicketStatus[]],
    priority: [[] as TicketPriority[]],
    assignedAgentId: [''],   // '' = no filter; 'unassigned' = null-agent; guid string otherwise
    customerId: [''],
  });

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.filters.patchValue({
      search: qp.get('search') ?? '',
      status: (qp.get('status')?.split(',').filter(Boolean) ?? []) as TicketStatus[],
      priority: (qp.get('priority')?.split(',').filter(Boolean) ?? []) as TicketPriority[],
      assignedAgentId: qp.get('assignedAgentId') ?? '',
      customerId: qp.get('customerId') ?? '',
    }, { emitEvent: false });
    this.page.set(Number(qp.get('page') ?? '1'));

    // Populate agent + customer dropdowns once. 100 rows covers MVP scale.
    this.adminUsers.list({ role: 'SupportAgent', pageSize: 100 })
      .subscribe(r => this.agents.set(r.items));
    this.adminUsers.list({ role: 'Customer', pageSize: 100 })
      .subscribe(r => this.customers.set(r.items));

    this.filters.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged((a, b) =>
        a.search === b.search &&
        (a.status ?? []).join(',') === (b.status ?? []).join(',') &&
        (a.priority ?? []).join(',') === (b.priority ?? []).join(',') &&
        a.assignedAgentId === b.assignedAgentId &&
        a.customerId === b.customerId),
    ).subscribe(() => {
      this.page.set(1);
      this.syncUrl();
      this.load();
    });

    this.load();

    // Story 7.2 — Admin sees all ticket events (server broadcasts to "admins" group).
    // Merge the 3 event streams and refetch on any emission.
    this.realtime.subscribeToMyTickets().catch(() => { /* offline handled by shell badge */ });
    merge(
      this.realtime.onTicketCreated(),
      this.realtime.onTicketUpdated(),
      this.realtime.onTicketAssigned(),
    )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load());

    this.realtime.reconnected
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.realtime.subscribeToMyTickets().catch(() => { /* silent */ });
        this.load();
      });
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    const { search, status, priority, assignedAgentId, customerId } = this.filters.getRawValue();
    const params: ListMyTicketsParams = {
      page: this.page(),
      pageSize: this.pageSize,
      search: search || undefined,
      status: status?.length ? status : undefined,
      priority: priority?.length ? priority : undefined,
      assignedAgentId: assignedAgentId || undefined,
      customerId: customerId || undefined,
    };
    this.service.listAllForAdmin(params).subscribe({
      next: r => { this.result.set(r); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.status === 0 ? 'Cannot reach the server.' : 'Failed to load tickets.');
      },
    });
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
    const { search, status, priority, assignedAgentId, customerId } = this.filters.getRawValue();
    const p = this.page();
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        search: search || null,
        status: status?.length ? status.join(',') : null,
        priority: priority?.length ? priority.join(',') : null,
        assignedAgentId: assignedAgentId || null,
        customerId: customerId || null,
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

  async onInlineAssign(t: TicketListItem, rawValue: string, selectEl: EventTarget | null): Promise<void> {
    if (this.assigning() === t.id) return;
    const newAssigneeId: string | null = rawValue === '' ? null : rawValue;
    if (newAssigneeId === t.assignedAgentId) return;

    const label = newAssigneeId
      ? this.agents().find(a => a.id === newAssigneeId)?.displayName ?? 'agent'
      : null;
    const msg = newAssigneeId ? `Assign this ticket to ${label}?` : 'Unassign this ticket?';
    const ok = await this.dialog.confirm(msg, {
      title: newAssigneeId ? 'Assign ticket' : 'Unassign ticket',
      okLabel: newAssigneeId ? 'Assign' : 'Unassign',
    });
    if (!ok) {
      // Revert the select's DOM value back to the row's server-truth id.
      (selectEl as HTMLSelectElement).value = t.assignedAgentId ?? '';
      return;
    }

    this.assigning.set(t.id);
    this.service.assign(t.id, newAssigneeId, t.rowVersion).subscribe({
      next: () => { this.assigning.set(null); this.load(); },
      error: (err: HttpErrorResponse) => {
        this.assigning.set(null);
        (selectEl as HTMLSelectElement).value = t.assignedAgentId ?? '';
        if (err.status === 409) {
          this.dialog.alert(
            'This ticket was updated by someone else. Refreshing the list.',
            { tone: 'danger' },
          );
          this.load();
        } else if (err.status === 400 && err.error?.errors?.assignedAgentId) {
          this.dialog.alert(err.error.errors.assignedAgentId.join(' '), { tone: 'danger' });
        } else {
          this.dialog.alert('Failed to assign ticket.', { tone: 'danger' });
        }
      },
    });
  }
}
