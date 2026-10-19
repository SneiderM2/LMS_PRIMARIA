import { Component, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ThemeToggleComponent } from './shared/components/theme-toggle/theme-toggle.component';
import { LanguageSelectorComponent } from './shared/components/language-selector/language-selector.component';
import { DataPolicyModalComponent } from './shared/components/data-policy-modal/data-policy-modal.component';
import { SessionWarningModalComponent } from './shared/components/session-warning-modal/session-warning-modal.component';
import { AuthService } from './core/services/auth.service';
import { SessionTimeoutService } from './core/services/session-timeout.service';
import { ServerStatusService } from './core/services/server-status.service';
import { ConsentPayload } from './core/models/user.model';
import { TranslatePipe } from './shared/pipes/translate.pipe';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    ThemeToggleComponent,
    LanguageSelectorComponent,
    DataPolicyModalComponent,
    SessionWarningModalComponent,
    TranslatePipe
  ],
  templateUrl: './app.component.html'
})
export class AppComponent implements OnInit {
  title = 'lms-primaria-frontend';

  public policySubmitting = false;

  constructor(
    public authService: AuthService,
    public serverStatus: ServerStatusService,
    private sessionTimeoutService: SessionTimeoutService
  ) {}

  ngOnInit(): void {
    if (this.authService.isAuthenticated()) {
      this.sessionTimeoutService.startMonitoring();
    }
  }

  public get showPolicyModal(): boolean {
    const user = this.authService.currentUser();
    if (!user) {
      return false;
    }

    return user.dataPolicyAccepted === false || user.requiresPolicyAcceptance === true;
  }

  public get requiresGuardian(): boolean {
    const role = this.authService.currentUser()?.role;
    return role === 'Student';
  }

  public get policyVersion(): string {
    return environment.dataPolicyVersion;
  }

  public get policyUrl(): string {
    return environment.dataPolicyUrl;
  }

  public onPolicyAccepted(payload: ConsentPayload): void {
    this.policySubmitting = true;

    this.authService.acceptDataPolicy(payload).subscribe({
      next: () => {
        this.policySubmitting = false;
        this.sessionTimeoutService.startMonitoring();
      },
      error: (err) => {
        this.policySubmitting = false;
        console.error('Error al registrar consentimiento:', err);
      }
    });
  }
}
