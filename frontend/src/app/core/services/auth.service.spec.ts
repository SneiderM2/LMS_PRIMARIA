import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from './auth.service';

describe('AuthService Security & Session Tests', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let router: Router;

  function createFakeJwt(expSecondsFromNow: number): string {
    const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
    const payload = btoa(JSON.stringify({
      sub: 'user1',
      exp: Math.floor(Date.now() / 1000) + expSecondsFromNow
    }));
    return `${header}.${payload}.signature`;
  }

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        AuthService
      ]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('devuelve el token JWT cuando es vigente y no ha expirado', () => {
    const validToken = createFakeJwt(3600);
    localStorage.setItem('lms_auth_token', validToken);

    expect(service.getToken()).toBe(validToken);
  });

  it('descarta automáticamente un token JWT expirado y limpia la sesión', () => {
    const expiredToken = createFakeJwt(-120);
    localStorage.setItem('lms_auth_token', expiredToken);
    localStorage.setItem('lms_current_user', JSON.stringify({ id: 'u1', fullName: 'Test', role: 'Student' }));

    expect(service.getToken()).toBeNull();
    expect(localStorage.getItem('lms_auth_token')).toBeNull();
    expect(service.currentUser()).toBeNull();
  });

  it('rota el token JWT en localStorage al cambiar la contraseña exitosamente', () => {
    const oldToken = createFakeJwt(1800);
    const rotatedToken = createFakeJwt(7200);
    localStorage.setItem('lms_auth_token', oldToken);

    service.changePassword('OldPass!123', 'NewPass!456', 'NewPass!456').subscribe();

    const req = httpMock.expectOne((r) => r.url.endsWith('/auth/change-password'));
    expect(req.request.method).toBe('POST');
    req.flush({
      message: 'Contraseña actualizada',
      token: rotatedToken
    });

    expect(localStorage.getItem('lms_auth_token')).toBe(rotatedToken);
  });

  it('clearSessionData elimina todos los artefactos de sesión y reinicia signals', () => {
    localStorage.setItem('lms_auth_token', createFakeJwt(3600));
    localStorage.setItem('lms_current_user', JSON.stringify({ id: 'admin', fullName: 'Admin', role: 'Admin' }));

    service.clearSessionData();

    expect(localStorage.getItem('lms_auth_token')).toBeNull();
    expect(localStorage.getItem('lms_current_user')).toBeNull();
    expect(service.currentUser()).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });
});
