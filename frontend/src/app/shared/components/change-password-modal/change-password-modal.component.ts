import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { PasswordPolicyService } from '../../../core/services/password-policy.service';
import { PasswordPolicyMeterComponent } from '../password-policy-meter/password-policy-meter.component';

@Component({
  selector: 'app-change-password-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, PasswordPolicyMeterComponent],
  templateUrl: './change-password-modal.component.html',
  styleUrls: ['./change-password-modal.component.css']
})
export class ChangePasswordModalComponent {
  @Input() isOpen = false;
  @Output() close = new EventEmitter<void>();
  @Output() passwordChanged = new EventEmitter<void>();

  public currentPassword = '';
  public newPassword = '';
  public confirmNewPassword = '';

  public showCurrentPass = false;
  public showNewPass = false;
  public showConfirmPass = false;

  public isSubmitting = false;
  public errorMessage: string | null = null;

  constructor(
    private authService: AuthService,
    private toastService: ToastService,
    private policyService: PasswordPolicyService
  ) {}

  public get currentUser() {
    return this.authService.currentUser();
  }

  public get passwordsMatch(): boolean {
    if (!this.confirmNewPassword) return true;
    return this.newPassword === this.confirmNewPassword;
  }

  public get isFormValid(): boolean {
    if (!this.currentPassword.trim() || !this.newPassword.trim() || !this.confirmNewPassword.trim()) {
      return false;
    }
    if (this.newPassword !== this.confirmNewPassword) {
      return false;
    }
    const validation = this.policyService.validate(
      this.newPassword,
      this.currentUser?.id,
      this.currentUser?.fullName
    );
    return validation.isValid;
  }

  public onCancel(): void {
    this.resetForm();
    this.close.emit();
  }

  public onSubmit(): void {
    if (!this.isFormValid) {
      return;
    }

    if (this.currentPassword === this.newPassword) {
      this.errorMessage = 'La nueva contraseña debe ser diferente a la contraseña actual.';
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = null;

    this.authService.changePassword(this.currentPassword, this.newPassword, this.confirmNewPassword)
      .subscribe({
        next: (res) => {
          this.isSubmitting = false;
          this.toastService.success(res?.message || 'Tu contraseña ha sido actualizada con éxito.', 'Contraseña segura');
          this.resetForm();
          this.passwordChanged.emit();
          this.close.emit();
        },
        error: (err) => {
          this.isSubmitting = false;
          this.errorMessage = err?.error?.message || 'No fue posible cambiar la contraseña. Verifica tu contraseña actual.';
        }
      });
  }

  private resetForm(): void {
    this.currentPassword = '';
    this.newPassword = '';
    this.confirmNewPassword = '';
    this.showCurrentPass = false;
    this.showNewPass = false;
    this.showConfirmPass = false;
    this.errorMessage = null;
  }
}
