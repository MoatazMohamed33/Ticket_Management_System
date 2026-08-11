import { Component, HostListener, inject } from '@angular/core';
import { DialogService } from './dialog.service';

@Component({
  selector: 'app-dialog-host',
  standalone: true,
  templateUrl: './dialog-host.component.html',
  styleUrl: './dialog-host.component.scss',
})
export class DialogHostComponent {
  private readonly dialog = inject(DialogService);
  readonly current = this.dialog.current;

  ok(): void { this.dialog.close(true); }
  cancel(): void { this.dialog.close(false); }

  @HostListener('document:keydown.escape')
  onEsc(): void {
    const req = this.current();
    if (!req) return;
    // Alerts: Esc = dismiss (resolve true, same as OK). Confirms: Esc = cancel.
    this.dialog.close(req.kind === 'alert');
  }
}
