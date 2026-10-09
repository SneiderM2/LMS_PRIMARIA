import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { ServerStatusService } from '../services/server-status.service';
import { catchError, tap, throwError } from 'rxjs';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const serverStatus = inject(ServerStatusService);

  const token = authService.getToken();
  const isApiUrl = req.url.includes('onrender.com') || 
                   req.url.includes('localhost:5000') || 
                   req.url.startsWith('/api') ||
                   req.url.includes('/api/');

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
