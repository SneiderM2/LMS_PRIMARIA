import { Component, ChangeDetectionStrategy, HostListener, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';

@Component({
  selector: 'app-confirm-dialog-host',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './confirm-dialog-host.component.html',
  styleUrls: ['./confirm-dialog-host.component.css']
})
export class ConfirmDialogHostComponent implements AfterViewChecked {
  @ViewChild('confirmBtn') confirmBtnRef?: ElementRef<HTMLButtonElement>;
  @ViewChild('promptInput') promptInputRef?: ElementRef<HTMLInputElement>;

  private previousIsOpen = false;

  constructor(public dialogService: ConfirmDialogService) {}

  @HostListener('document:keydown.escape', ['$event'])
  onEscape(e: Event): void {
    if (this.dialogService.state().isOpen) {
      e.preventDefault();
      this.dialogService.onCancel();
    }
  }

  @HostListener('document:keydown.enter', ['$event'])
  onEnter(e: KeyboardEvent): void {
    if (this.dialogService.state().isOpen && (e.target as HTMLElement)?.tagName !== 'TEXTAREA') {
      e.preventDefault();
      this.dialogService.onConfirm();
    }
  }

  ngAfterViewChecked(): void {
    const isOpen = this.dialogService.state().isOpen;
    if (isOpen && !this.previousIsOpen) {
      setTimeout(() => {
        if (this.dialogService.state().type === 'prompt' && this.promptInputRef) {
          this.promptInputRef.nativeElement.focus();
          this.promptInputRef.nativeElement.select();
        } else if (this.confirmBtnRef) {
          this.confirmBtnRef.nativeElement.focus();
        }
      }, 50);
    }
    this.previousIsOpen = isOpen;
  }
}
