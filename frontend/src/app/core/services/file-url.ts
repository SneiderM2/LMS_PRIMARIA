import { environment } from '../../../environments/environment';

/**
 * Convierte las rutas de archivos que devuelve la API (`/api/files/...`) en URLs
 * absolutas del backend, porque el frontend se sirve desde otro origen (GitHub Pages).
 */
export function resolveFileUrl(url?: string | null): string {
  if (!url) {
    return '';
  }

  if (/^https?:\/\//i.test(url)) {
    return url;
  }

  const apiOrigin = environment.apiUrl.replace(/\/api\/?$/, '');

  if (url.startsWith('/api/')) {
    return `${apiOrigin}${url}`;
  }

  // Archivos heredados guardados como /uploads/...
  if (url.startsWith('/uploads/')) {
    return `${apiOrigin}/api/files${url}`;
  }

  return `${apiOrigin}/${url.replace(/^\//, '')}`;
}
