import { Injectable, NgZone, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, Subscription, timer, interval } from 'rxjs';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class SessionTimeoutService {
  // Requerimientos del sprint: 2.5 minutos para aviso, 5 minutos para cierre total
  private readonly WARNING_MS = 2.5 * 60 * 1000; // 150 segundos
  private readonly LOGOUT_MS = 5 * 60 * 1000;    // 300 segundos

  // Polling de sesión única: cada 45 segundos verifica que no haya doble sesión
  private readonly SESSION_POLL_INTERVAL_MS = 45 * 1000;

  public sessionWarning$ = new Subject<{ show: boolean; remainingSeconds: number }>();
  public isWarningActive = false;

  private warningTimerSub?: Subscription;
  private logoutTimerSub?: Subscription;
  private countdownSub?: Subscription;
  private sessionPollSub?: Subscription;

  private userActivityEvents = ['mousemove', 'keydown', 'mousedown', 'touchstart', 'scroll', 'click'];
  private activityListener = () => this.handleUserActivity();

  private isMonitoring = false;

  constructor(
    private router: Router, 
    private ngZone: NgZone,
    private authService: AuthService
  ) {}

  public startMonitoring(): void {
    if (this.isMonitoring) return;
    this.isMonitoring = true;
    this.attachActivityListeners();
    this.resetTimers();
    this.startSessionPolling();
  }

  public stopMonitoring(): void {
    this.isMonitoring = false;
    this.detachActivityListeners();
    this.clearTimers();
    this.sessionPollSub?.unsubscribe();
    this.isWarningActive = false;
    this.sessionWarning$.next({ show: false, remainingSeconds: 0 });
  }

  /**
   * Llamado cuando el usuario hace clic en "Seguir conectado" en el modal de aviso
   */
  public stayConnected(): void {
    this.isWarningActive = false;
    this.sessionWarning$.next({ show: false, remainingSeconds: 0 });
    this.resetTimers();
  }

  private handleUserActivity(): void {
    // Si ya saltó el modal de advertencia a los 2.5 min, no resetear automáticamente por un leve mousemove,
    // se requiere que el usuario haga clic explícito en "Seguir conectado"
    if (this.isWarningActive) return;

    this.resetTimers();
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
   * Polling cada 45s: consulta GET /api/auth/validate-session.
   * Si el servidor responde 401 con code DUPLICATE_SESSION, el jwt.interceptor
   * ya maneja el logout automático. Este método solo inicia el ciclo.
   */
  private startSessionPolling(): void {
    this.sessionPollSub?.unsubscribe();

    this.ngZone.runOutsideAngular(() => {
      this.sessionPollSub = interval(this.SESSION_POLL_INTERVAL_MS).subscribe(() => {
        this.ngZone.run(() => {
          if (!this.authService.isAuthenticated()) {
            this.sessionPollSub?.unsubscribe();
            return;
          }
          // La respuesta 401 es manejada por el jwt.interceptor automáticamente
          this.authService.validateSession().subscribe({ error: () => {} });
        });
      });
    });
  }

  private triggerWarning(): void {
    if (!this.authService.isAuthenticated()) return;

    this.isWarningActive = true;
    let secondsLeft = Math.floor((this.LOGOUT_MS - this.WARNING_MS) / 1000); // 150s

    this.sessionWarning$.next({ show: true, remainingSeconds: secondsLeft });

    this.countdownSub?.unsubscribe();
    this.countdownSub = timer(1000, 1000).subscribe(() => {
      secondsLeft--;
      if (secondsLeft <= 0) {
        this.countdownSub?.unsubscribe();
      } else {
        this.sessionWarning$.next({ show: true, remainingSeconds: secondsLeft });
      }
    });
  }

  private performAutoLogout(): void {
    this.stopMonitoring();
    this.authService.logout();
    this.router.navigate(['/login'], { queryParams: { sessionExpired: 'true' } });
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
  }

  private detachActivityListeners(): void {
    if (typeof window === 'undefined') return;
    this.userActivityEvents.forEach(evt => {
      window.removeEventListener(evt, this.activityListener);
    });
  }
}
