import { Component, OnInit, AfterViewInit, OnDestroy, NgZone } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { UserRole } from '../../../core/models/user.model';
import { SessionTimeoutService } from '../../../core/services/session-timeout.service';
import { ServerStatusService } from '../../../core/services/server-status.service';
import { PasswordPolicyService } from '../../../core/services/password-policy.service';
import { PasswordPolicyMeterComponent } from '../../../shared/components/password-policy-meter/password-policy-meter.component';
import { environment } from '../../../../environments/environment';

import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

declare const google: any;
declare const grecaptcha: any;

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe, PasswordPolicyMeterComponent],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit, AfterViewInit, OnDestroy {
  public activeTab: 'login' | 'register' = 'login';

  private readonly destroy$ = new Subject<void>();
  private grecaptchaIntervalId: ReturnType<typeof setInterval> | null = null;
  private googleIntervalId: ReturnType<typeof setInterval> | null = null;
  private googleTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private tabSwitchTimerId: ReturnType<typeof setTimeout> | null = null;

  // Campos de Iniciar Sesión
  public id = '';
  public password = '';
  public showLoginPassword = false;

  // Sprint 4: Honeypot (trampa invisible para bots)
  public honeypotTrap = '';

  // Sprint 4: CAPTCHA Token
  public captchaToken = '';
  public captchaWidgetId: any = null;
  public captchaRegWidgetId: any = null;
  public isProduction = environment.production;
  public captchaLoadError = false;
  public isDevCaptchaActive = false;

  // Campos de Registro
  public regId = '';
  public regFullName = '';
  public regPassword = '';
  public regConfirmPassword = '';
  public showRegPassword = false;
  public showRegConfirmPassword = false;
  public regRole: UserRole = 'Student';
  public regGrade = '1°';
  public regHoneypot = '';
  public regTeacherCode = '';
  public regGuardianName = '';
  public regGuardianDocument = '';
  public regGuardianAuth = false;
  public regAcceptPolicy = false;

  public readonly policyVersion = environment.dataPolicyVersion;
  public readonly policyUrl = environment.dataPolicyUrl;

  public isLoading = false;
  public errorMessage: string | null = null;
  public successMessage: string | null = null;

  // Precalentamiento del backend en segundo plano (elimina los 20 segundos de cold start)
  public isServerWarming = true;
  public isServerReady = false;
  public isServerUnavailable = false;
  private warmupAttempts = 0;
  private warmupRetryTimerId: ReturnType<typeof setTimeout> | null = null;

  // Modal de sesión activa en otro dispositivo
  public showActiveSessionModal = false;
  public activeSessionInfo: { message: string; sessionStartedAt?: string; deviceHint?: string } | null = null;
  private pendingForceLoginType: 'credentials' | 'google' = 'credentials';
  private pendingGoogleToken: string | null = null;

  constructor(
    private authService: AuthService,
    private sessionTimeoutService: SessionTimeoutService,
    private passwordPolicyService: PasswordPolicyService,
    private serverStatus: ServerStatusService,
    private route: ActivatedRoute,
    private ngZone: NgZone
  ) { }

  ngOnInit(): void {
    // 1. Precalentar el servidor en segundo plano tan pronto abre la pantalla
    this.warmupBackend();

    // 2. Si viene redirigido por inactividad de sesión
    this.route.queryParams
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        if (params['sessionExpired'] === 'true') {
          this.errorMessage = '⏱️ Tu sesión ha expirado por inactividad (5 minutos). Por favor ingresa nuevamente.';
        } else if (params['duplicateSession'] === 'true') {
          this.errorMessage = '⚠️ Tu sesión se ha cerrado porque se inició sesión en otro dispositivo.';
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    if (this.grecaptchaIntervalId) clearInterval(this.grecaptchaIntervalId);
    if (this.googleIntervalId) clearInterval(this.googleIntervalId);
    if (this.googleTimeoutId) clearTimeout(this.googleTimeoutId);
    if (this.tabSwitchTimerId) clearTimeout(this.tabSwitchTimerId);
    if (this.warmupRetryTimerId) clearTimeout(this.warmupRetryTimerId);
  }

  private warmupBackend(): void {
    this.isServerWarming = true;
    this.isServerReady = false;
    this.isServerUnavailable = false;

    this.authService.warmup()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.ngZone.run(() => {
            this.isServerReady = true;
            this.isServerWarming = false;
            this.isServerUnavailable = false;
          });
        },
        error: () => {
          this.ngZone.run(() => {
            this.warmupAttempts++;
            // El cold start de Render puede tardar; reintentamos con espera
            // creciente antes de mostrar un estado de error honesto.
            if (this.warmupAttempts < 5) {
              this.warmupRetryTimerId = setTimeout(
                () => this.warmupBackend(),
                3000 * this.warmupAttempts
              );
            } else {
              this.isServerWarming = false;
              this.isServerUnavailable = true;
              // Evita que el banner global siga anunciando un arranque que ya
              // agotó sus reintentos; el pill muestra el estado real.
              this.serverStatus.setWakingUp(false);
            }
          });
        }
      });
  }

  ngAfterViewInit(): void {
    this.initRecaptcha();
    this.initGoogleSignIn();
  }

  public bypassCaptchaDev(): void {
    if (!this.isProduction) {
      this.captchaToken = 'DEV_TEST_TOKEN';
      this.isDevCaptchaActive = true;
      this.errorMessage = null;
    }
  }

  private initRecaptcha(): void {
    const siteKey = environment.recaptchaSiteKey;
    if (!siteKey) {
      console.warn('[reCAPTCHA] No hay site key configurada; el registro y el login no estarán disponibles.');
      this.captchaLoadError = true;
      return;
    }

    let attempts = 0;
    const maxAttempts = 15; // ~4.5 segundos
    this.grecaptchaIntervalId = setInterval(() => {
      attempts++;
      if (typeof grecaptcha !== 'undefined' && grecaptcha.render) {
        if (this.grecaptchaIntervalId) clearInterval(this.grecaptchaIntervalId);
        this.renderLoginCaptcha(siteKey);
        if (this.activeTab === 'register') {
          this.renderRegisterCaptcha(siteKey);
        }
      } else if (attempts >= maxAttempts) {
        if (this.grecaptchaIntervalId) clearInterval(this.grecaptchaIntervalId);
        console.warn('[reCAPTCHA] SDK no disponible tras tiempo límite de espera.');
        this.ngZone.run(() => {
          this.captchaLoadError = true;
        });
      }
    }, 300);
  }

  private renderLoginCaptcha(siteKey: string): void {
    const loginContainer = document.getElementById('recaptcha-container');
    if (loginContainer && this.captchaWidgetId === null) {
      try {
        this.captchaWidgetId = grecaptcha.render('recaptcha-container', {
          sitekey: siteKey,
          callback: (token: string) => {
            this.ngZone.run(() => {
              this.captchaToken = token;
              this.isDevCaptchaActive = false;
              this.errorMessage = null;
            });
          },
          'expired-callback': () => {
            this.ngZone.run(() => {
              this.captchaToken = '';
            });
          },
          'error-callback': () => {
            this.ngZone.run(() => {
              console.warn('[reCAPTCHA Login] Error reportado por el widget.');
              this.captchaLoadError = true;
            });
          }
        });
      } catch (e) {
        console.warn('[reCAPTCHA Login] error al inicializar:', e);
      }
    }
  }

  private renderRegisterCaptcha(siteKey: string): void {
    const regContainer = document.getElementById('recaptcha-reg-container');
    if (regContainer && this.captchaRegWidgetId === null) {
      try {
        this.captchaRegWidgetId = grecaptcha.render('recaptcha-reg-container', {
          sitekey: siteKey,
          callback: (token: string) => {
            this.ngZone.run(() => {
              this.captchaToken = token;
              this.isDevCaptchaActive = false;
              this.errorMessage = null;
            });
          },
          'expired-callback': () => {
            this.ngZone.run(() => {
              this.captchaToken = '';
            });
          },
          'error-callback': () => {
            this.ngZone.run(() => {
              console.warn('[reCAPTCHA Registro] Error reportado por el widget.');
              this.captchaLoadError = true;
            });
          }
        });
      } catch (e) {
        console.warn('[reCAPTCHA Registro] error al inicializar:', e);
      }
    }
  }

  private ensureRecaptchaForTab(tab: 'login' | 'register'): void {
    const siteKey = environment.recaptchaSiteKey;
    if (!siteKey || typeof grecaptcha === 'undefined' || !grecaptcha.render) {
      return;
    }
    if (tab === 'login') {
      this.renderLoginCaptcha(siteKey);
    } else {
      this.renderRegisterCaptcha(siteKey);
    }
  }

  private initGoogleSignIn(): void {
    this.googleIntervalId = setInterval(() => {
      if (typeof google !== 'undefined' && google.accounts && google.accounts.id) {
        if (this.googleIntervalId) clearInterval(this.googleIntervalId);
        try {
          const clientId = environment.googleClientId;
          if (clientId) {
            google.accounts.id.initialize({
              client_id: clientId,
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
          }
        } catch (err) {
          console.warn('Google Identity init:', err);
        }
      }
    }, 300);

    this.googleTimeoutId = setTimeout(() => {
      if (this.googleIntervalId) clearInterval(this.googleIntervalId);
    }, 6000);
  }

  public handleGoogleCredential(idToken: string): void {
    this.isLoading = true;
    this.errorMessage = null;

    this.authService.loginWithGoogle(idToken, this.honeypotTrap)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.isLoading = false;
          this.sessionTimeoutService.startMonitoring();
        },
        error: (err: any) => {
          this.isLoading = false;
          if (err?.status === 409 && err?.error?.code === 'ACTIVE_SESSION') {
            // Hay una sesión activa en otro lugar → mostrar modal de confirmación
            this.pendingForceLoginType = 'google';
            this.pendingGoogleToken = idToken;
            this.activeSessionInfo = {
              message: err.error.message,
              sessionStartedAt: err.error.sessionStartedAt,
              deviceHint: err.error.deviceHint
            };
            this.showActiveSessionModal = true;
          } else {
            this.errorMessage = err?.error?.message || 'No fue posible iniciar sesión con Google.';
          }
        }
      });
  }

  public switchTab(tab: 'login' | 'register'): void {
    this.activeTab = tab;
    this.errorMessage = null;
    this.successMessage = null;
    this.captchaToken = '';
    this.isDevCaptchaActive = false;
    if (this.tabSwitchTimerId) clearTimeout(this.tabSwitchTimerId);
    this.tabSwitchTimerId = setTimeout(() => {
      this.ensureRecaptchaForTab(tab);
    }, 100);
  }

  public onLogin(): void {
    if (!this.id.trim() || !this.password.trim()) {
      this.errorMessage = 'Por favor escribe tu carnet/documento y contraseña escolar 📝';
      return;
    }

    // Validación de reCAPTCHA con soporte de entorno de desarrollo
    if (!this.captchaToken) {
      if (!this.isProduction) {
        this.captchaToken = 'DEV_TEST_TOKEN';
        this.isDevCaptchaActive = true;
      } else {
        this.errorMessage = this.captchaLoadError
          ? 'No se pudo cargar la verificación de seguridad (reCAPTCHA). Si tienes un bloqueador de anuncios activo (AdBlock, Brave Shields), por favor desactívalo y recarga la página.'
          : 'Por favor completa la verificación de seguridad reCAPTCHA ("No soy un robot") 🤖';
        return;
      }
    }

    this.isLoading = true;
    this.errorMessage = null;
    this.successMessage = null;

    this.doLogin(false);
  }

  /**
   * Lógica de login reutilizable. Se llama con forceLogin=false la primera vez
   * y con forceLogin=true cuando el usuario confirma cerrar la sesión activa.
   */
  private doLogin(forceLogin: boolean): void {
    this.authService.login({
      id: this.id.trim(),
      password: this.password,
      honeypotTrap: this.honeypotTrap,
      captchaToken: this.captchaToken
    }, forceLogin)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
      next: () => {
        this.isLoading = false;
        this.sessionTimeoutService.startMonitoring();
      },
      error: (err: any) => {
        this.isLoading = false;
        const status: number = err?.status;
        const serverMessage: string | undefined = err?.error?.message;

        // 409 ACTIVE_SESSION → mostrar modal de confirmación de cierre de sesión activa
        if (status === 409 && err?.error?.code === 'ACTIVE_SESSION') {
          this.pendingForceLoginType = 'credentials';
          this.pendingGoogleToken = null;
          this.activeSessionInfo = {
            message: err.error.message,
            sessionStartedAt: err.error.sessionStartedAt,
            deviceHint: err.error.deviceHint
          };
          this.showActiveSessionModal = true;
          return;
        }

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
          case 423:
            this.errorMessage = serverMessage || '🔒 Tu cuenta ha sido bloqueada temporalmente por seguridad. Intenta de nuevo en 15 minutos.';
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

  /**
   * El usuario confirmó cerrar la sesión activa y continuar el login.
   */
  public onConfirmForceLogin(): void {
    this.showActiveSessionModal = false;
    this.activeSessionInfo = null;
    this.isLoading = true;
    this.errorMessage = null;

    if (this.pendingForceLoginType === 'google' && this.pendingGoogleToken) {
      this.authService.loginWithGoogle(this.pendingGoogleToken, this.honeypotTrap, true)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.isLoading = false;
            this.sessionTimeoutService.startMonitoring();
          },
          error: (err: any) => {
            this.isLoading = false;
            this.errorMessage = err?.error?.message || 'No fue posible iniciar sesión con Google.';
          }
        });
    } else {
      this.doLogin(true);
    }
  }

  /**
   * El usuario canceló el modal → no se toma ninguna acción.
   */
  public onCancelForceLogin(): void {
    this.showActiveSessionModal = false;
    this.activeSessionInfo = null;
    this.isLoading = false;
  }

  private validateNormalizedPassword(pwd: string): string | null {
    const res = this.passwordPolicyService.validate(pwd, this.regId, this.regFullName);
    if (!res.isValid) {
      return res.errors[0] || 'La contraseña debe tener entre 8 y 64 caracteres, incluyendo mayúscula, minúscula, número y símbolo (@, #, !, etc.) 🔒';
    }
    return null;
  }

  public onRegister(): void {
    if (!this.regId.trim() || !this.regFullName.trim() || !this.regPassword.trim()) {
      this.errorMessage = 'Por favor completa todos los campos requeridos 📝';
      return;
    }

    const pwdError = this.validateNormalizedPassword(this.regPassword);
    if (pwdError) {
      this.errorMessage = pwdError;
      return;
    }

    if (this.regPassword !== this.regConfirmPassword) {
      this.errorMessage = 'Las contraseñas no coinciden. Verifícalas por favor ❌';
      return;
    }

    if (this.regRole === 'Teacher' && !this.regTeacherCode.trim()) {
      this.errorMessage = 'El registro de docentes requiere el código de invitación institucional 🔑';
      return;
    }

    if (this.regRole === 'Student') {
      if (!this.regGuardianAuth || this.regGuardianName.trim().length < 5) {
        this.errorMessage = 'Para registrar a un menor de edad se requiere el nombre del acudiente y su autorización (Ley 1581 de 2012, art. 7).';
        return;
      }
    }

    if (!this.regAcceptPolicy) {
      this.errorMessage = 'Debes aceptar la política de tratamiento de datos personales para crear la cuenta.';
      return;
    }

    // Validación de reCAPTCHA en Registro con soporte de entorno de desarrollo
    if (!this.captchaToken) {
      if (!this.isProduction) {
        this.captchaToken = 'DEV_TEST_TOKEN';
        this.isDevCaptchaActive = true;
      } else {
        this.errorMessage = this.captchaLoadError
          ? 'No se pudo cargar la verificación de seguridad (reCAPTCHA). Si tienes un bloqueador de anuncios activo (AdBlock, Brave Shields), por favor desactívalo y recarga la página.'
          : 'Por favor completa la verificación de seguridad reCAPTCHA ("No soy un robot") 🤖';
        return;
      }
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
      teacherInviteCode: this.regRole === 'Teacher' ? this.regTeacherCode.trim() : undefined,
      guardianFullName: this.regRole === 'Student' ? this.regGuardianName.trim() : undefined,
      guardianDocument: this.regRole === 'Student' ? this.regGuardianDocument.trim() : undefined,
      guardianAuthorization: this.regRole === 'Student' ? this.regGuardianAuth : false,
      acceptDataPolicy: true,
      dataPolicyVersion: environment.dataPolicyVersion,
      honeypotTrap: this.regHoneypot,
      captchaToken: this.captchaToken
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
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