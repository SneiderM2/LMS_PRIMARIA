import { Component, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ThemeToggleComponent } from './shared/components/theme-toggle/theme-toggle.component';
import { DataPolicyModalComponent } from './shared/components/data-policy-modal/data-policy-modal.component';
import { SessionWarningModalComponent } from './shared/components/session-warning-modal/session-warning-modal.component';
import { AuthService } from './core/services/auth.service';
import { SessionTimeoutService } from './core/services/session-timeout.service';
import { ServerStatusService } from './core/services/server-status.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet, 
    ThemeToggleComponent, 
    DataPolicyModalComponent, 
    SessionWarningModalComponent
  ],
  templateUrl: './app.component.html'
})
export class AppComponent implements OnInit {
  title = 'lms-primaria-frontend';

  constructor(
    public authService: AuthService,
    public serverStatus: ServerStatusService,
    private sessionTimeoutService: SessionTimeoutService
  ) {}

  ngOnInit(): void {
    // Si el usuario ya está autenticado al cargar o refrescar la aplicación, iniciar monitoreo de inactividad
    if (this.authService.isAuthenticated()) {
      this.sessionTimeoutService.startMonitoring();
    }
  }

  public get showPolicyModal(): boolean {
    const user = this.authService.currentUser();
    return !!user && user.dataPolicyAccepted === false;
  }

  public onPolicyAccepted(): void {
    this.authService.acceptDataPolicy().subscribe({
      next: () => {
        this.sessionTimeoutService.startMonitoring();
      },
      error: (err) => console.error('Error al registrar consentimiento:', err)
    });
  }
}
