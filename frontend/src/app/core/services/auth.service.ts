import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, map, of } from 'rxjs';
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
  private readonly SESSION_META_KEY = 'lms_session_meta';

  // Signals reactivos modernos de Angular (PII solo en memoria RAM, nunca en texto plano en localStorage)
  public currentUser = signal<User | null>(this.getStoredUser());
  public isAuthenticated = computed(() => !!this.currentUser());
  public userRole = computed(() => this.currentUser()?.role || null);
  public requiresPolicyAcceptance = signal<boolean>(this.currentUser()?.requiresPolicyAcceptance ?? false);

  constructor(private http: HttpClient, private router: Router) {
    // Saneamiento preventivo: eliminar cualquier PII legado en texto plano de localStorage
    localStorage.removeItem(this.USER_KEY);
    localStorage.removeItem('user');
  }

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
      guardianDocumentType: payload?.guardianDocumentType || 'CC',
      guardianAuthorization: payload?.guardianAuthorization === true,
      autorizaServicioEducativo: payload?.autorizaServicioEducativo ?? true,
      autorizaDatosOpcionales: payload?.autorizaDatosOpcionales ?? false,
      autorizaTransmisionInternacional: payload?.autorizaTransmisionInternacional ?? true
    };

    return this.http.post<any>(`${this.apiUrl}/privacy/consent`, body).pipe(
      tap(() => {
        const user = this.currentUser();
        if (user) {
          const updated: User = {
            ...user,
            dataPolicyAccepted: true,
            dataPolicyVersion: body.version,
            requiresPolicyAcceptance: false,
            hasActiveMinorConsent: true
          };
          this.currentUser.set(updated);
          this.requiresPolicyAcceptance.set(false);
          this.saveNonPiiSessionMeta(updated);
        }
      })
    );
  }

  /**
   * Cambia la contraseña del usuario autenticado validando política estricta (8 a 64 caracteres).
   */
  public changePassword(currentPassword: string, newPassword: string, confirmNewPassword?: string): Observable<any> {
    const body = { currentPassword, newPassword, confirmNewPassword };
    return this.http.post<any>(`${this.apiUrl}/auth/change-password`, body).pipe(
      tap((res) => {
        if (res?.token) {
          localStorage.setItem(this.TOKEN_KEY, res.token);
        }
      })
    );
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
    const savedTheme = localStorage.getItem('scikids_theme');
    const savedLang = localStorage.getItem('scikids_lang');

    localStorage.clear();
    sessionStorage.clear();

    if (savedTheme) {
      localStorage.setItem('scikids_theme', savedTheme);
    }
    if (savedLang) {
      localStorage.setItem('scikids_lang', savedLang);
    }

    // Limpieza de cachés de datos sensibles de PWA / Service Worker (aulas compartidas)
    if (typeof window !== 'undefined' && 'caches' in window) {
      caches.keys().then(cacheNames => {
        cacheNames.forEach(name => {
          if (name.includes('api') || name.includes('data') || name.includes('ngsw:db')) {
            caches.delete(name);
          }
        });
      }).catch(() => {});
    }

    this.currentUser.set(null);
    this.requiresPolicyAcceptance.set(false);
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
    if (!this.currentUser()) {
      this.rehydrateFromStorage();
    }
    return !!this.currentUser() && !!this.getToken();
  }

  /**
   * Rehidrata sincrónicamente el signal currentUser en RAM leyendo de sessionStorage y del JWT vigente
   * cuando el navegador se recarga (F5).
   */
  public rehydrateFromStorage(): User | null {
    const existing = this.currentUser();
    if (existing) {
      return existing;
    }

    const restored = this.getStoredUser();
    if (restored) {
      this.currentUser.set(restored);
      this.requiresPolicyAcceptance.set(restored.requiresPolicyAcceptance === true);
    }
    return restored;
  }

  /**
   * Garantiza que el estado del usuario en RAM esté rehidratado tras un F5:
   * 1) Intenta restaurar de sessionStorage + claims del JWT vigente.
   * 2) Si no hay metadatos de sesión en sessionStorage o el signal sigue vacío con un token válido,
   *    consulta GET /api/auth/me para sincronizar el perfil y el estado de ConsentimientoHabeasData.
   */
  public ensureUserHydrated(): Observable<User | null> {
    const token = this.getToken();
    if (!token) {
      return of(null);
    }

    const hasSessionMeta = !!sessionStorage.getItem(this.SESSION_META_KEY);
    const localUser = this.rehydrateFromStorage();

    if (localUser && hasSessionMeta) {
      return of(localUser);
    }

    return this.http.get<User>(`${this.apiUrl}/auth/me`, {
      headers: { 'X-Skip-Interceptor-Redirect': 'true' }
    }).pipe(
      map((profile) => {
        const hydrated: User = {
          ...profile,
          requiresPolicyAcceptance: profile.requiresPolicyAcceptance ?? false,
          hasActiveMinorConsent: profile.hasActiveMinorConsent ?? !(profile.requiresPolicyAcceptance ?? false)
        };
        this.currentUser.set(hydrated);
        this.requiresPolicyAcceptance.set(hydrated.requiresPolicyAcceptance === true);
        this.saveNonPiiSessionMeta(hydrated);
        return hydrated;
      }),
      catchError(() => {
        // Fallback al usuario reconstruido desde el JWT si el backend está en cold start
        return of(localUser);
      })
    );
  }

  public getRoleHomeRoute(role: UserRole): string {
    switch (role) {
      case 'Teacher':
        return '/dashboard/teacher';
      case 'Student':
        return '/dashboard/student';
      case 'Admin':
      case '.admin':
        return '/dashboard/admin';
      default:
        return '/login';
    }
  }

  public redirectByRole(role: UserRole): void {
    this.router.navigate([this.getRoleHomeRoute(role)]);
  }

  private handleAuthResponse(response: LoginResponse): void {
    const requiresPolicy = response.requiresPolicyAcceptance ?? response.user.requiresPolicyAcceptance ?? false;
    const user: User = {
      ...response.user,
      requiresPolicyAcceptance: requiresPolicy,
      dataPolicyVersion: response.dataPolicyVersion ?? response.user.dataPolicyVersion,
      hasActiveMinorConsent: response.user.hasActiveMinorConsent ?? !requiresPolicy
    };

    this.saveAuthData(response.token, user);
    this.currentUser.set(user);
    this.requiresPolicyAcceptance.set(requiresPolicy);
    this.redirectByRole(user.role);
  }

  private saveAuthData(token: string, user: User): void {
    localStorage.setItem(this.TOKEN_KEY, token);
    // Cumplimiento Ley 1581 / Ley 1098: NO guardar PII de menores en texto plano en localStorage
    localStorage.removeItem(this.USER_KEY);
    this.saveNonPiiSessionMeta(user);
  }

  private saveNonPiiSessionMeta(user: User): void {
    try {
      sessionStorage.setItem(this.SESSION_META_KEY, JSON.stringify({
        role: user.role,
        dataPolicyAccepted: user.dataPolicyAccepted ?? true,
        dataPolicyVersion: user.dataPolicyVersion ?? environment.dataPolicyVersion,
        requiresPolicyAcceptance: user.requiresPolicyAcceptance ?? false,
        hasActiveMinorConsent: user.hasActiveMinorConsent ?? true
      }));
    } catch {
      // Ignorar errores de cuota en modo privado
    }
  }

  private getStoredUser(): User | null {
    // Nunca leer PII en texto plano desde localStorage; reconstruir en memoria RAM desde el JWT vigente
    localStorage.removeItem(this.USER_KEY);
    const token = localStorage.getItem(this.TOKEN_KEY);
    if (!token || this.isTokenExpired(token)) {
      return null;
    }

    try {
      const parts = token.split('.');
      const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
      const payload = JSON.parse(atob(padded));

      let meta: any = {};
      const metaRaw = sessionStorage.getItem(this.SESSION_META_KEY);
      if (metaRaw) {
        meta = JSON.parse(metaRaw);
      }

      const role = (meta.role || payload.role || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || 'Student') as UserRole;
      const username = payload.nameid || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] || payload.sub || '';
      const fullName = payload.unique_name || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] || username;

      return {
        id: username,
        internalId: payload.user_id ? Number(payload.user_id) : undefined,
        fullName,
        role,
        gradeLevel: payload.grade_level || undefined,
        avatarUrl: payload.avatar_url || '',
        dataPolicyAccepted: meta.dataPolicyAccepted ?? true,
        dataPolicyVersion: meta.dataPolicyVersion ?? environment.dataPolicyVersion,
        requiresPolicyAcceptance: meta.requiresPolicyAcceptance ?? false,
        hasActiveMinorConsent: meta.hasActiveMinorConsent ?? true
      };
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
