import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { catchError, throwError } from 'rxjs';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const token = authService.getToken();

  let authReq = req;
  if (token) {
    authReq = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // Capturar HTTP 401 por sesión duplicada/revocada (middleware SingleSession o validate-session)
      if (error.status === 401) {
        const isAuthEndpoint =
          req.url.includes('/auth/login') ||
          req.url.includes('/auth/register') ||
          req.url.includes('/auth/google-login');

        if (!isAuthEndpoint) {
          const code = error.error?.code;
          const isDuplicateSession = code === 'DUPLICATE_SESSION';

          // Limpiar credenciales locales
          localStorage.removeItem('lms_auth_token');
          localStorage.removeItem('lms_current_user');
          sessionStorage.clear();
          authService.currentUser.set(null);

          if (isDuplicateSession) {
            // Emitir evento global para que el UI muestre una notificación elegante
            window.dispatchEvent(new CustomEvent('lms:duplicate-session', {
              detail: {
                message: error.error?.message || 'Tu sesión fue cerrada porque se inició sesión en otro dispositivo.'
              }
            }));
            router.navigate(['/login'], { queryParams: { duplicateSession: 'true' } });
          } else {
            // 401 genérico (token expirado, etc.)
            router.navigate(['/login'], { queryParams: { sessionExpired: 'true' } });
          }
        }
      }

      return throwError(() => error);
    })
  );
};
