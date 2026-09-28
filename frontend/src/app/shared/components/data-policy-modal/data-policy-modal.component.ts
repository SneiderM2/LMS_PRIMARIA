import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-data-policy-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './data-policy-modal.component.html',
  styleUrls: ['./data-policy-modal.component.css']
})
export class DataPolicyModalComponent {
  @Input() visible: boolean = false;
  @Output() accepted = new EventEmitter<void>();

  public consentGiven: boolean = false;
  public isSubmitting: boolean = false;

  public onAccept(): void {
    if (!this.consentGiven) return;
    this.isSubmitting = true;
    this.accepted.emit();
  }
}
