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

@Injectable({
  providedIn: 'root'
})
export class SystemFilesService {
  private readonly API_URL = 'http://localhost:5000/api/systemfiles';

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
   * Reinicia/trunca un archivo de log específico
   */
  public clearLog(fileName: string): Observable<{ message: string; fileName: string }> {
    const params = new HttpParams().set('fileName', fileName);
    return this.http.post<{ message: string; fileName: string }>(`${this.API_URL}/logs/clear`, null, { params });
  }
}
