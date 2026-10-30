import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Guard de Protección de Menores (Ley 1581 de 2012 Art. 7, Decreto 1377 de 2013 y Ley 1098 de 2006).
 * Impide que un perfil Student (menor de edad) opere en la plataforma sin un registro activo
 * y vigente en ConsentimientoHabeasData firmado por su representante legal.
 * Soporta recarga del navegador (F5) rehidratando el estado desde sessionStorage o /api/auth/me.
 */
export const minorConsentGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.ensureUserHydrated().pipe(
    map((user) => {
      if (!user) {
        return router.createUrlTree(['/login']);
      }

      if (user.role === 'Student') {
        const hasActiveConsent =
          user.dataPolicyAccepted === true &&
          user.requiresPolicyAcceptance !== true &&
          user.hasActiveMinorConsent !== false;

        if (!hasActiveConsent) {
          authService.requiresPolicyAcceptance.set(true);
        }
      }

      return true;
    })
  );
};
