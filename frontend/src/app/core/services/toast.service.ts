import { Injectable, signal } from '@angular/core';

export interface ToastItem {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  message: string;
  title?: string;
  durationMs: number;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  private readonly _toasts = signal<ToastItem[]>([]);
  public readonly toasts = this._toasts.asReadonly();

  public success(message: string, title?: string): void {
    this.show('success', message, title, 4500);
  }

  public error(message: string, title?: string): void {
    this.show('error', message, title, 7000);
  }

  public warning(message: string, title?: string): void {
    this.show('warning', message, title, 5000);
  }

  public info(message: string, title?: string): void {
    this.show('info', message, title, 4500);
  }

  public remove(id: string): void {
    this._toasts.update(items => items.filter(t => t.id !== id));
  }

  private show(type: ToastItem['type'], message: string, title?: string, durationMs: number = 4500): void {
    const id = `${Date.now()}_${Math.random().toString(36).substring(2, 7)}`;
    const toast: ToastItem = { id, type, message, title, durationMs };

    this._toasts.update(items => {
      // Máximo 4 toasts visibles simultáneos
      const sliced = items.length >= 4 ? items.slice(items.length - 3) : items;
      return [...sliced, toast];
    });

    setTimeout(() => {
      this.remove(id);
    }, durationMs);
  }
}
