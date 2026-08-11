import { AsyncPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { UserRole } from '../../core/auth/auth.models';
import { DialogService } from '../../core/ui/dialog.service';
import { AdminUsersService } from './admin-users.service';
import { AdminUser, PagedResult } from './admin-users.models';
import { CreateUserDialogComponent } from './create-user-dialog.component';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, AsyncPipe, CreateUserDialogComponent],
  templateUrl: './users.component.html',
  styleUrl: './users.component.scss',
})
export class UsersComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AdminUsersService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);
  private readonly dialog = inject(DialogService);

  readonly result = signal<PagedResult<AdminUser> | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly showCreate = signal(false);
  readonly actionInFlight = signal<string | null>(null);   // userId currently being mutated
  readonly page = signal(1);

  readonly filters = this.fb.nonNullable.group({
    search: [''],
    role: [''],
  });

  readonly pageSize = 20;

  ngOnInit(): void {
    // Restore state from URL
    const qp = this.route.snapshot.queryParamMap;
    this.filters.patchValue({
      search: qp.get('search') ?? '',
      role: qp.get('role') ?? '',
    }, { emitEvent: false });
    this.page.set(Number(qp.get('page') ?? '1'));

    this.filters.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged((a, b) => a.search === b.search && a.role === b.role),
    ).subscribe(() => {
      this.page.set(1);
      this.syncUrl();
      this.load();
    });

    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    const { search, role } = this.filters.getRawValue();
    this.service.list({
      page: this.page(),
      pageSize: this.pageSize,
      search: search || undefined,
      role: (role || undefined) as UserRole | undefined,
    }).subscribe({
      next: r => {
        this.result.set(r);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.status === 0
          ? 'Cannot reach the server.'
          : 'Failed to load users.');
      },
    });
  }

  private syncUrl(): void {
    const { search, role } = this.filters.getRawValue();
    const p = this.page();
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        search: search || null,
        role: role || null,
        page: p > 1 ? p : null,
      },
      queryParamsHandling: 'merge',
    });
  }

  canGoNext(): boolean {
    const r = this.result();
    return !!r && this.page() < r.totalPages;
  }

  nextPage(): void {
    if (this.canGoNext()) {
      this.page.update(p => p + 1);
      this.syncUrl();
      this.load();
    }
  }

  prevPage(): void {
    if (this.page() > 1) {
      this.page.update(p => p - 1);
      this.syncUrl();
      this.load();
    }
  }

  openCreate(): void { this.showCreate.set(true); }

  onCreated(user: AdminUser): void {
    this.showCreate.set(false);
    // Optimistic prepend + refresh totals via reload
    this.load();
  }

  onCancelCreate(): void { this.showCreate.set(false); }

  isSelf(user: AdminUser): boolean {
    return this.auth.user$.value?.id === user.id;
  }

  async toggleActive(user: AdminUser): Promise<void> {
    if (this.isSelf(user) && user.isActive) {
      await this.dialog.alert('You cannot deactivate your own account.', { tone: 'danger' });
      return;
    }
    const action = user.isActive ? 'Deactivate' : 'Activate';
    const ok = await this.dialog.confirm(
      `${action} ${user.displayName}?`,
      { title: `${action} user`, okLabel: action, tone: user.isActive ? 'danger' : 'info' },
    );
    if (!ok) return;

    this.actionInFlight.set(user.id);
    const call$ = user.isActive
      ? this.service.deactivate(user.id)
      : this.service.activate(user.id);

    call$.subscribe({
      next: () => {
        this.actionInFlight.set(null);
        // Patch the row locally
        this.result.update(r => {
          if (!r) return r;
          return {
            ...r,
            items: r.items.map(u => u.id === user.id ? { ...u, isActive: !u.isActive } : u),
          };
        });
      },
      error: err => {
        this.actionInFlight.set(null);
        this.dialog.alert(err?.error?.detail ?? 'Action failed.', { tone: 'danger' });
      },
    });
  }

  async changeRole(user: AdminUser, newRole: string, selectEl?: HTMLSelectElement): Promise<void> {
    // Reset the DOM select back to the server-truth role. Cheaper than reloading the list.
    const revert = () => { if (selectEl) selectEl.value = user.role; };

    if (this.isSelf(user)) {
      await this.dialog.alert('You cannot change your own role.', { tone: 'danger' });
      revert();
      return;
    }
    if (newRole === user.role) return;
    const ok = await this.dialog.confirm(
      `Change ${user.displayName}'s role from ${user.role} to ${newRole}?`,
      { title: 'Change role', okLabel: 'Change role' },
    );
    if (!ok) {
      revert();
      return;
    }

    this.actionInFlight.set(user.id);
    this.service.changeRole(user.id, newRole as UserRole).subscribe({
      next: () => {
        this.actionInFlight.set(null);
        this.result.update(r => {
          if (!r) return r;
          return {
            ...r,
            items: r.items.map(u => u.id === user.id ? { ...u, role: newRole as UserRole } : u),
          };
        });
        this.dialog.alert('Role updated. User must log in again.');
      },
      error: err => {
        this.actionInFlight.set(null);
        this.dialog.alert(err?.error?.detail ?? 'Failed to change role.', { tone: 'danger' });
        revert();
      },
    });
  }
}
