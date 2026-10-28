import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot, provideRouter, Router, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { roleGuard } from './role.guard';
import { AuthService } from '../services/auth.service';
import { User } from '../models/user.model';

describe('roleGuard RBAC Security Tests', () => {
  let authService: AuthService;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        AuthService
      ]
    });

    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
  });

  function runGuard(expectedRoles: string[]) {
    const route = { data: { roles: expectedRoles } } as unknown as ActivatedRouteSnapshot;
    const state = { url: '/dashboard/admin' } as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => roleGuard(route, state));
  }

  it('permite el acceso cuando el usuario tiene el rol requerido', () => {
    authService.currentUser.set({
      id: 'admin1',
      fullName: 'Administrador',
      role: 'Admin'
    } as User);

    const result = runGuard(['Admin']);
    expect(result).toBeTrue();
  });

  it('bloquea la escalación de privilegios cuando un Estudiante intenta acceder a una ruta Admin', () => {
    authService.currentUser.set({
      id: 'student1',
      fullName: 'Estudiante',
      role: 'Student'
    } as User);

    const result = runGuard(['Admin']);
    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toBe('/dashboard/student');
  });

  it('redirige a /login cuando no hay rol activo en sesión', () => {
    authService.currentUser.set(null);

    const result = runGuard(['Teacher', 'Admin']);
    expect(result instanceof UrlTree).toBeTrue();
  });
});
