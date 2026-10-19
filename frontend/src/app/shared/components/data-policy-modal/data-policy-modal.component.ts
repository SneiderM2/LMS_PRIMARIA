import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '../../pipes/translate.pipe';
import { ConsentPayload, UserRole } from '../../../core/models/user.model';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-data-policy-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './data-policy-modal.component.html',
  styleUrls: ['./data-policy-modal.component.css']
})
export class DataPolicyModalComponent implements OnChanges {
  @Input() visible: boolean = false;
  @Input() userRole: UserRole | null = null;
  @Input() policyVersion: string = environment.dataPolicyVersion;
  @Input() policyUrl: string = environment.dataPolicyUrl;
  @Input() requiresGuardian: boolean = false;
  @Input() submitting: boolean = false;

  @Output() accepted = new EventEmitter<ConsentPayload>();

  public consentGiven: boolean = false;
  public guardianFullName: string = '';
  public guardianDocument: string = '';
  public guardianAuthorization: boolean = false;
  public errorMessage: string = '';

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      this.resetForm();
    }
  }

  public onAccept(): void {
    if (!this.consentGiven || this.submitting) {
      return;
    }

    if (this.requiresGuardian) {
      if (!this.guardianAuthorization || this.guardianFullName.trim().length < 5) {
        this.errorMessage = 'Para menores de edad se requiere el nombre completo del padre, madre o acudiente y marcar la autorización.';
        return;
      }
    }

    this.errorMessage = '';
    this.accepted.emit({
      version: this.policyVersion,
      guardianFullName: this.guardianFullName.trim() || undefined,
      guardianDocument: this.guardianDocument.trim() || undefined,
      guardianAuthorization: this.guardianAuthorization
    });
  }

  private resetForm(): void {
    this.consentGiven = false;
    this.guardianFullName = '';
    this.guardianDocument = '';
    this.guardianAuthorization = false;
    this.errorMessage = '';
  }
}
