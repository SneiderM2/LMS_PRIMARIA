import { Injectable, NgZone, OnDestroy, effect } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, Subscription, timer, interval } from 'rxjs';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class SessionTimeoutService implements OnDestroy {
  // Computadores compartidos en aulas de primaria: aviso a los 2.5 min y cierre/limpieza total tras 5 min de inactividad
  private readonly WARNING_MS = 2.5 * 60 * 1000; // 150 segundos (2.5 minutos)
  private readonly LOGOUT_MS = 5 * 60 * 1000;    // 300 segundos (5 minutos)

  // Polling de sesión única: cada 15 segundos verifica que la sesión siga activa y no haya sido desplazada
  private readonly SESSION_POLL_INTERVAL_MS = 15 * 1000;

  // Throttle de eventos de actividad del usuario (evita recrear suscripciones RxJS cientos de veces por segundo)
  private readonly ACTIVITY_THROTTLE_MS = 2000;
  private lastActivityResetAt = 0;

  public sessionWarning$ = new Subject<{ show: boolean; remainingSeconds: number }>();
  public isWarningActive = false;

  private warningTimerSub?: Subscription;
  private logoutTimerSub?: Subscription;
  private countdownSub?: Subscription;
  private sessionPollSub?: Subscription;
  private validateReqSub?: Subscription;

  private userActivityEvents = ['mousemove', 'keydown', 'mousedown', 'touchstart', 'scroll', 'click'];
  private activityListener = () => this.handleUserActivity();
  private visibilityListener = () => {
    if (typeof document !== 'undefined' && document.visibilityState === 'visible') {
      this.checkWallClockAndSession(true);
    }
  };
  private focusListener = () => this.checkWallClockAndSession(true);
  private storageListener = (event: StorageEvent) => this.handleStorageEvent(event);

  private isMonitoring = false;

  constructor(
    private router: Router,
    private ngZone: NgZone,
    private authService: AuthService
  ) {
    // Sincronizar automáticamente el monitoreo con el estado de autenticación reactivo
    effect(() => {
      const authenticated = this.authService.isAuthenticated();
      if (authenticated) {
        if (!this.isMonitoring) {
          this.startMonitoring();
        }
      } else if (this.isMonitoring) {
        this.stopMonitoring();
      }
    });
  }

  ngOnDestroy(): void {
    this.stopMonitoring();
  }

  public startMonitoring(): void {
    this.lastActivityResetAt = Date.now();
    this.isWarningActive = false;
    this.sessionWarning$.next({ show: false, remainingSeconds: 0 });

    if (!this.isMonitoring) {
      this.isMonitoring = true;
      this.attachActivityListeners();
    }

    this.resetTimers();
    this.startSessionPolling();
  }

  public stopMonitoring(): void {
    this.isMonitoring = false;
    this.detachActivityListeners();
    this.clearTimers();
    this.sessionPollSub?.unsubscribe();
    this.validateReqSub?.unsubscribe();
    this.isWarningActive = false;
    this.sessionWarning$.next({ show: false, remainingSeconds: 0 });
  }

  /**
   * Llamado cuando el usuario hace clic en "Seguir conectado" en el modal de aviso
   */
  public stayConnected(): void {
    this.isWarningActive = false;
    this.lastActivityResetAt = Date.now();
    this.sessionWarning$.next({ show: false, remainingSeconds: 0 });
    this.resetTimers();
  }

  private handleUserActivity(): void {
    if (!this.authService.isAuthenticated()) return;

    const now = Date.now();
    const elapsed = now - this.lastActivityResetAt;

    // Si la pestaña estuvo en segundo plano y ya superó los 5 minutos de inactividad, cerrar sesión de inmediato
    if (elapsed >= this.LOGOUT_MS) {
      this.ngZone.run(() => {
        this.performAutoLogout();
      });
      return;
    }

    // Si ya saltó el modal de advertencia a los 2.5 min (o se superó el umbral en segundo plano),
    // no resetear automáticamente por un leve mousemove: se requiere clic explícito en "Seguir conectado"
    if (this.isWarningActive) return;

    if (elapsed >= this.WARNING_MS) {
      this.ngZone.run(() => {
        this.triggerWarning();
      });
      return;
    }

    if (elapsed < this.ACTIVITY_THROTTLE_MS) {
      return;
    }

    this.lastActivityResetAt = now;
    this.resetTimers();
  }

  private checkWallClockAndSession(validateRemote = false): void {
    if (!this.authService.isAuthenticated()) {
      if (this.isMonitoring) {
        this.stopMonitoring();
      }
      return;
    }

    const elapsed = Date.now() - this.lastActivityResetAt;

    if (elapsed >= this.LOGOUT_MS) {
      this.ngZone.run(() => {
        this.performAutoLogout();
      });
      return;
    }

    if (!this.isWarningActive && elapsed >= this.WARNING_MS) {
      this.ngZone.run(() => {
        this.triggerWarning();
      });
    }

    if (validateRemote) {
      this.ngZone.run(() => {
        this.validateServerSession();
      });
    }
  }

  /**
   * Detecta cambios de token en otras pestañas del mismo navegador en tiempo real.
   */
  private handleStorageEvent(event: StorageEvent): void {
    if (!this.authService.isAuthenticated()) return;

    if (event.key === 'lms_auth_token' || event.key === null) {
      const newSharedToken = event.newValue;
      const currentTabToken = this.authService.getTabToken();

      if (!newSharedToken) {
        // Otra pestaña cerró la sesión manualmente o expiró
        this.ngZone.run(() => {
          this.stopMonitoring();
          this.authService.clearSessionData();
          this.router.navigate(['/login'], { queryParams: { sessionExpired: 'true' } });
        });
      } else if (currentTabToken && newSharedToken !== currentTabToken) {
        // Otra pestaña inició una nueva sesión desplazando a esta pestaña
        this.ngZone.run(() => {
          this.stopMonitoring();
          this.authService.clearSessionData(true);
          window.dispatchEvent(new CustomEvent('lms:duplicate-session', {
            detail: {
              message: 'Tu sesión fue cerrada porque se inició sesión en otro dispositivo o pestaña.'
            }
          }));
          this.router.navigate(['/login'], { queryParams: { duplicateSession: 'true' } });
        });
      }
    }
  }

  private resetTimers(): void {
    this.clearTimers();

    if (!this.authService.isAuthenticated()) {
      return;
    }

    // Ejecutar temporizadores fuera de Angular NgZone para no ralentizar con Change Detection en cada movimiento
    this.ngZone.runOutsideAngular(() => {
      // 1. A los 2.5 minutos se dispara la advertencia interactiva
      this.warningTimerSub = timer(this.WARNING_MS).subscribe(() => {
        this.ngZone.run(() => {
          this.triggerWarning();
        });
      });

      // 2. A los 5 minutos de inactividad se realiza el cierre forzoso
      this.logoutTimerSub = timer(this.LOGOUT_MS).subscribe(() => {
        this.ngZone.run(() => {
          this.performAutoLogout();
        });
      });
    });
  }

  /**
   * Polling cada 15s: verifica el reloj de inactividad real y consulta GET /api/auth/validate-session.
   * Si el servidor responde 401 con code DUPLICATE_SESSION, el jwt.interceptor
   * ya maneja el logout automático.
   */
  private startSessionPolling(): void {
    this.sessionPollSub?.unsubscribe();

    this.ngZone.runOutsideAngular(() => {
      this.sessionPollSub = interval(this.SESSION_POLL_INTERVAL_MS).subscribe(() => {
        this.ngZone.run(() => {
          if (!this.authService.isAuthenticated()) {
            this.stopMonitoring();
            return;
          }
          this.checkWallClockAndSession(true);
        });
      });
    });
  }

  private validateServerSession(): void {
    if (!this.authService.isAuthenticated()) return;
    // La respuesta 401 es manejada por el jwt.interceptor automáticamente
    this.validateReqSub?.unsubscribe();
    this.validateReqSub = this.authService.validateSession().subscribe({ error: () => {} });
  }

  private triggerWarning(): void {
    if (!this.authService.isAuthenticated()) return;

    this.isWarningActive = true;
    const computeSecondsLeft = (): number => {
      const elapsed = Date.now() - this.lastActivityResetAt;
      return Math.max(0, Math.ceil((this.LOGOUT_MS - elapsed) / 1000));
    };

    const initialSeconds = computeSecondsLeft() || Math.floor((this.LOGOUT_MS - this.WARNING_MS) / 1000); // 150s
    this.sessionWarning$.next({ show: true, remainingSeconds: initialSeconds });

    this.countdownSub?.unsubscribe();
    this.ngZone.runOutsideAngular(() => {
      this.countdownSub = timer(1000, 1000).subscribe(() => {
        this.ngZone.run(() => {
          const secondsLeft = computeSecondsLeft();
          if (secondsLeft <= 0) {
            this.countdownSub?.unsubscribe();
            this.performAutoLogout();
          } else {
            this.sessionWarning$.next({ show: true, remainingSeconds: secondsLeft });
          }
        });
      });
    });
  }

  private performAutoLogout(): void {
    this.stopMonitoring();
    this.authService.logout({ sessionExpired: 'true' });
  }

  private clearTimers(): void {
    this.warningTimerSub?.unsubscribe();
    this.logoutTimerSub?.unsubscribe();
    this.countdownSub?.unsubscribe();
  }

  private attachActivityListeners(): void {
    if (typeof window === 'undefined') return;
    this.userActivityEvents.forEach(evt => {
      window.addEventListener(evt, this.activityListener, { passive: true });
    });
    window.addEventListener('focus', this.focusListener);
    window.addEventListener('storage', this.storageListener);
    if (typeof document !== 'undefined') {
      document.addEventListener('visibilitychange', this.visibilityListener);
    }
  }

  private detachActivityListeners(): void {
    if (typeof window === 'undefined') return;
    this.userActivityEvents.forEach(evt => {
      window.removeEventListener(evt, this.activityListener);
    });
    window.removeEventListener('focus', this.focusListener);
    window.removeEventListener('storage', this.storageListener);
    if (typeof document !== 'undefined') {
      document.removeEventListener('visibilitychange', this.visibilityListener);
    }
  }
}
