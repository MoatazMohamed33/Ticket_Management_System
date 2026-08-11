import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TicketsService } from './tickets.service';
import { TicketPriority } from './tickets.models';

@Component({
  selector: 'app-new-ticket',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './new-ticket.component.html',
  styleUrl: './new-ticket.component.scss',
})
export class NewTicketComponent {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(TicketsService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly topError = signal<string | null>(null);
  readonly maxDescription = 8000;

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(this.maxDescription)]],
    priority: ['Medium' as TicketPriority, [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid || this.loading()) return;
    this.loading.set(true);
    this.topError.set(null);

    this.service.create(this.form.getRawValue()).subscribe({
      next: ticket => {
        this.loading.set(false);
        // Story 4.3 detail page is where we ultimately land; until it exists in this session,
        // navigate to /customer/tickets (list page from 4.2).
        this.router.navigate(['/customer/tickets', ticket.id]);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.applyServerErrors(err, this.form);
        if (err.status === 429) this.topError.set('Too many tickets in a short time — try again in a minute.');
        else if (err.status === 0) this.topError.set('Cannot reach the server.');
        else if (!err.error?.errors) this.topError.set('Failed to create ticket.');
      },
    });
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
