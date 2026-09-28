/**
 * Configuración dinámica de la URL base del Backend API.
 * Detecta automáticamente si la aplicación se carga desde localhost o desde un dispositivo
 * en la misma red local (ej. celular o tablet conectada vía Wi-Fi: 192.168.x.x o 10.x.x.x).
 */
export function getApiBaseUrl(): string {
  if (typeof window !== 'undefined' && window.location) {
    const hostname = window.location.hostname;
    // Si se accede desde la IP local de la máquina (ej: 192.168.1.15), apuntar al backend en esa misma IP
    if (hostname && hostname !== 'localhost' && hostname !== '127.0.0.1') {
      return `http://${hostname}:5000/api`;
    }
  }
  return 'http://localhost:5000/api';
}
