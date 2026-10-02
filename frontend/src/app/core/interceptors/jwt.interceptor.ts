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
      // Capturar HTTP 401 por sesión duplicada o token invalidado
      if (error.status === 401) {
        const isAuthEndpoint = req.url.includes('/auth/login') || req.url.includes('/auth/register');
        if (!isAuthEndpoint) {
          // 1. Eliminar credenciales de sesión activa
          localStorage.removeItem('lms_auth_token');
          localStorage.removeItem('lms_current_user');
          sessionStorage.clear();
          authService.currentUser.set(null);

          // 2. Alerta explicativa al usuario
          const alertMessage = error.error?.message || 'Tu sesión se ha cerrado porque se inició sesión en otro dispositivo';
          alert(alertMessage);

          // 3. Redirigir al login
          router.navigate(['/login'], { queryParams: { duplicateSession: 'true' } });
        }
      }

      return throwError(() => error);
    })
  );
};
