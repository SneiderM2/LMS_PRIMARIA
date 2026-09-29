import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

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
  level: 'INFO' | 'WARNING' | 'ERROR';
  user: string;
  message: string;
  details?: string;
  clientIp?: string;
  httpMethod?: string;
  path?: string;
  statusCode?: number;
  elapsedMs?: number;
}

import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SystemFilesService {
  private readonly API_URL = `${environment.apiUrl}/systemfiles`;

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
   * Reinicia/trunca un archivo de log específico
   */
  public clearLog(fileName: string): Observable<{ message: string; fileName: string }> {
    const params = new HttpParams().set('fileName', fileName);
    return this.http.post<{ message: string; fileName: string }>(`${this.API_URL}/logs/clear`, null, { params });
  }
}
