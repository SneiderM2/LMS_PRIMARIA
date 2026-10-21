import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { getApiBaseUrl } from './api.config';

export interface SystemFileDto {
  fileName: string;
  filePath?: string;
  size: number;
  lastModified: string;
  content: string;
}

export interface LogFileInfo {
  name: string;
  size: number;
  lastModified: string;
}

export interface LogsListResponse {
  directory: string;
  files: LogFileInfo[];
}

export interface ParsedLogEntry {
  id: string;
  timestamp: string;
  level: 'INFO' | 'AUDIT' | 'WARNING' | 'ERROR';
  user: string;
  message: string;
  details?: string;
  clientIp?: string;
  httpMethod?: string;
  path?: string;
  statusCode?: number;
  elapsedMs?: number;
}

export interface AuditLogItem {
  id: string;
  timestamp: string;
  level: 'INFO' | 'AUDIT' | 'WARNING' | 'ERROR';
  method: string;
  path: string;
  statusCode: number;
  elapsedMs: number;
  ip: string;
  userId?: string;
  userEmail?: string;
  userRole?: string;
  action: string;
  details?: string;
}

export interface AuditLogResult {
  items: AuditLogItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AuditLogFilterParams {
  page?: number;
  pageSize?: number;
  level?: string;
  keyword?: string;
  role?: string;
  from?: string;
  to?: string;
}

@Injectable({
  providedIn: 'root'
})
export class SystemFilesService {
  private readonly baseUrl = getApiBaseUrl();
  private readonly API_URL = `${this.baseUrl}/systemfiles`;
  private readonly ADMIN_URL = `${this.baseUrl}/admin`;

  constructor(private http: HttpClient) {}

  /**
   * Obtiene e inspecciona el archivo de configuración .htaccess
   */
  public getHtaccess(): Observable<SystemFileDto> {
    return this.http.get<SystemFileDto>(`${this.API_URL}/htaccess`);
  }

  /**
   * Lista todos los archivos de logs generados en el servidor
   */
  public getLogsList(): Observable<LogsListResponse> {
    return this.http.get<LogsListResponse>(`${this.API_URL}/logs`);
  }

  /**
   * Lee el contenido de un archivo de log específico con protección de ruta
   */
  public getLogContent(fileName: string): Observable<SystemFileDto> {
    const params = new HttpParams().set('fileName', fileName);
    return this.http.get<SystemFileDto>(`${this.API_URL}/logs/content`, { params });
  }

  /**
   * Lee el log procesado por el parser backend con filtros de nivel, fecha y usuario
   */
  public getParsedLogs(
    fileName: string = 'access.log',
    level?: string,
    user?: string,
    date?: string
  ): Observable<ParsedLogEntry[]> {
    let params = new HttpParams().set('fileName', fileName);
    if (level && level !== 'ALL') params = params.set('level', level);
    if (user && user.trim()) params = params.set('user', user.trim());
    if (date && date.trim()) params = params.set('date', date.trim());

    return this.http.get<ParsedLogEntry[]>(`${this.API_URL}/logs/parsed`, { params });
  }

  /**
   * Consulta registros estructurados de auditoría desde /api/admin/audit-logs
   */
  public getGlobalAuditLogs(params: AuditLogFilterParams): Observable<AuditLogResult> {
    let httpParams = new HttpParams();
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    if (params.level && params.level !== 'ALL') httpParams = httpParams.set('level', params.level);
    if (params.keyword?.trim()) httpParams = httpParams.set('keyword', params.keyword.trim());
    if (params.role?.trim() && params.role !== 'ALL') httpParams = httpParams.set('role', params.role.trim());
    if (params.from?.trim()) httpParams = httpParams.set('from', params.from.trim());
    if (params.to?.trim()) httpParams = httpParams.set('to', params.to.trim());

    return this.http.get<AuditLogResult>(`${this.ADMIN_URL}/audit-logs`, { params: httpParams });
  }

  /**
   * Descarga el archivo de logs de auditoría consolidado (/logs/app-audit.log)
   */
  public downloadAuditLogFile(): void {
    window.open(`${this.ADMIN_URL}/audit-logs/download`, '_blank');
  }

  /**
   * Reinicia/trunca un archivo de log específico
   */
  public clearLog(fileName: string): Observable<{ message: string; fileName: string }> {
    const params = new HttpParams().set('fileName', fileName);
    return this.http.post<{ message: string; fileName: string }>(`${this.API_URL}/logs/clear`, null, { params });
  }
}
