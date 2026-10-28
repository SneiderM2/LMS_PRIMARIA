import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { ServerStatusService } from '../services/server-status.service';
import { getApiBaseUrl } from '../services/api.config';
import { catchError, tap, throwError } from 'rxjs';

/**
 * Verifica estrictamente si la URL destino pertenece al backend de confianza del LMS
 * para prevenir fugas del token JWT hacia dominios de terceros (CWE-200 / CWE-522).
 */
export function isTrustedApiUrl(url: string): boolean {
  if (!url) return false;
  if (url === '/api' || url.startsWith('/api/')) {
    return true;
  }

  try {
    const baseOrigin = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:4200';
    const parsedReq = new URL(url, baseOrigin);
    const configuredApiOrigin = new URL(getApiBaseUrl(), baseOrigin).origin;

    const trustedOrigins = new Set<string>([
      configuredApiOrigin,
      'https://lms-primaria.onrender.com',
      'http://localhost:5000',
      'http://127.0.0.1:5000'
    ]);

    return trustedOrigins.has(parsedReq.origin) &&
           (parsedReq.pathname === '/api' || parsedReq.pathname.startsWith('/api/'));
  } catch {
    return false;
  }
}

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const serverStatus = inject(ServerStatusService);

  const token = authService.getToken();
  const isApiUrl = isTrustedApiUrl(req.url);

  const skipRedirect = req.headers.has('X-Skip-Interceptor-Redirect');

  let headers = req.headers;
  if (skipRedirect) {
    headers = headers.delete('X-Skip-Interceptor-Redirect');
  }

  if (token && isApiUrl) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  const authReq = req.clone({ headers });

  // Monitoreo proactivo de Cold Start en Render:
  // Si la petición a la API tarda más de 3 segundos en responder, se notifica el inicio del contenedor
  let coldStartTimer: any = null;
  if (isApiUrl) {
    coldStartTimer = setTimeout(() => {
      serverStatus.setWakingUp(true);
    }, 3000);
  }

  return next(authReq).pipe(
    tap(() => {
      if (coldStartTimer) {
        clearTimeout(coldStartTimer);
        coldStartTimer = null;
      }
      if (serverStatus.isWakingUp()) {
        serverStatus.setWakingUp(false);
      }
    }),
    catchError((error: HttpErrorResponse) => {
      if (coldStartTimer) {
        clearTimeout(coldStartTimer);
        coldStartTimer = null;
      }

      // 1. Manejo de Cold Start y Caídas de Servidor (0 = fallo red/CORS, 502/503/504 = gateway/sleeping)
      if (error.status === 0 || error.status === 502 || error.status === 503 || error.status === 504) {
        serverStatus.setWakingUp(true);
      }

      // 2. Manejo de Sesión No Autorizada / Expirada / Duplicada (401)
      if (error.status === 401) {
        const isAuthEndpoint =
          req.url.includes('/auth/login') ||
          req.url.includes('/auth/register') ||
          req.url.includes('/auth/google-login');

        if (!isAuthEndpoint) {
          // Si el guard solicitó gestionar la redirección vía UrlTree, se delega al guard
          if (skipRedirect) {
            return throwError(() => error);
          }

          const isDuplicateSession = error.error?.code === 'DUPLICATE_SESSION';

          // Limpiar todas las credenciales de almacenamiento local y de sesión
          authService.clearSessionData();

          if (isDuplicateSession) {
            window.dispatchEvent(new CustomEvent('lms:duplicate-session', {
              detail: {
                message: error.error?.message || 'Tu sesión fue cerrada porque se inició sesión en otro dispositivo.'
              }
            }));
            router.navigate(['/login'], { queryParams: { duplicateSession: 'true' } });
          } else {
            router.navigate(['/login'], { queryParams: { sessionExpired: 'true' } });
          }

          return throwError(() => new Error('Sesión no autorizada o expirada.'));
        }
      }

      return throwError(() => error);
    })
  );
};
