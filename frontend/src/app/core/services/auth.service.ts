import { Injectable, signal, computed } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError } from 'rxjs';
import { LoginRequest, LoginResponse, RegisterRequest, User, UserRole } from '../models/user.model';
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
   * Intenta iniciar sesión.
   * @param credentials Datos de login
   * @param forceLogin Si true, cierra la sesión activa en otro dispositivo
   */
  public login(credentials: LoginRequest, forceLogin = false): Observable<LoginResponse> {
    const body = { ...credentials, forceLogin };
    return this.http.post<LoginResponse>(`${this.apiUrl}/auth/login`, body).pipe(
      tap(response => {
        this.saveAuthData(response.token, response.user);
        this.currentUser.set(response.user);
        this.redirectByRole(response.user.role);
      })
    );
  }

  public register(data: RegisterRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/auth/register`, data).pipe(
      tap(response => {
        this.saveAuthData(response.token, response.user);
        this.currentUser.set(response.user);
        this.redirectByRole(response.user.role);
      })
    );
  }

  /**
   * Intenta iniciar sesión con Google.
   * @param idToken Token de identidad de Google
   * @param honeypotTrap Valor del honeypot (debe estar vacío)
   * @param forceLogin Si true, cierra la sesión activa en otro dispositivo
   */
  public loginWithGoogle(idToken: string, honeypotTrap?: string, forceLogin = false): Observable<LoginResponse> {
    const body = { idToken, honeypotTrap, forceLogin };
    return this.http.post<LoginResponse>(`${this.apiUrl}/auth/google-login`, body).pipe(
      tap(response => {
        this.saveAuthData(response.token, response.user);
        this.currentUser.set(response.user);
        this.redirectByRole(response.user.role);
      })
    );
  }

  public googleLogin(idToken: string): Observable<LoginResponse> {
    return this.loginWithGoogle(idToken);
  }

  public acceptDataPolicy(): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/auth/accept-data-policy`, { accepted: true }).pipe(
      tap(() => {
        const user = this.currentUser();
        if (user) {
          const updated = { ...user, dataPolicyAccepted: true };
          this.currentUser.set(updated);
          localStorage.setItem(this.USER_KEY, JSON.stringify(updated));
        }
      })
    );
  }

  /**
   * Polling ligero / Validación: verifica que la sesión actual siga siendo válida en el servidor.
   * @param skipInterceptorRedirect Si true, evita que el interceptor ejecute redirección automática para permitir manejo del guard
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
    // Intentar notificar al servidor (best-effort, sin bloquear)
    const token = this.getToken();
    if (token) {
      this.http.post(`${this.apiUrl}/auth/logout`, {}).subscribe({ error: () => {} });
    }
    this.clearSessionData();
    this.router.navigate(['/login']);
  }

  public getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY) || 
           localStorage.getItem('token') || 
           localStorage.getItem('access_token');
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
        this.router.navigate(['/dashboard/admin']);
        break;
      default:
        this.router.navigate(['/login']);
    }
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
}
