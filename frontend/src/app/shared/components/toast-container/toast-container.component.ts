import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService, ToastItem } from '../../../core/services/toast.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './toast-container.component.html',
  styleUrls: ['./toast-container.component.css']
})
export class ToastContainerComponent {
  constructor(public toastService: ToastService) {}

  public getIcon(type: ToastItem['type']): string {
    switch (type) {
      case 'success': return '✅';
      case 'error': return '❌';
      case 'warning': return '⚠️';
      case 'info': return 'ℹ️';
    }
  }

  public getTitle(toast: ToastItem): string {
    if (toast.title) return toast.title;
    switch (toast.type) {
      case 'success': return '¡Operación Exitosa!';
      case 'error': return 'Ha ocurrido un error';
      case 'warning': return 'Atención';
      case 'info': return 'Información';
    }
  }

  public trackById(index: number, item: ToastItem): string {
    return item.id;
  }
}
