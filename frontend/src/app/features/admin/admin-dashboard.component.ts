import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { TeacherService } from '../../core/services/teacher.service';
import { SemaforoSummaryMetrics } from '../../core/models/semaforo.model';
import { SystemInspectorComponent } from './system-inspector/system-inspector.component';


@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, SystemInspectorComponent],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.css']
})
export class AdminDashboardComponent implements OnInit {
  public metrics: SemaforoSummaryMetrics = {
    totalStudents: 0,
    greenCount: 0,
    yellowCount: 0,
    redCount: 0
  };
  public isLoading = true;

  constructor(
    public authService: AuthService,
    private teacherService: TeacherService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.teacherService.getMetrics().subscribe({
      next: (m) => {
        this.metrics = m;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  public goToTeacherView(): void {
    this.router.navigate(['/dashboard/teacher']);
  }

  public logout(): void {
    this.authService.logout();
  }
}
