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
}
