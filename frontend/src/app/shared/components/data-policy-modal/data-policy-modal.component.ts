import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '../../pipes/translate.pipe';
import { ConsentPayload, UserRole } from '../../../core/models/user.model';
import { environment } from '../../../../environments/environment';
import { GuardianConsentNoticeComponent } from '../guardian-consent-notice/guardian-consent-notice.component';

@Component({
  selector: 'app-data-policy-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe, GuardianConsentNoticeComponent],
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
  public guardianDocumentType: string = 'CC';
  public guardianAuthorization: boolean = false;
  public autorizaServicioEducativo: boolean = false;
  public autorizaDatosOpcionales: boolean = false;
  public autorizaTransmisionInternacional: boolean = true;
  public errorMessage: string = '';

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      this.resetForm();
    }
  }

  public onGuardianConsentChange(payload: ConsentPayload): void {
    this.guardianFullName = payload.guardianFullName || '';
    this.guardianDocument = payload.guardianDocument || '';
    this.guardianDocumentType = payload.guardianDocumentType || 'CC';
    this.autorizaServicioEducativo = payload.autorizaServicioEducativo === true;
    this.guardianAuthorization = this.autorizaServicioEducativo;
    this.autorizaDatosOpcionales = payload.autorizaDatosOpcionales === true;
    this.autorizaTransmisionInternacional = payload.autorizaTransmisionInternacional !== false;
  }

  public onAccept(): void {
    if (!this.consentGiven || this.submitting) {
      return;
    }

    if (this.requiresGuardian) {
      if (!this.guardianAuthorization || !this.autorizaServicioEducativo || this.guardianFullName.trim().length < 5) {
        this.errorMessage = 'Para menores de edad se requiere el nombre completo del padre, madre o acudiente y marcar la finalidad educativa esencial.';
        return;
      }
    }

    this.errorMessage = '';
    this.accepted.emit({
      version: this.policyVersion,
      guardianFullName: this.guardianFullName.trim() || undefined,
      guardianDocument: this.guardianDocument.trim() || undefined,
      guardianDocumentType: this.guardianDocumentType,
      guardianAuthorization: this.requiresGuardian ? this.guardianAuthorization : true,
      autorizaServicioEducativo: this.requiresGuardian ? this.autorizaServicioEducativo : true,
      autorizaDatosOpcionales: this.autorizaDatosOpcionales,
      autorizaTransmisionInternacional: this.autorizaTransmisionInternacional
    });
  }

  private resetForm(): void {
    this.consentGiven = false;
    this.guardianFullName = '';
    this.guardianDocument = '';
    this.guardianDocumentType = 'CC';
    this.guardianAuthorization = false;
    this.autorizaServicioEducativo = false;
    this.autorizaDatosOpcionales = false;
    this.autorizaTransmisionInternacional = true;
    this.errorMessage = '';
  }
}
