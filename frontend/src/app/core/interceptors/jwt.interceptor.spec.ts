import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { jwtInterceptor, isTrustedApiUrl } from './jwt.interceptor';
import { AuthService } from '../services/auth.service';
import { ServerStatusService } from '../services/server-status.service';

describe('jwtInterceptor Security & Token Leak Prevention Tests', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;
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
        provideHttpClient(withInterceptors([jwtInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        AuthService,
        ServerStatusService
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('valida estrictamente orígenes de confianza con isTrustedApiUrl (mitigación CWE-200)', () => {
    expect(isTrustedApiUrl('/api/student/courses')).toBeTrue();
    expect(isTrustedApiUrl('http://localhost:5000/api/teacher/courses')).toBeTrue();
    expect(isTrustedApiUrl('https://lms-primaria.onrender.com/api/admin/users')).toBeTrue();

    // Dominios externos maliciosos o de terceros que contienen subcadenas 'onrender.com' o '/api/'
    expect(isTrustedApiUrl('https://evil-onrender.com/api/capture')).toBeFalse();
    expect(isTrustedApiUrl('https://attacker.onrender.com/api/steal')).toBeFalse();
    expect(isTrustedApiUrl('https://api.dicebear.com/api/bottts/student.svg')).toBeFalse();
    expect(isTrustedApiUrl('https://external-service.com/api/v1/Resource')).toBeFalse();
  });

  it('adjunta el encabezado Authorization Bearer en peticiones a la API legítima', () => {
    const validToken = createFakeJwt(3600);
    localStorage.setItem('lms_auth_token', validToken);

    http.get('/api/student/dashboard').subscribe();

    const req = httpMock.expectOne('/api/student/dashboard');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${validToken}`);
    req.flush({});
  });

  it('NO filtra el token JWT hacia dominios externos de terceros', () => {
    const validToken = createFakeJwt(3600);
    localStorage.setItem('lms_auth_token', validToken);

    const externalUrl = 'https://api.dicebear.com/api/bottts/seed.svg';
    http.get(externalUrl).subscribe();

    const req = httpMock.expectOne(externalUrl);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('limpia la sesión y redirige a /login cuando un endpoint protegido responde 401', () => {
    spyOn(router, 'navigate');
    const validToken = createFakeJwt(3600);
    localStorage.setItem('lms_auth_token', validToken);

    http.get('/api/teacher/courses').subscribe({
      error: () => { /* esperado */ }
    });

    const req = httpMock.expectOne('/api/teacher/courses');
    req.flush({ message: 'No autorizado' }, { status: 401, statusText: 'Unauthorized' });

    expect(localStorage.getItem('lms_auth_token')).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], { queryParams: { sessionExpired: 'true' } });
  });
});
