import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { ServerStatusService } from '../services/server-status.service';
import { catchError, map, of } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const serverStatus = inject(ServerStatusService);

  const token = authService.getToken();

  // 1. Si no existe token en almacenamiento, redirigir de inmediato al login
  if (!token) {
    authService.clearSessionData();
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  // 2. Validar sesión única con el backend antes de renderizar la vista protegida.
  // Al pasar true (skipInterceptorRedirect), el guard gestiona la respuesta e impide
  // peticiones paralelas a endpoints protegidos (evitando 401 en cascada a /api/admin/courses)
  return authService.validateSession(true).pipe(
    map((res) => {
      if (res && res.valid) {
        return true;
      }
      authService.clearSessionData();
      return router.createUrlTree(['/login'], { queryParams: { sessionExpired: 'true' } });
    }),
    catchError((error: HttpErrorResponse) => {
      // 2.1. Sesión rechazada por el backend (401: expirada o duplicada en otro dispositivo)
      if (error.status === 401) {
        const isDuplicate = error.error?.code === 'DUPLICATE_SESSION';
        authService.clearSessionData();

        if (isDuplicate) {
          window.dispatchEvent(new CustomEvent('lms:duplicate-session', {
            detail: {
              message: error.error?.message || 'Tu sesión fue cerrada porque se inició sesión en otro dispositivo.'
            }
          }));
          return of(router.createUrlTree(['/login'], { queryParams: { duplicateSession: 'true' } }));
        }

        return of(router.createUrlTree(['/login'], { queryParams: { sessionExpired: 'true' } }));
      }

      // 2.2. Manejo de Cold Start o fallo temporal de red (0, 502, 503, 504)
      if (error.status === 0 || error.status === 502 || error.status === 503 || error.status === 504) {
        serverStatus.setWakingUp(true);
        // Si el usuario ya cuenta con datos de sesión locales, permitir renderizado del cascarón
        if (authService.isAuthenticated()) {
          return of(true);
        }
      }

      // 2.3. Cualquier otro fallo irrecuperable
      authService.clearSessionData();
      return of(router.createUrlTree(['/login'], { queryParams: { sessionExpired: 'true' } }));
    })
  );
};
