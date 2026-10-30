import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PrivacyRight {
  code: string;
  title: string;
  description: string;
}

export interface PrivacyPolicyInfo {
  version: string;
  effectiveDate: string;
  policyUrl: string;
  controller: {
    name: string;
    nit: string;
    address: string;
    email: string;
    phone: string;
    dpo: string;
  };
  purposes: string[];
  rights: PrivacyRight[];
  retention: {
    academicYears: number;
    logDays: number;
    inactivityAnonymizationMonths: number;
  };
  internationalTransfers: { provider: string; country: string; purpose: string; safeguard: string }[];
  children: string;
  authority: string;
}

/**
 * Ejercicio de derechos del titular (Ley 1581 de 2012): acceso, portabilidad,
 * rectificación y supresión. Las solicitudes quedan auditadas en el backend.
 */
@Injectable({
  providedIn: 'root'
})
export class PrivacyService {
  private readonly apiUrl = `${environment.apiUrl}/privacy`;

  constructor(private http: HttpClient) { }

  public getPolicy(): Observable<PrivacyPolicyInfo> {
    return this.http.get<PrivacyPolicyInfo>(`${this.apiUrl}/policy`);
  }

  public exportMyData(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/my-data`, { responseType: 'blob' });
  }

  public rectify(nombre: string, apellido: string): Observable<{ message: string; fullName: string }> {
    return this.http.put<{ message: string; fullName: string }>(`${this.apiUrl}/my-data`, { nombre, apellido });
  }

  public deleteAccount(password: string, confirmation: string, reason?: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/delete-account`, {
      password,
      confirmation,
      reason
    });
  }

  public getConsentStatus(): Observable<{
    policyAccepted: boolean;
    policyVersion: string;
    currentPolicyVersion: string;
    isStudent: boolean;
    hasActiveMinorConsent: boolean;
  }> {
    return this.http.get<any>(`${this.apiUrl}/consent-status`);
  }

  public revokeConsent(motivo: string): Observable<{
    message: string;
    revokedAtUtc: string;
    solicitudArcoId: number;
    fechaLimiteLegalUtc: string;
  }> {
    return this.http.post<any>(`${this.apiUrl}/consent/revoke`, { motivo });
  }

  public createSolicitudArco(payload: {
    tipoSolicitud: 'CONOCER' | 'ACTUALIZAR' | 'RECTIFICAR' | 'SUPRIMIR' | 'REVOCAR';
    descripcion: string;
    solicitanteNombre?: string;
    solicitanteDocumento?: string;
  }): Observable<{
    id: number;
    tipoSolicitud: string;
    estado: string;
    terminoDiasHabiles: number;
    fechaSolicitudUtc: string;
    fechaLimiteLegalUtc: string;
    message: string;
  }> {
    return this.http.post<any>(`${this.apiUrl}/arco`, payload);
  }

  public getMySolicitudesArco(): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/arco`);
  }
}
