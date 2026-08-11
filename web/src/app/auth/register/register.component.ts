import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  loading = false;
  topError: string | null = null;

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    displayName: ['', [Validators.required]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit(): void {
    if (this.form.invalid || this.loading) return;
    this.loading = true;
    this.topError = null;
    const { email, displayName, password } = this.form.getRawValue();
    this.auth.register(email, displayName, password).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/login']);
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.applyServerErrors(err, this.form);
        if (err.status === 0) this.topError = 'Cannot reach the server. Is the API running?';
        else if (err.status === 409) this.topError = 'That email is already registered.';
        else if (!err.error?.errors) this.topError = 'Registration failed.';
      },
    });
  }

  private applyServerErrors(err: HttpErrorResponse, form: FormGroup): void {
    const errors = err.error?.errors as Record<string, string[]> | undefined;
    if (!errors) return;
    for (const [field, messages] of Object.entries(errors)) {
      // Server returns camelCase per Story 2.2 JSON policy — no case conversion needed.
      const control = form.get(field);
      control?.setErrors({ server: messages.join(' ') });
    }
  }
}
