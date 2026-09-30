import { Component, OnInit, AfterViewInit, NgZone } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { UserRole } from '../../../core/models/user.model';
import { SessionTimeoutService } from '../../../core/services/session-timeout.service';
import { environment } from '../../../../environments/environment';

declare const google: any;
declare const grecaptcha: any;

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit, AfterViewInit {
  public activeTab: 'login' | 'register' = 'login';

  // Campos de Iniciar Sesión
  public id = '';
  public password = '';

  // Sprint 4: Honeypot (trampa invisible para bots)
  public honeypotTrap = '';

  // Sprint 4: CAPTCHA Token
  public captchaToken = '';
  public captchaWidgetId: any = null;
  public captchaRegWidgetId: any = null;

  // Campos de Registro
  public regId = '';
  public regFullName = '';
  public regPassword = '';
  public regConfirmPassword = '';
  public regRole: UserRole = 'Student';
  public regGrade = '1°';
  public regHoneypot = '';

  public isLoading = false;
  public errorMessage: string | null = null;
  public successMessage: string | null = null;

  constructor(
    private authService: AuthService,
    private sessionTimeoutService: SessionTimeoutService,
    private route: ActivatedRoute,
    private ngZone: NgZone
  ) { }

  ngOnInit(): void {
    // Si viene redirigido por inactividad de sesión
    this.route.queryParams.subscribe(params => {
      if (params['sessionExpired'] === 'true') {
        this.errorMessage = '⏱️ Tu sesión ha expirado por inactividad (5 minutos). Por favor ingresa nuevamente.';
      }
    });
  }

  ngAfterViewInit(): void {
    this.initRecaptcha();
    this.initGoogleSignIn();
  }

  private initRecaptcha(): void {
    const siteKey = environment.recaptchaSiteKey || '6LcMQNgtAAAAADA_qovyBzaHt_EDrKEMxTyCeqXo';

    // Esperar a que el SDK de Google reCAPTCHA esté listo
    const checkGrecaptcha = setInterval(() => {
      if (typeof grecaptcha !== 'undefined' && grecaptcha.render) {
        clearInterval(checkGrecaptcha);

        // 1. Widget de reCAPTCHA en el formulario de Login
        const loginContainer = document.getElementById('recaptcha-container');
        if (loginContainer && this.captchaWidgetId === null) {
          try {
            this.captchaWidgetId = grecaptcha.render('recaptcha-container', {
              sitekey: siteKey,
              callback: (token: string) => {
                this.ngZone.run(() => {
                  this.captchaToken = token;
                  this.errorMessage = null;
                });
              },
              'expired-callback': () => {
                this.ngZone.run(() => {
                  this.captchaToken = '';
                });
              }
            });
          } catch (e) {
            console.warn('[reCAPTCHA Login] error al inicializar:', e);
          }
        }

        // 2. Widget de reCAPTCHA en el formulario de Registro
        const regContainer = document.getElementById('recaptcha-reg-container');
        if (regContainer && this.captchaRegWidgetId === null) {
          try {
            this.captchaRegWidgetId = grecaptcha.render('recaptcha-reg-container', {
              sitekey: siteKey,
              callback: (token: string) => {
                this.ngZone.run(() => {
                  this.captchaToken = token;
                  this.errorMessage = null;
                });
              },
              'expired-callback': () => {
                this.ngZone.run(() => {
                  this.captchaToken = '';
                });
              }
            });
          } catch (e) {
            console.warn('[reCAPTCHA Registro] error al inicializar:', e);
          }
        }
      }
    }, 300);

    setTimeout(() => clearInterval(checkGrecaptcha), 10000);
  }

  private initGoogleSignIn(): void {
    const checkGoogle = setInterval(() => {
      if (typeof google !== 'undefined' && google.accounts && google.accounts.id) {
        clearInterval(checkGoogle);
        try {
          google.accounts.id.initialize({
            client_id: '782910394812-sampleclientidforexample.apps.googleusercontent.com',
            callback: (res: any) => this.handleGoogleCredential(res.credential)
          });

          const btnContainer = document.getElementById('googleLoginBtnContainer');
          if (btnContainer) {
            google.accounts.id.renderButton(btnContainer, {
              theme: 'outline',
              size: 'large',
              shape: 'pill',
              text: 'signin_with',
              locale: 'es'
            });
          }
        } catch (err) {
          console.warn('Google Identity init:', err);
        }
      }
    }, 300);

    setTimeout(() => clearInterval(checkGoogle), 6000);
  }

  public handleGoogleCredential(idToken: string): void {
    this.isLoading = true;
    this.errorMessage = null;

    this.authService.loginWithGoogle(idToken, this.honeypotTrap).subscribe({
      next: () => {
        this.isLoading = false;
        this.sessionTimeoutService.startMonitoring();
      },
      error: (err: any) => {
        this.isLoading = false;
        this.errorMessage = err?.error?.message || 'No fue posible iniciar sesión con Google.';
      }
    });
  }

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

    // Validación obligatoria de reCAPTCHA
    if (!this.captchaToken) {
      this.errorMessage = 'Por favor completa la verificación de seguridad reCAPTCHA ("No soy un robot") 🤖';
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;
    this.successMessage = null;

    this.authService.login({ 
      id: this.id.trim(), 
      password: this.password,
      honeypotTrap: this.honeypotTrap,
      captchaToken: this.captchaToken
    }).subscribe({
      next: () => {
        this.isLoading = false;
        this.sessionTimeoutService.startMonitoring();
      },
      error: (err: any) => {
        this.isLoading = false;
        const status: number = err?.status;
        const serverMessage: string | undefined = err?.error?.message;

        // Resetear widget de captcha en caso de fallo para permitir nuevo intento
        if (typeof grecaptcha !== 'undefined' && this.captchaWidgetId !== null) {
          try {
            grecaptcha.reset(this.captchaWidgetId);
          } catch (e) {
            console.warn('Error reseteando reCAPTCHA login:', e);
          }
        }
        this.captchaToken = '';

        switch (status) {
          case 400:
            this.errorMessage = serverMessage || 'Verificación de seguridad reCAPTCHA falló o los datos ingresados no son válidos.';
            break;
          case 401:
            this.errorMessage = serverMessage || 'Identificación escolar o contraseña incorrecta. ¡Inténtalo de nuevo!';
            break;
          case 409:
            this.errorMessage = '⚠️ Ocurrió un problema al procesar tu solicitud. Por favor intenta de nuevo en un momento.';
            console.error('[Login 409]', err?.error);
            break;
          case 423:
            this.errorMessage = serverMessage || '🔒 Tu cuenta ha sido bloqueada temporalmente. Intenta de nuevo en 10 minutos.';
            break;
          case 503:
            this.errorMessage = '🔌 El servicio de autenticación no está disponible en este momento. Intenta de nuevo en unos segundos.';
            break;
          case 500:
            this.errorMessage = '⚠️ Error interno del servidor. Por favor contacta al administrador escolar.';
            break;
          default:
            this.errorMessage = serverMessage || 'No fue posible conectarse al servidor escolar. Verifica tu conexión a internet.';
        }
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

    // Validación obligatoria de reCAPTCHA en Registro
    if (!this.captchaToken) {
      this.errorMessage = 'Por favor completa la verificación de seguridad reCAPTCHA ("No soy un robot") 🤖';
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
      grade: this.regRole === 'Student' ? this.regGrade : undefined,
      honeypotTrap: this.regHoneypot,
      captchaToken: this.captchaToken
    }).subscribe({
      next: () => {
        this.isLoading = false;
        this.successMessage = '¡Cuenta creada con éxito! Ingresando a tu aula... 🎉';
        this.sessionTimeoutService.startMonitoring();
      },
      error: (err: any) => {
        this.isLoading = false;
        this.errorMessage = err?.error?.message || 'No se pudo crear la cuenta. Verifica los datos e inténtalo nuevamente.';
        
        // Resetear widget de captcha en caso de fallo para permitir nuevo intento
        if (typeof grecaptcha !== 'undefined' && this.captchaRegWidgetId !== null) {
          try {
            grecaptcha.reset(this.captchaRegWidgetId);
          } catch (e) {
            console.warn('Error reseteando reCAPTCHA registro:', e);
          }
        }
        this.captchaToken = '';
      }
    });
  }
}