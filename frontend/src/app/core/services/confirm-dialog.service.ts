import { Injectable, signal } from '@angular/core';

export interface ConfirmDialogOptions {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  tone?: 'danger' | 'primary' | 'warning';
}

export interface PromptDialogOptions {
  title: string;
  message: string;
  placeholder?: string;
  initialValue?: string;
  confirmText?: string;
  cancelText?: string;
}

interface DialogState {
  isOpen: boolean;
  type: 'confirm' | 'prompt';
  title: string;
  message: string;
  confirmText: string;
  cancelText: string;
  tone: 'danger' | 'primary' | 'warning';
  inputValue: string;
  placeholder: string;
  resolve?: (value: any) => void;
}

@Injectable({
  providedIn: 'root'
})
export class ConfirmDialogService {
  private readonly _state = signal<DialogState>({
    isOpen: false,
    type: 'confirm',
    title: '',
    message: '',
    confirmText: 'Confirmar',
    cancelText: 'Cancelar',
    tone: 'primary',
    inputValue: '',
    placeholder: ''
  });

  public readonly state = this._state.asReadonly();

  public confirm(options: ConfirmDialogOptions): Promise<boolean> {
    return new Promise<boolean>((resolve) => {
      this._state.set({
        isOpen: true,
        type: 'confirm',
        title: options.title,
        message: options.message,
        confirmText: options.confirmText || 'Confirmar',
        cancelText: options.cancelText || 'Cancelar',
        tone: options.tone || 'primary',
        inputValue: '',
        placeholder: '',
        resolve
      });
    });
  }

  public prompt(options: PromptDialogOptions): Promise<string | null> {
    return new Promise<string | null>((resolve) => {
      this._state.set({
        isOpen: true,
        type: 'prompt',
        title: options.title,
        message: options.message,
        confirmText: options.confirmText || 'Aceptar',
        cancelText: options.cancelText || 'Cancelar',
        tone: 'primary',
        inputValue: options.initialValue || '',
        placeholder: options.placeholder || '',
        resolve
      });
    });
  }

  public updateInputValue(val: string): void {
    this._state.update(s => ({ ...s, inputValue: val }));
  }

  public onConfirm(): void {
    const s = this._state();
    if (s.resolve) {
      if (s.type === 'confirm') {
        s.resolve(true);
      } else {
        s.resolve(s.inputValue);
      }
    }
    this.close();
  }

  public onCancel(): void {
    const s = this._state();
    if (s.resolve) {
      if (s.type === 'confirm') {
        s.resolve(false);
      } else {
        s.resolve(null);
      }
    }
    this.close();
  }

  private close(): void {
    this._state.update(s => ({ ...s, isOpen: false, resolve: undefined }));
  }
}
