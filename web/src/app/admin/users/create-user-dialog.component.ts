import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Output, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminUsersService } from './admin-users.service';
import { AdminUser } from './admin-users.models';

@Component({
  selector: 'app-create-user-dialog',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="backdrop" (click)="cancel()"></div>
    <div class="dialog card" role="dialog" aria-labelledby="create-user-title">
      <h2 id="create-user-title">Create new user</h2>

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="form-group">
          <label for="new-email">Email</label>
          <input id="new-email" formControlName="email" type="email"
                 placeholder="new.user@example.com" autocomplete="off"
                 [class.invalid]="!!form.controls.email.errors?.['server']" />
          @if (form.controls.email.errors?.['server']) {
            <small class="form-error">{{ form.controls.email.errors!['server'] }}</small>
          }
        </div>

        <div class="form-group">
          <label for="new-name">Display name</label>
          <input id="new-name" formControlName="displayName" type="text"
                 [class.invalid]="!!form.controls.displayName.errors?.['server']" />
          @if (form.controls.displayName.errors?.['server']) {
            <small class="form-error">{{ form.controls.displayName.errors!['server'] }}</small>
          }
        </div>

        <div class="form-group">
          <label>Role</label>
          <div class="role-choice">
            <label class="role-option">
              <input type="radio" formControlName="role" value="SupportAgent" />
              <span>Support Agent</span>
            </label>
            <label class="role-option">
              <input type="radio" formControlName="role" value="Admin" />
              <span>Admin</span>
            </label>
          </div>
        </div>

        <div class="form-group">
          <label for="new-password">Initial password</label>
          <input id="new-password" formControlName="initialPassword" type="password"
                 autocomplete="new-password"
                 [class.invalid]="!!form.controls.initialPassword.errors?.['server']" />
          <small class="form-hint">8+ chars, upper + lower + digit. User can change it later.</small>
          @if (form.controls.initialPassword.errors?.['server']) {
            <small class="form-error">{{ form.controls.initialPassword.errors!['server'] }}</small>
          }
        </div>

        @if (topError) {
          <div class="alert alert-danger" role="alert">{{ topError }}</div>
        }

        <div class="actions">
          <button type="button" class="btn btn-secondary" (click)="cancel()" [disabled]="loading">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || loading">
            @if (loading) { Creating… } @else { Create user }
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .backdrop {
      position: fixed; inset: 0;
      background: rgb(15 23 42 / 0.4);
      z-index: 100;
    }
    .dialog {
      position: fixed;
      top: 50%; left: 50%;
      transform: translate(-50%, -50%);
      width: 100%; max-width: 480px;
      max-height: 90vh; overflow-y: auto;
      z-index: 101;
      box-shadow: var(--shadow-lg);
    }
    .dialog h2 { margin: 0 0 var(--space-5); font-size: var(--font-size-xl); }
    .role-choice { display: flex; gap: var(--space-3); }
    .role-option {
      flex: 1; display: flex; align-items: center; gap: var(--space-2);
      padding: var(--space-3); border: 1px solid var(--color-border-strong);
      border-radius: var(--radius-md); cursor: pointer;
      margin-bottom: 0; font-weight: normal;
    }
    .role-option input[type=radio] { width: auto; margin: 0; }
    .actions {
      display: flex; justify-content: flex-end; gap: var(--space-2);
      margin-top: var(--space-5);
    }
    @media (max-width: 480px) {
      .dialog { max-width: calc(100vw - 32px); }
    }
  `],
})
export class CreateUserDialogComponent {
  @Output() created = new EventEmitter<AdminUser>();
  @Output() closed = new EventEmitter<void>();

  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AdminUsersService);

  loading = false;
  topError: string | null = null;

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    displayName: ['', [Validators.required]],
    role: ['SupportAgent' as 'SupportAgent' | 'Admin', [Validators.required]],
    initialPassword: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit(): void {
    if (this.form.invalid || this.loading) return;
    this.loading = true;
    this.topError = null;

    this.service.create(this.form.getRawValue()).subscribe({
      next: user => {
        this.loading = false;
        this.created.emit(user);
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.applyServerErrors(err, this.form);
        if (err.status === 409) this.topError = 'That email is already registered.';
        else if (err.status === 0) this.topError = 'Cannot reach the server.';
        else if (!err.error?.errors) this.topError = 'Failed to create user.';
      },
    });
  }

  cancel(): void {
    if (this.loading) return;
    this.closed.emit();
  }

  private applyServerErrors(err: HttpErrorResponse, form: FormGroup): void {
    const errors = err.error?.errors as Record<string, string[]> | undefined;
    if (!errors) return;
    for (const [field, messages] of Object.entries(errors)) {
      const control = form.get(field);
      control?.setErrors({ server: messages.join(' ') });
    }
  }
}
