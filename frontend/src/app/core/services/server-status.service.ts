import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ServerStatusService {
  /**
   * Estado reactivo que indica si el backend en Render se está despertando tras inactividad.
   */
  readonly isWakingUp = signal<boolean>(false);

  /**
   * Mensaje descriptivo amigable para los usuarios (profesores, alumnos, admins).
   */
  readonly statusMessage = signal<string>(
    'Iniciando servidor en la nube... Render está reactivando los servicios tras inactividad (Cold Start).'
  );

  private autoResetTimer: any = null;

  /**
   * Activa o desactiva la alerta de Cold Start del servidor.
   */
  public setWakingUp(waking: boolean, customMessage?: string): void {
    if (customMessage) {
      this.statusMessage.set(customMessage);
    } else {
      this.statusMessage.set(
        'Iniciando servidor en la nube... Render está reactivando los servicios tras inactividad (Cold Start).'
      );
    }

    this.isWakingUp.set(waking);

    if (this.autoResetTimer) {
      clearTimeout(this.autoResetTimer);
      this.autoResetTimer = null;
    }

    if (waking) {
      // Auto-limpieza tras 45s para no bloquear la interfaz permanentemente si la red se recupera
      this.autoResetTimer = setTimeout(() => {
        this.isWakingUp.set(false);
      }, 45000);
    }
  }
}
