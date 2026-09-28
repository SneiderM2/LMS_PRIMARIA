import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { SessionTimeoutService } from '../../../core/services/session-timeout.service';

@Component({
  selector: 'app-session-warning-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './session-warning-modal.component.html',
  styleUrls: ['./session-warning-modal.component.css']
})
export class SessionWarningModalComponent implements OnInit, OnDestroy {
  public visible = false;
  public remainingSeconds = 150;
  private sub?: Subscription;

  constructor(private sessionTimeoutService: SessionTimeoutService) {}

  ngOnInit(): void {
    this.sub = this.sessionTimeoutService.sessionWarning$.subscribe(status => {
      this.visible = status.show;
      this.remainingSeconds = status.remainingSeconds;
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  public get formattedTime(): string {
    const minutes = Math.floor(this.remainingSeconds / 60);
    const seconds = this.remainingSeconds % 60;
    return `${minutes}:${seconds < 10 ? '0' : ''}${seconds}`;
  }

  public onContinue(): void {
    this.sessionTimeoutService.stayConnected();
  }
}
