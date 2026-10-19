import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { resolveFileUrl } from './file-url';

/**
 * Descarga de adjuntos a través de HttpClient para que el interceptor JWT
 * adjunte el token. Un enlace directo a /api/files no envía Authorization.
 */
@Injectable({
  providedIn: 'root'
})
export class FileDownloadService {
  constructor(private http: HttpClient) { }

  public fetchBlob(fileUrl: string): Observable<Blob> {
    return this.http.get(resolveFileUrl(fileUrl), { responseType: 'blob' });
  }

  public openInNewTab(fileUrl: string): void {
    // Recursos externos (por ejemplo, videos de YouTube) se abren directamente.
    if (/^https?:\/\//i.test(fileUrl)) {
      window.open(fileUrl, '_blank', 'noopener,noreferrer');
      return;
    }

    this.fetchBlob(fileUrl).subscribe({
      next: (blob) => {
        const objectUrl = URL.createObjectURL(blob);
        window.open(objectUrl, '_blank', 'noopener,noreferrer');
        setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
      },
      error: (err) => console.error('No fue posible abrir el archivo:', err)
    });
  }

  public save(fileUrl: string, fileName: string): void {
    this.fetchBlob(fileUrl).pipe(
      tap((blob) => {
        const objectUrl = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = objectUrl;
        link.download = fileName || 'archivo';
        link.rel = 'noopener';
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
      })
    ).subscribe({
      error: (err) => console.error('No fue posible descargar el archivo:', err)
    });
  }
}
