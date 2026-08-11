import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { AdminUsersService } from '../../admin/users/admin-users.service';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { DialogService } from '../../core/ui/dialog.service';
import { TicketsService } from './tickets.service';
import { ActivityEntry, Comment, TicketDetail, TicketStatus } from './tickets.models';
import { ConcurrencyBannerComponent } from './concurrency-banner.component';

type FeedItem =
  | { kind: 'comment'; at: string; item: Comment }
  | { kind: 'activity'; at: string; item: ActivityEntry };

@Component({
  selector: 'app-ticket-detail',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, RouterLink, ConcurrencyBannerComponent],
  templateUrl: './ticket-detail.component.html',
  styleUrl: './ticket-detail.component.scss',
})
export class TicketDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(TicketsService);
  private readonly adminUsers = inject(AdminUsersService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  private readonly dialog = inject(DialogService);

  private currentTicketId: string | null = null;

  readonly ticket = signal<TicketDetail | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly posting = signal(false);
  readonly closing = signal(false);
  readonly statusChanging = signal(false);
  readonly priorityChanging = signal(false);
  readonly assigning = signal(false);
  readonly commentError = signal<string | null>(null);
  readonly conflict = signal<{ at: Date | null; by: string | null } | null>(null);
  readonly agents = signal<{ id: string; displayName: string }[]>([]);
  readonly maxBody = 4000;
  readonly priorityOptions: string[] = ['Low', 'Medium', 'High', 'Critical'];

  readonly commentForm = this.fb.nonNullable.group({
    body: ['', [Validators.required, Validators.maxLength(this.maxBody)]],
  });

  readonly loggingTime = signal(false);
  readonly timeLogError = signal<string | null>(null);
  readonly timeLogForm = this.fb.nonNullable.group({
    workedOn: [this.today(), [Validators.required]],
    durationMinutes: [30, [Validators.required, Validators.min(1), Validators.max(1440)]],
    description: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  // Public so the template can bind [max]="today()" on the date input to prevent
  // future-date selection client-side (server also enforces via validator).
  today(): string {
    return new Date().toISOString().slice(0, 10);
  }

  readonly feed = computed<FeedItem[]>(() => {
    const t = this.ticket();
    if (!t) return [];
    const items: FeedItem[] = [
      ...t.comments.map(c => ({ kind: 'comment' as const, at: c.createdAt, item: c })),
      ...t.activity.map(a => ({ kind: 'activity' as const, at: a.at, item: a })),
    ];
    return items.sort((x, y) => x.at.localeCompare(y.at));
  });

  readonly totalLogged = computed(() => {
    const t = this.ticket();
    if (!t) return '0:00';
    const h = Math.floor(t.totalLoggedMinutes / 60);
    const m = t.totalLoggedMinutes % 60;
    return `${h}:${m.toString().padStart(2, '0')}`;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.error.set('Missing ticket id.'); return; }
    this.currentTicketId = id;
    this.load(id);

    // Populate agent dropdown for Admin's assignee picker (Story 6.3).
    if (this.auth.user$.value?.role === 'Admin') {
      this.adminUsers.list({ role: 'SupportAgent', pageSize: 100 }).subscribe({
        next: r => this.agents.set(r.items.map(a => ({ id: a.id, displayName: a.displayName }))),
        error: () => { /* non-blocking */ },
      });
    }

    // Story 7.2 — SignalR subscribe + live-update wiring. Refetch on any relevant event.
    this.realtime.subscribeToTicket(id).catch(() => { /* graceful — offline path handled by shell badge */ });

    this.realtime.onTicketUpdated()
      .pipe(filter(e => e.ticketId === id), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load(id));

    this.realtime.onCommentAdded()
      .pipe(filter(e => e.ticketId === id), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load(id));

    this.realtime.onTimeEntryLogged()
      .pipe(filter(e => e.ticketId === id), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load(id));

    this.realtime.onTicketAssigned()
      .pipe(filter(e => e.ticketId === id), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load(id));

    // On reconnect, missed events during offline window need reconciliation.
    this.realtime.reconnected
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.realtime.subscribeToTicket(id).catch(() => { /* silent */ });
        this.load(id);
      });
  }

  ngOnDestroy(): void {
    if (this.currentTicketId) {
      this.realtime.unsubscribeFromTicket(this.currentTicketId).catch(() => { /* silent */ });
    }
  }

  load(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.getDetail(id).subscribe({
      next: t => { this.ticket.set(t); this.loading.set(false); this.conflict.set(null); },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        if (err.status === 404) this.error.set("This ticket doesn't exist or you don't have access.");
        else if (err.status === 0) this.error.set('Cannot reach the server.');
        else this.error.set('Failed to load ticket.');
      },
    });
  }

  postComment(): void {
    const t = this.ticket();
    if (!t || this.commentForm.invalid || this.posting()) return;
    this.posting.set(true);
    this.commentError.set(null);
    this.service.addComment(t.id, this.commentForm.getRawValue()).subscribe({
      next: c => {
        this.posting.set(false);
        this.commentForm.reset({ body: '' });
        // Optimistic append for instant UX.
        this.ticket.update(cur => cur ? { ...cur, comments: [...cur.comments, c] } : cur);
        // Server bumped Ticket.UpdatedAt on the tracked entity → RowVersion is now stale on
        // the client. Refetch in the background so a subsequent Close call doesn't 409 on a
        // fresh rowVersion mismatch.
        this.service.getDetail(t.id).subscribe({
          next: fresh => this.ticket.set(fresh),
          error: () => { /* silent — optimistic list is correct; only rowVersion matters here */ },
        });
      },
      error: (err: HttpErrorResponse) => {
        this.posting.set(false);
        if (err.status === 429) this.commentError.set('Too many requests — try again shortly.');
        else if (err.status === 404) {
          this.commentError.set('Ticket no longer accessible.');
          this.router.navigate(['/customer/tickets']);
        }
        else if (err.error?.errors?.body) this.commentError.set(err.error.errors.body.join(' '));
        else this.commentError.set('Failed to post comment.');
      },
    });
  }

  isOwnerCustomer(): boolean {
    const t = this.ticket();
    const u = this.auth.user$.value;
    return !!t && !!u && u.role === 'Customer' && t.customer.id === u.id;
  }

  canClose(): boolean {
    const t = this.ticket();
    return !!t && t.status === 'Resolved' && this.isOwnerCustomer();
  }

  readonly canMutateStatus = computed(() => {
    const t = this.ticket();
    const u = this.auth.user$.value;
    if (!t || !u) return false;
    if (u.role === 'Admin') return true;
    if (u.role === 'SupportAgent') return t.assignedAgent?.id === u.id;
    return false;
  });

  readonly legalNextStatuses = computed<TicketStatus[]>(() => {
    const t = this.ticket();
    if (!t || !this.canMutateStatus()) return [];
    switch (t.status) {
      case 'Open':       return ['InProgress'];
      case 'InProgress': return ['Open', 'Resolved'];
      case 'Resolved':   return ['InProgress'];
      case 'Closed':     return [];
      default:           return [];
    }
  });

  // Admin-only: change priority + reassign
  readonly canMutatePriority = computed(() => this.auth.user$.value?.role === 'Admin');
  readonly canAssign = computed(() => this.auth.user$.value?.role === 'Admin');

  async onPriorityChange(newPriority: string, selectEl: HTMLSelectElement): Promise<void> {
    const t = this.ticket();
    if (!t || this.priorityChanging()) return;
    if (newPriority === t.priority) return;
    const ok = await this.dialog.confirm(`Change priority to ${newPriority}?`, {
      title: 'Change priority', okLabel: 'Change priority',
    });
    if (!ok) {
      selectEl.value = t.priority;
      return;
    }

    this.priorityChanging.set(true);
    this.service.changePriority(t.id, newPriority, t.rowVersion).subscribe({
      next: () => { this.priorityChanging.set(false); this.load(t.id); },
      error: async (err: HttpErrorResponse) => {
        this.priorityChanging.set(false);
        selectEl.value = t.priority;
        if (err.status === 409) this.conflict.set({ at: null, by: null });
        else if (err.status === 404) {
          await this.dialog.alert('This ticket is no longer available.', { tone: 'danger' });
          this.router.navigate(['/admin/tickets']);
        }
        else this.dialog.alert('Failed to change priority.', { tone: 'danger' });
      },
    });
  }

  async onAssigneeChange(rawValue: string, selectEl: HTMLSelectElement): Promise<void> {
    const t = this.ticket();
    if (!t || this.assigning()) return;
    const newAssigneeId: string | null = rawValue === '' ? null : rawValue;
    const currentId = t.assignedAgent?.id ?? null;
    if (newAssigneeId === currentId) return;

    const label = newAssigneeId
      ? this.agents().find(a => a.id === newAssigneeId)?.displayName ?? 'agent'
      : null;
    const msg = newAssigneeId ? `Assign this ticket to ${label}?` : 'Unassign this ticket?';
    const ok = await this.dialog.confirm(msg, {
      title: newAssigneeId ? 'Assign ticket' : 'Unassign ticket',
      okLabel: newAssigneeId ? 'Assign' : 'Unassign',
    });
    if (!ok) {
      selectEl.value = currentId ?? '';
      return;
    }

    this.assigning.set(true);
    this.service.assign(t.id, newAssigneeId, t.rowVersion).subscribe({
      next: () => { this.assigning.set(false); this.load(t.id); },
      error: async (err: HttpErrorResponse) => {
        this.assigning.set(false);
        selectEl.value = currentId ?? '';
        if (err.status === 409) this.conflict.set({ at: null, by: null });
        else if (err.status === 400 && err.error?.errors?.assignedAgentId)
          this.dialog.alert(err.error.errors.assignedAgentId.join(' '), { tone: 'danger' });
        else if (err.status === 404) {
          await this.dialog.alert('This ticket is no longer available.', { tone: 'danger' });
          this.router.navigate(['/admin/tickets']);
        }
        else this.dialog.alert('Failed to change assignee.', { tone: 'danger' });
      },
    });
  }

  logTime(): void {
    const t = this.ticket();
    if (!t || this.timeLogForm.invalid || this.loggingTime()) return;
    this.loggingTime.set(true);
    this.timeLogError.set(null);
    const raw = this.timeLogForm.getRawValue();
    this.service.logTime(t.id, {
      workedOn: raw.workedOn,
      durationMinutes: raw.durationMinutes,
      description: raw.description,
    }).subscribe({
      next: entry => {
        this.loggingTime.set(false);
        this.timeLogForm.patchValue({ description: '', durationMinutes: 30 });
        this.ticket.update(cur => {
          if (!cur) return cur;
          // Sort by workedOn ascending to match the server-side query order (Story 4.3).
          // Users can log entries for past dates → keep the local list in canonical order.
          const updatedEntries = [...cur.timeEntries, entry].sort((a, b) =>
            a.workedOn.localeCompare(b.workedOn));
          return {
            ...cur,
            timeEntries: updatedEntries,
            totalLoggedMinutes: updatedEntries.reduce((sum, e) => sum + e.durationMinutes, 0),
          };
        });
      },
      error: (err: HttpErrorResponse) => {
        this.loggingTime.set(false);
        if (err.status === 400 && err.error?.errors) {
          const first = Object.values(err.error.errors as Record<string, string[]>)[0];
          this.timeLogError.set(first?.join(' ') ?? 'Invalid input.');
        } else if (err.status === 429) {
          this.timeLogError.set('Too many requests — try again shortly.');
        } else if (err.status === 404) {
          this.timeLogError.set('Ticket no longer accessible.');
          this.router.navigate(['/customer/tickets']);
        } else {
          this.timeLogError.set('Failed to log time.');
        }
      },
    });
  }

  async onStatusChange(newStatus: string, selectEl: HTMLSelectElement): Promise<void> {
    const t = this.ticket();
    if (!t || this.statusChanging()) return;
    if (newStatus === t.status) return;   // belt-and-suspenders; "current" option is inert

    const target = newStatus as TicketStatus;
    const ok = await this.dialog.confirm(`Change status to ${target}?`, {
      title: 'Change status', okLabel: 'Change status',
    });
    if (!ok) {
      selectEl.value = t.status;
      return;
    }

    this.statusChanging.set(true);
    this.service.changeStatus(t.id, target, t.rowVersion).subscribe({
      next: () => {
        this.statusChanging.set(false);
        // Refetch full detail (rowVersion + activity feed + updatedAt).
        // changeStatus returns TicketDto, not TicketDetail — full refresh is simpler than merging.
        this.load(t.id);
      },
      error: async (err: HttpErrorResponse) => {
        this.statusChanging.set(false);
        selectEl.value = t.status;
        if (err.status === 409) {
          this.conflict.set({ at: null, by: null });
        } else if (err.status === 400) {
          this.dialog.alert(err.error?.detail ?? 'Illegal status transition.', { tone: 'danger' });
          this.load(t.id);
        } else if (err.status === 404) {
          await this.dialog.alert('This ticket is no longer available.', { tone: 'danger' });
          this.router.navigate(['/customer/tickets']);
        } else {
          this.dialog.alert('Failed to change status.', { tone: 'danger' });
        }
      },
    });
  }

  async close(): Promise<void> {
    const t = this.ticket();
    if (!t || !this.canClose() || this.closing()) return;
    const ok = await this.dialog.confirm(
      "Close this ticket? You'll still be able to view it, but the conversation ends here.",
      { title: 'Close ticket', okLabel: 'Close ticket' },
    );
    if (!ok) return;

    this.closing.set(true);
    this.service.close(t.id, t.rowVersion).subscribe({
      next: () => {
        this.closing.set(false);
        this.load(t.id);   // refetch to pull in the TicketClosed activity entry + new rowVersion
      },
      error: async (err: HttpErrorResponse) => {
        this.closing.set(false);
        if (err.status === 409) {
          // Story 5.3 — non-dismissive banner, no auto-refetch. Reload button in the banner
          // drives the refresh; the user's draft comment (if any) is preserved.
          this.conflict.set({ at: null, by: null });
        } else if (err.status === 400) {
          this.dialog.alert(err.error?.detail ?? 'Cannot close this ticket right now.', { tone: 'danger' });
          this.load(t.id);
        } else if (err.status === 404) {
          await this.dialog.alert('This ticket is no longer available.', { tone: 'danger' });
          this.router.navigate(['/customer/tickets']);
        } else {
          this.dialog.alert('Failed to close ticket.', { tone: 'danger' });
        }
      },
    });
  }

  activityLabel(a: ActivityEntry): string {
    return a.summary || a.event;
  }
}
