import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { UserRole } from '../models/user.model';

export const roleGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.currentUser()) {
    authService.rehydrateFromStorage();
  }

  const expectedRoles = route.data['roles'] as UserRole[];
  const currentRole = authService.userRole();

  if (currentRole && expectedRoles.includes(currentRole)) {
    return true;
  }

  // Si tiene un rol válido pero diferente al requerido, redirigir directamente vía UrlTree
  if (currentRole) {
    return router.createUrlTree([authService.getRoleHomeRoute(currentRole)]);
  }

  // Si no cuenta con rol definido o sesión corrupta, limpiar y retornar UrlTree al login
  authService.clearSessionData();
  return router.createUrlTree(['/login']);
};
