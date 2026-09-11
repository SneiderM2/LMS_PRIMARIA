import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { UserRole } from '../models/user.model';

export const roleGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const expectedRoles = route.data['roles'] as UserRole[];
  const currentRole = authService.userRole();

  if (currentRole && expectedRoles.includes(currentRole)) {
    return true;
  }

  // Si no tiene el rol, redirigir a su propio dashboard
  if (currentRole) {
    authService.redirectByRole(currentRole);
  } else {
    router.navigate(['/login']);
  }
  return false;
};
