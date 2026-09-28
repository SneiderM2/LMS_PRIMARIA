import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StudentService, StudentMetrics, StudentCoursesCatalog } from '../../core/services/student.service';
import { AuthService } from '../../core/services/auth.service';
import { Content } from '../../core/models/content.model';
import { FileDropZoneComponent } from './components/file-drop-zone/file-drop-zone.component';

@Component({
  selector: 'app-student-dashboard',
  standalone: true,
  imports: [CommonModule, FileDropZoneComponent],
  templateUrl: './student-dashboard.component.html',
  styleUrls: ['./student-dashboard.component.css']
})
export class StudentDashboardComponent implements OnInit {
  public contents: Content[] = [];
  public filteredContents: Content[] = [];
  public isLoading = true;
  public selectedSubject: string = 'Todas';

  // Pestañas del estudiante
  public activeTab: 'activities' | 'courses' = 'activities';

  // Métricas del Estudiante
  public metrics: StudentMetrics = {
    enrolledCoursesCount: 0,
    cumulativeGpa: 5.0,
    pendingActivitiesCount: 0,
    progressPercentage: 100
  };

  // Catálogo y Gestión de Clases (CRUD Estudiante)
  public coursesCatalog: StudentCoursesCatalog = {
    myActiveCourses: [],
    availableCourses: []
  };
  public coursesLoading = false;
  public notificationMessage = '';
  public notificationType: 'success' | 'info' | 'warning' = 'info';

  // Control del modal de entrega Drag & Drop
  public showDropZone = false;
  public activeAssignmentId: string | null = null;
  public activeAssignmentTitle: string = '';

  public readonly subjects = [
    { name: 'Todas', icon: '🌈', color: '#38bdf8' },
    { name: 'Matemáticas', icon: '📐', color: '#facc15' },
    { name: 'Ciencias Naturales', icon: '🔬', color: '#4ade80' },
    { name: 'Lengua y Literatura', icon: '📖', color: '#fb923c' },
    { name: 'Artes y Creatividad', icon: '🎨', color: '#a855f7' }
  ];

  constructor(
    public authService: AuthService,
    private studentService: StudentService
  ) {}

  ngOnInit(): void {
    this.loadDashboard();
    this.loadMetrics();
    this.loadCourses();
  }

  public switchTab(tab: 'activities' | 'courses'): void {
    this.activeTab = tab;
    if (tab === 'courses') {
      this.loadCourses();
    } else {
      this.loadDashboard();
      this.loadMetrics();
    }
  }

  public loadDashboard(): void {
    this.isLoading = true;
    this.studentService.getDashboard().subscribe({
      next: (items) => {
        this.contents = items;
        this.applySubjectFilter();
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  public loadMetrics(): void {
    this.studentService.getMetrics().subscribe({
      next: (m) => {
        this.metrics = m;
      }
    });
  }

  public loadCourses(): void {
    this.coursesLoading = true;
    this.studentService.getCourses().subscribe({
      next: (res) => {
        this.coursesCatalog = res;
        this.coursesLoading = false;
      },
      error: () => {
        this.coursesLoading = false;
      }
    });
  }

  public enrollInCourse(courseId: number): void {
    this.studentService.enrollCourse(courseId).subscribe({
      next: (res) => {
        this.showToast(res.message, 'success');
        this.loadCourses();
        this.loadMetrics();
      },
      error: (err) => {
        this.showToast('Error al inscribir clase: ' + (err.error?.message || err.message), 'warning');
      }
    });
  }

  public withdrawFromCourse(courseId: number, courseName: string): void {
    if (!confirm(`¿Deseas retirar ${courseName} de tus clases activas? Tu historial y notas no se perderán.`)) {
      return;
    }

    this.studentService.withdrawCourse(courseId).subscribe({
      next: (res) => {
        this.showToast(res.message, 'info');
        this.loadCourses();
        this.loadMetrics();
      },
      error: (err) => {
        this.showToast('Error al retirar la clase: ' + (err.error?.message || err.message), 'warning');
      }
    });
  }

  private showToast(msg: string, type: 'success' | 'info' | 'warning' = 'info'): void {
    this.notificationMessage = msg;
    this.notificationType = type;
    setTimeout(() => {
      this.notificationMessage = '';
    }, 4500);
  }

  public selectSubject(subjectName: string): void {
    this.selectedSubject = subjectName;
    this.applySubjectFilter();
  }

  private applySubjectFilter(): void {
    if (this.selectedSubject === 'Todas') {
      this.filteredContents = this.contents;
    } else {
      this.filteredContents = this.contents.filter(c => c.subject.toLowerCase() === this.selectedSubject.toLowerCase());
    }
  }

  public openSubmissionModal(content: Content): void {
    this.activeAssignmentId = content.id;
    this.activeAssignmentTitle = content.title;
    this.showDropZone = true;
  }

  public closeSubmissionModal(): void {
    this.showDropZone = false;
    this.activeAssignmentId = null;
  }

  public onAssignmentSubmitted(): void {
    this.closeSubmissionModal();
    this.loadDashboard();
    this.loadMetrics();
  }

  public openMedia(url?: string): void {
    if (url) {
      window.open(url, '_blank');
    }
  }

  public logout(): void {
    this.authService.logout();
  }
}
