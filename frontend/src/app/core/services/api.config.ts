import { environment } from '../../../environments/environment';

/**
 * Configuración dinámica de la URL base del Backend API.
 * En producción apunta a Render (environment.prod.ts) y en desarrollo a localhost o IP local.
 */
export function getApiBaseUrl(): string {
  if (environment.production) {
    return environment.apiUrl;
  }
  if (typeof window !== 'undefined' && window.location) {
    const hostname = window.location.hostname;
    if (hostname && hostname !== 'localhost' && hostname !== '127.0.0.1') {
      return `http://${hostname}:5000/api`;
    }
  }
  return environment.apiUrl;
}

