import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError } from 'rxjs';
import { ConsentPayload, LoginRequest, LoginResponse, RegisterRequest, User, UserRole } from '../models/user.model';
import { environment } from '../../../environments/environment';

export interface ActiveSessionInfo {
  code: 'ACTIVE_SESSION';
  message: string;
  deviceHint?: string;
  sessionStartedAt?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly apiUrl = environment.apiUrl;
  private readonly TOKEN_KEY = 'lms_auth_token';
  private readonly USER_KEY = 'lms_current_user';

  // Signals reactivos modernos de Angular
  public currentUser = signal<User | null>(this.getStoredUser());
  public isAuthenticated = computed(() => !!this.currentUser());
  public userRole = computed(() => this.currentUser()?.role || null);

  constructor(private http: HttpClient, private router: Router) { }

  /**
   * Precalentamiento del backend (Wake-up ping).
   * Despierta el servidor en Render en segundo plano mientras el usuario escribe sus credenciales.
   * No se consultan endpoints de diagnóstico (fueron retirados por seguridad).
   */
  public warmup(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/ping`).pipe(
      catchError(() => this.http.get<any>(`${this.apiUrl}/health/live`))
    );
  }

  public login(credentials: LoginRequest, forceLogin = false): Observable<LoginResponse> {
    const body = { ...credentials, forceLogin };
    return this.http.post<LoginResponse>(`${this.apiUrl}/auth/login`, body).pipe(
      tap(response => this.handleAuthResponse(response))
    );
  }

  public register(data: RegisterRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/auth/register`, data).pipe(
      tap(response => this.handleAuthResponse(response))
    );
  }

  public loginWithGoogle(idToken: string, honeypotTrap?: string, forceLogin = false): Observable<LoginResponse> {
    const body = { idToken, honeypotTrap, forceLogin };
    return this.http.post<LoginResponse>(`${this.apiUrl}/auth/google-login`, body).pipe(
      tap(response => this.handleAuthResponse(response))
    );
  }

  public googleLogin(idToken: string): Observable<LoginResponse> {
    return this.loginWithGoogle(idToken);
  }

  /**
   * Registra la aceptación de la política con evidencia (versión, acudiente si aplica).
   */
  public acceptDataPolicy(payload?: Partial<ConsentPayload>): Observable<any> {
    const body = {
      accepted: true,
      version: payload?.version || environment.dataPolicyVersion,
      guardianFullName: payload?.guardianFullName,
      guardianDocument: payload?.guardianDocument,
      guardianAuthorization: payload?.guardianAuthorization === true
    };

    return this.http.post<any>(`${this.apiUrl}/privacy/consent`, body).pipe(
      tap(() => {
        const user = this.currentUser();
        if (user) {
          const updated: User = {
            ...user,
            dataPolicyAccepted: true,
            dataPolicyVersion: body.version,
            requiresPolicyAcceptance: false
          };
          this.currentUser.set(updated);
          localStorage.setItem(this.USER_KEY, JSON.stringify(updated));
        }
      })
    );
  }

  /**
   * Cambia la contraseña del usuario autenticado validando política estricta (4 a 10 caracteres).
   */
  public changePassword(currentPassword: string, newPassword: string, confirmNewPassword?: string): Observable<any> {
    const body = { currentPassword, newPassword, confirmNewPassword };
    return this.http.post<any>(`${this.apiUrl}/auth/change-password`, body);
  }

  /**
   * Polling ligero / Validación: verifica que la sesión actual siga siendo válida en el servidor.
   */
  public validateSession(skipInterceptorRedirect = false): Observable<{ valid: boolean }> {
    const headers: { [key: string]: string } = {};
    if (skipInterceptorRedirect) {
      headers['X-Skip-Interceptor-Redirect'] = 'true';
    }
    return this.http.get<{ valid: boolean }>(`${this.apiUrl}/auth/validate-session`, { headers });
  }

  public clearSessionData(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    localStorage.removeItem('token');
    localStorage.removeItem('access_token');
    localStorage.removeItem('user');
    localStorage.removeItem('lms_token');
    sessionStorage.clear();
    this.currentUser.set(null);
  }

  public logout(): void {
    const token = this.getToken();
    if (token) {
      this.http.post(`${this.apiUrl}/auth/logout`, {}).subscribe({ error: () => {} });
    }
    this.clearSessionData();
    this.router.navigate(['/login']);
  }

  /**
   * Devuelve el token solo si existe y no ha expirado. Un token vencido se descarta.
   */
  public getToken(): string | null {
    const token = localStorage.getItem(this.TOKEN_KEY)
      || localStorage.getItem('token')
      || localStorage.getItem('access_token');

    if (!token) {
      return null;
    }

    if (this.isTokenExpired(token)) {
      this.clearSessionData();
      return null;
    }

    return token;
  }

  public hasValidSession(): boolean {
    return !!this.currentUser() && !!this.getToken();
  }

  public redirectByRole(role: UserRole): void {
    switch (role) {
      case 'Teacher':
        this.router.navigate(['/dashboard/teacher']);
        break;
      case 'Student':
        this.router.navigate(['/dashboard/student']);
        break;
      case 'Admin':
      case '.admin':
        this.router.navigate(['/dashboard/admin']);
        break;
      default:
        this.router.navigate(['/login']);
    }
  }

  private handleAuthResponse(response: LoginResponse): void {
    const user: User = {
      ...response.user,
      requiresPolicyAcceptance: response.requiresPolicyAcceptance ?? response.user.requiresPolicyAcceptance ?? false,
      dataPolicyVersion: response.dataPolicyVersion ?? response.user.dataPolicyVersion
    };

    this.saveAuthData(response.token, user);
    this.currentUser.set(user);
    this.redirectByRole(user.role);
  }

  private saveAuthData(token: string, user: User): void {
    localStorage.setItem(this.TOKEN_KEY, token);
    localStorage.setItem(this.USER_KEY, JSON.stringify(user));
  }

  private getStoredUser(): User | null {
    const userJson = localStorage.getItem(this.USER_KEY);
    if (!userJson) return null;
    try {
      return JSON.parse(userJson) as User;
    } catch {
      return null;
    }
  }

  /**
   * Decodifica el payload JWT sin validar la firma (la firma la valida el backend).
   * Se usa únicamente para descartar tokens expirados en el cliente.
   */
  private isTokenExpired(token: string): boolean {
    try {
      const parts = token.split('.');
      if (parts.length < 2) {
        return true;
      }

      const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
      const payload = JSON.parse(atob(padded));

      return typeof payload.exp === 'number' && payload.exp * 1000 <= Date.now();
    } catch {
      return true;
    }
  }
}
