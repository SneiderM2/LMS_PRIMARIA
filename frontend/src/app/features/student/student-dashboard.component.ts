import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { StudentService, StudentMetrics, StudentCoursesCatalog, StudentNotification } from '../../core/services/student.service';
import { AuthService } from '../../core/services/auth.service';
import { FileDownloadService } from '../../core/services/file-download.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { Content } from '../../core/models/content.model';
import { FileDropZoneComponent } from './components/file-drop-zone/file-drop-zone.component';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { LanguageSelectorComponent } from '../../shared/components/language-selector/language-selector.component';
import { NotificationBellComponent } from '../../shared/components/notification-bell/notification-bell.component';
import { MonthCalendarComponent, CalendarEvent } from '../../shared/components/calendar/month-calendar.component';
import { SkeletonComponent } from '../../shared/components/skeleton/skeleton.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ChangePasswordModalComponent } from '../../shared/components/change-password-modal/change-password-modal.component';

@Component({
  selector: 'app-student-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    FileDropZoneComponent,
    TranslatePipe,
    LanguageSelectorComponent,
    NotificationBellComponent,
    MonthCalendarComponent,
    SkeletonComponent,
    EmptyStateComponent,
    ChangePasswordModalComponent
  ],
  templateUrl: './student-dashboard.component.html',
  styleUrls: ['./student-dashboard.component.css']
})
export class StudentDashboardComponent implements OnInit, OnDestroy {
  public contents: Content[] = [];
  public filteredContents: Content[] = [];
  public isLoading = true;
  public selectedSubject: string = 'Todas';
  public missionStatusFilter: 'ALL' | 'PENDIENTES' | 'ENTREGADAS' | 'CALIFICADAS' = 'ALL';
  public searchQuery: string = '';

  // Modo de visualización de misiones: 'cards' o 'calendar'
  public viewMode: 'cards' | 'calendar' = 'cards';
  public calendarEvents: CalendarEvent[] = [];

  // Notificaciones escolares activas
  public notifications: StudentNotification[] = [];

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
  public showChangePasswordModal = false;

  // Control del modal de entrega Drag & Drop y detalle de tarea
  public showDropZone = false;
  public activeAssignmentId: string | null = null;
  public activeAssignmentTitle: string = '';
  public activeAssignment: Content | null = null;
  public selectedTaskDetail: Content | null = null;

  public subjects = [
    { name: 'Todas', icon: '🌈', color: '#38bdf8' },
    { name: 'Matemáticas', icon: '📐', color: '#facc15' },
    { name: 'Ciencias Naturales', icon: '🔬', color: '#4ade80' },
    { name: 'Lengua y Literatura', icon: '📖', color: '#fb923c' },
    { name: 'Artes y Creatividad', icon: '🎨', color: '#a855f7' }
  ];

  private readonly destroy$ = new Subject<void>();
  private toastTimeoutId: ReturnType<typeof setTimeout> | null = null;

  constructor(
    public authService: AuthService,
    private studentService: StudentService,
    private fileDownload: FileDownloadService,
    private toastService: ToastService,
    private confirmDialogService: ConfirmDialogService
  ) {}

  ngOnInit(): void {
    this.loadDashboard();
    this.loadMetrics();
    this.loadCourses();
    this.loadNotifications();
  }

  ngOnDestroy(): void {
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
      this.toastTimeoutId = null;
    }
    this.destroy$.next();
    this.destroy$.complete();
  }

  public switchTab(tab: 'activities' | 'courses'): void {
    this.activeTab = tab;
    if (tab === 'courses') {
      this.loadCourses();
    } else {
      this.loadDashboard();
      this.loadMetrics();
      this.loadNotifications();
    }
  }

  public loadDashboard(): void {
    this.isLoading = true;
    this.studentService.getDashboard()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (items) => {
          this.contents = items;
          this.syncDynamicSubjects(items);
          this.applySubjectFilter();
          this.calendarEvents = items
            .filter(i => i.dueDate)
            .map(i => ({
              id: i.id,
              date: i.dueDate!,
              title: i.title,
              color: i.hasSubmitted ? '#22c55e' : (this.isOverdue(i.dueDate) ? '#ef4444' : '#38bdf8'),
              subtitle: i.subject
            }));
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
        }
      });
  }

  public loadNotifications(): void {
    this.studentService.getNotifications()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (notifs) => {
          this.notifications = notifs;
        },
        error: () => {}
      });
  }

  public onNotificationClick(notif: StudentNotification): void {
    if (notif.taskId) {
      const task = this.contents.find(c => c.id === notif.taskId?.toString());
      if (task) {
        this.openTaskDetailModal(task);
      }
    }
  }

  public onCalendarEventClick(event: CalendarEvent): void {
    const task = this.contents.find(c => c.id === event.id.toString());
    if (task) {
      this.openTaskDetailModal(task);
    }
  }

  private syncDynamicSubjects(items: Content[]): void {
    const baseSubjects = [
      { name: 'Todas', icon: '🌈', color: '#38bdf8' },
      { name: 'Matemáticas', icon: '📐', color: '#facc15' },
      { name: 'Ciencias Naturales', icon: '🔬', color: '#4ade80' },
      { name: 'Lengua y Literatura', icon: '📖', color: '#fb923c' },
      { name: 'Artes y Creatividad', icon: '🎨', color: '#a855f7' }
    ];
    const knownNames = new Set(baseSubjects.map(s => s.name.toLowerCase()));
    const extraColors = ['#ec4899', '#14b8a6', '#6366f1', '#f97316'];
    let colorIdx = 0;

    for (const item of items) {
      if (item.subject && !knownNames.has(item.subject.toLowerCase())) {
        knownNames.add(item.subject.toLowerCase());
        baseSubjects.push({
          name: item.subject,
          icon: '📘',
          color: extraColors[colorIdx++ % extraColors.length]
        });
      }
    }
    this.subjects = baseSubjects;
  }

  public onKpiClick(metric: 'courses' | 'gpa' | 'pending' | 'progress'): void {
    if (metric === 'courses') {
      this.switchTab('courses');
      return;
    }
    this.activeTab = 'activities';
    this.selectedSubject = 'Todas';
    if (metric === 'pending') {
      this.missionStatusFilter = 'PENDIENTES';
    } else if (metric === 'gpa') {
      this.missionStatusFilter = 'CALIFICADAS';
    } else {
      this.missionStatusFilter = 'ALL';
    }
    this.applySubjectFilter();
  }

  public setMissionStatusFilter(status: 'ALL' | 'PENDIENTES' | 'ENTREGADAS' | 'CALIFICADAS'): void {
    this.missionStatusFilter = status;
    this.applySubjectFilter();
  }

  public onSearchChange(): void {
    this.applySubjectFilter();
  }

  public get totalMissionsCount(): number {
    return this.contents.length;
  }

  public get pendingMissionsCount(): number {
    return this.contents.filter(c => c.type === 'Assignment' && !c.hasSubmitted).length;
  }

  public get submittedMissionsCount(): number {
    return this.contents.filter(c => c.type === 'Assignment' && c.hasSubmitted).length;
  }

  public get gradedMissionsCount(): number {
    return this.contents.filter(c => c.type === 'Assignment' && c.mySubmission?.grade !== null && c.mySubmission?.grade !== undefined).length;
  }

  public loadMetrics(): void {
    this.studentService.getMetrics()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (m) => {
          this.metrics = m;
        }
      });
  }

  public loadCourses(): void {
    this.coursesLoading = true;
    this.studentService.getCourses()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
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
    this.studentService.enrollCourse(courseId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
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

  public async withdrawFromCourse(courseId: number, courseName: string): Promise<void> {
    const confirmed = await this.confirmDialogService.confirm({
      title: 'Retirar Materia',
      message: `¿Deseas retirar ${courseName} de tus clases activas? Tu historial y notas no se perderán.`,
      tone: 'warning',
      confirmText: 'Retirar Materia'
    });
    if (!confirmed) return;

    this.studentService.withdrawCourse(courseId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
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
    if (type === 'success') {
      this.toastService.success(msg);
    } else if (type === 'warning') {
      this.toastService.warning(msg);
    } else {
      this.toastService.info(msg);
    }
  }

  public selectSubject(subjectName: string): void {
    this.selectedSubject = subjectName;
    this.applySubjectFilter();
  }

  public applySubjectFilter(): void {
    let list = [...this.contents];

    if (this.selectedSubject !== 'Todas') {
      list = list.filter(c => c.subject.toLowerCase() === this.selectedSubject.toLowerCase());
    }

    if (this.missionStatusFilter === 'PENDIENTES') {
      list = list.filter(c => c.type === 'Assignment' && !c.hasSubmitted);
    } else if (this.missionStatusFilter === 'ENTREGADAS') {
      list = list.filter(c => c.type === 'Assignment' && c.hasSubmitted);
    } else if (this.missionStatusFilter === 'CALIFICADAS') {
      list = list.filter(c => c.type === 'Assignment' && c.mySubmission?.grade !== null && c.mySubmission?.grade !== undefined);
    }

    const q = this.searchQuery.trim().toLowerCase();
    if (q) {
      list = list.filter(c =>
        c.title.toLowerCase().includes(q) ||
        (c.description && c.description.toLowerCase().includes(q)) ||
        c.subject.toLowerCase().includes(q) ||
        (c.createdByUserName && c.createdByUserName.toLowerCase().includes(q))
      );
    }

    this.filteredContents = list;
  }

  public openSubmissionModal(content: Content): void {
    this.selectedTaskDetail = null;
    this.activeAssignment = content;
    this.activeAssignmentId = content.id;
    this.activeAssignmentTitle = content.title;
    this.showDropZone = true;
  }

  public closeSubmissionModal(): void {
    this.showDropZone = false;
    this.activeAssignment = null;
    this.activeAssignmentId = null;
  }

  public openTaskDetailModal(content: Content): void {
    this.selectedTaskDetail = content;
  }

  public closeTaskDetailModal(): void {
    this.selectedTaskDetail = null;
  }

  public onAssignmentSubmitted(): void {
    this.closeSubmissionModal();
    this.showToast('¡Tu tarea fue enviada con éxito a tu profe! 🎉⭐', 'success');
    this.loadDashboard();
    this.loadMetrics();
  }

  public openMedia(url?: string, filename?: string): void {
    if (url) {
      this.fileDownload.downloadFile(url, filename || 'recurso');
    }
  }

  public isOverdue(dueDate?: string | Date): boolean {
    if (!dueDate) return false;
    return new Date(dueDate).getTime() < Date.now();
  }

  public formatFileSize(bytes?: number): string {
    if (!bytes) return '';
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / 1048576).toFixed(1) + ' MB';
  }

  public logout(): void {
    this.authService.logout();
  }
}
