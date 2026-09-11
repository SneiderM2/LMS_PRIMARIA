import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { UserRole } from '../../../core/models/user.model';


@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent {
  public activeTab: 'login' | 'register' = 'login';

  // Campos de Iniciar Sesión
  public id = '';
  public password = '';

  // Campos de Registro
  public regId = '';
  public regFullName = '';
  public regPassword = '';
  public regConfirmPassword = '';
  public regRole: UserRole = 'Student';
  public regGrade = '1°';

  public isLoading = false;
  public errorMessage: string | null = null;
  public successMessage: string | null = null;

  constructor(private authService: AuthService) { }

  public switchTab(tab: 'login' | 'register'): void {
    this.activeTab = tab;
    this.errorMessage = null;
    this.successMessage = null;
  }

  public onLogin(): void {
    if (!this.id.trim() || !this.password.trim()) {
      this.errorMessage = 'Por favor escribe tu carnet/documento y contraseña escolar 📝';
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;
    this.successMessage = null;

    this.authService.login({ id: this.id.trim(), password: this.password }).subscribe({
      next: () => {
        this.isLoading = false;
      },
      error: (err: any) => {
        this.isLoading = false;
        this.errorMessage = err?.error?.message || 'Identificación escolar o contraseña incorrecta. ¡Inténtalo de nuevo!';
      }
    });
  }

  public onRegister(): void {
    if (!this.regId.trim() || !this.regFullName.trim() || !this.regPassword.trim()) {
      this.errorMessage = 'Por favor completa todos los campos requeridos 📝';
      return;
    }

    if (this.regPassword.length < 4) {
      this.errorMessage = 'La contraseña debe tener al menos 4 caracteres 🔒';
      return;
    }

    if (this.regPassword !== this.regConfirmPassword) {
      this.errorMessage = 'Las contraseñas no coinciden. Verifícalas por favor ❌';
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;
    this.successMessage = null;

    this.authService.register({
      id: this.regId.trim(),
      fullName: this.regFullName.trim(),
      password: this.regPassword,
      role: this.regRole,
      grade: this.regRole === 'Student' ? this.regGrade : undefined
    }).subscribe({
      next: () => {
        this.isLoading = false;
        this.successMessage = '¡Cuenta creada con éxito! Ingresando a tu aula... 🎉';
      },
      error: (err: any) => {
        this.isLoading = false;
        this.errorMessage = err?.error?.message || 'No se pudo crear la cuenta. Verifica los datos e inténtalo nuevamente.';
      }
    });
  }
}