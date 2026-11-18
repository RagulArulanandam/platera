import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  message: string;
  kind: 'info' | 'error' | 'success';
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly toasts = signal<Toast[]>([]);

  show(message: string, kind: Toast['kind'] = 'info', ms = 4000) {
    const id = this.nextId++;
    this.toasts.update((t) => [...t.slice(-3), { id, message, kind }]);
    setTimeout(() => this.dismiss(id), ms);
  }

  dismiss(id: number) {
    this.toasts.update((t) => t.filter((x) => x.id !== id));
  }
}
