import { Injectable, signal } from '@angular/core';

export type DialogKind = 'alert' | 'confirm';

export interface DialogRequest {
  id: number;
  kind: DialogKind;
  title: string;
  message: string;
  okLabel: string;
  cancelLabel?: string;
  tone: 'info' | 'danger';
  resolve: (ok: boolean) => void;
}

@Injectable({ providedIn: 'root' })
export class DialogService {
  private nextId = 1;
  readonly current = signal<DialogRequest | null>(null);

  alert(message: string, opts: { title?: string; tone?: 'info' | 'danger'; okLabel?: string } = {}): Promise<void> {
    return new Promise(resolve => {
      this.current.set({
        id: this.nextId++,
        kind: 'alert',
        title: opts.title ?? 'Notice',
        message,
        okLabel: opts.okLabel ?? 'OK',
        tone: opts.tone ?? 'info',
        resolve: () => resolve(),
      });
    });
  }

  confirm(
    message: string,
    opts: { title?: string; okLabel?: string; cancelLabel?: string; tone?: 'info' | 'danger' } = {},
  ): Promise<boolean> {
    return new Promise(resolve => {
      this.current.set({
        id: this.nextId++,
        kind: 'confirm',
        title: opts.title ?? 'Confirm',
        message,
        okLabel: opts.okLabel ?? 'OK',
        cancelLabel: opts.cancelLabel ?? 'Cancel',
        tone: opts.tone ?? 'info',
        resolve,
      });
    });
  }

  close(ok: boolean): void {
    const req = this.current();
    if (!req) return;
    this.current.set(null);
    req.resolve(ok);
  }
}
