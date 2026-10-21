import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { TeacherService, TeacherMetrics, TeacherActivity, UpdateActivityDto } from '../../core/services/teacher.service';
import { AuthService } from '../../core/services/auth.service';
import { StudentStatus, SemaforoSummaryMetrics } from '../../core/models/semaforo.model';
import { CreateContent, StudentSubmission } from '../../core/models/content.model';
import { Course, TeacherStudent } from '../../core/models/user.model';
import { ReportExportService } from '../../core/services/report-export.service';
import { FileDownloadService } from '../../core/services/file-download.service';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { LanguageSelectorComponent } from '../../shared/components/language-selector/language-selector.component';

@Component({
  selector: 'app-teacher-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe, LanguageSelectorComponent],
  templateUrl: './teacher-dashboard.component.html',
  styleUrls: ['./teacher-dashboard.component.css']
})
export class TeacherDashboardComponent implements OnInit, OnDestroy {
  // Pestañas: 'activities' | 'semaforo' | 'students'
  public activeTab: 'activities' | 'semaforo' | 'students' = 'activities';

  // --- Métricas Académicas ---
  public academicMetrics: TeacherMetrics = {
    totalStudents: 0,
    pendingGradingCount: 0,
    activeTasksCount: 0,
    submissionRate: 0
  };

  // --- Módulo CRUD de Actividades (Tareas y Recursos) ---
  public activities: TeacherActivity[] = [];
  public filteredActivities: TeacherActivity[] = [];
  public activityStatusFilter: 'ALL' | 'ACTIVE' | 'ARCHIVED' = 'ALL';
  public activityKindFilter: 'ALL' | 'Tarea' | 'Material' | 'PENDING_GRADING' = 'ALL';
  public activitySearchQuery: string = '';
  public isLoadingActivities = false;

  // Modal Crear Actividad
  public showCreateModal = false;
  public isSavingContent = false;
  public isUploadingResourceFile = false;
  public uploadedResourceFileName = '';
  public newContent: CreateContent = {
    title: '',
    description: '',
    type: 'Assignment',
    subject: 'Matemáticas',
    gradeLevel: '1°',
    fileUrl: '',
    dueDate: '',
    maxScore: 5.0,
    courseId: undefined
  };

  // Modal Editar Actividad
  public showEditModal = false;
  public isEditingActivity = false;
  public isUploadingEditResourceFile = false;
  public selectedActivityToEdit: TeacherActivity | null = null;
  public editActivityForm: UpdateActivityDto = {
    title: '',
    description: '',
    dueDate: '',
    maxScore: 5.0,
    resourceUrl: ''
  };

  // --- Cajón / Modal de Entregas y Calificación ---
  public showSubmissionsModal = false;
  public selectedActivityForSubmissions: TeacherActivity | null = null;
  public submissionsList: StudentSubmission[] = [];
  public submissionFilter: 'ALL' | 'PENDING' | 'GRADED' = 'ALL';
  public isLoadingSubmissions = false;
  public gradingSubmission: StudentSubmission | null = null;
  public gradeInput: number = 5.0;
  public feedbackInput: string = '';
  public isSavingGrade = false;

  // --- Módulo Semáforo ---
  public students: StudentStatus[] = [];
  public filteredStudents: StudentStatus[] = [];
  public metrics: SemaforoSummaryMetrics = {
    totalStudents: 0,
    greenCount: 0,
    yellowCount: 0,
    redCount: 0
  };
  public isLoadingSemaforo = true;
  public selectedGrade = '';
  public selectedStatusFilter: 'All' | 'Green' | 'Yellow' | 'Red' = 'All';
  public viewMode: 'cards' | 'table' = 'cards';

  // --- Módulo Gestión de Alumnos y Cursos ---
  public courses: Course[] = [];
  public selectedCourseId: number | null = null;
  public allStudents: TeacherStudent[] = [];
  public filteredAllStudents: TeacherStudent[] = [];
  public studentSearch = '';
  public studentGradeFilter = '';
  public isLoadingStudents = false;

  // Modal crear nuevo estudiante
  public showCreateStudentModal = false;
  public isCreatingStudent = false;
  public createStudentError: string | null = null;
  public newStudent = {
    username: '',
    fullName: '',
    password: '',
    grade: '1°'
  };

  // Modal crear nuevo curso/materia
  public showCreateCourseModal = false;
  public isCreatingCourse = false;
  public newCourse = {
    nombre: '',
    grado: '1°',
    grupo: 'A',
    descripcion: ''
  };

  // Notificaciones
  public enrollingMap: { [key: number]: boolean } = {};
  public enrollNotification: { message: string; type: 'success' | 'info' | 'error' } | null = null;
  private readonly destroy$ = new Subject<void>();
  private toastTimeoutId: ReturnType<typeof setTimeout> | null = null;

  constructor(
    public authService: AuthService,
    private teacherService: TeacherService,
    private reportService: ReportExportService,
    private fileDownload: FileDownloadService
  ) {}

  ngOnInit(): void {
    this.loadAcademicMetrics();
    this.loadActivities();
    this.loadCourses();
    this.loadAllStudents();
    this.loadSemaforoData();
  }

  ngOnDestroy(): void {
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
      this.toastTimeoutId = null;
    }
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Descarga autenticada de la entrega (el endpoint /api/files exige JWT).
   */
  public downloadSubmission(submission: StudentSubmission): void {
    if (!submission?.fileUrl) {
      return;
    }

    this.fileDownload.save(submission.fileUrl, submission.originalFileName || 'entrega');
  }

  public switchTab(tab: 'activities' | 'semaforo' | 'students'): void {
    this.activeTab = tab;
    if (tab === 'activities') {
      this.loadAcademicMetrics();
      this.loadActivities();
    } else if (tab === 'students') {
      this.loadAllStudents();
      this.loadCourses();
    } else if (tab === 'semaforo') {
      this.loadSemaforoData();
    }
  }

  // --- Métricas Académicas y Actividades ---
  public loadAcademicMetrics(): void {
    this.teacherService.getAcademicMetrics()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (m) => this.academicMetrics = m
      });
  }

  public loadActivities(): void {
    this.isLoadingActivities = true;
    this.teacherService.getActivities()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (acts) => {
          this.activities = acts;
          this.applyActivityFilter();
          this.isLoadingActivities = false;
        },
        error: () => {
          this.isLoadingActivities = false;
        }
      });
  }

  public onTeacherKpiClick(metric: 'students' | 'pendingGrading' | 'activeTasks' | 'semaforo'): void {
    if (metric === 'students') {
      this.switchTab('students');
      return;
    }
    if (metric === 'semaforo') {
      this.switchTab('semaforo');
      return;
    }
    this.activeTab = 'activities';
    this.activityStatusFilter = 'ACTIVE';
    if (metric === 'pendingGrading') {
      this.activityKindFilter = 'PENDING_GRADING';
    } else {
      this.activityKindFilter = 'Tarea';
    }
    this.applyActivityFilter();
  }

  public setActivityFilter(filter: 'ALL' | 'ACTIVE' | 'ARCHIVED'): void {
    this.activityStatusFilter = filter;
    this.applyActivityFilter();
  }

  public setActivityKindFilter(kind: 'ALL' | 'Tarea' | 'Material' | 'PENDING_GRADING'): void {
    this.activityKindFilter = kind;
    this.applyActivityFilter();
  }

  public get pendingGradingTasksCount(): number {
    return this.activities.filter(a => a.kind === 'Tarea' && a.pendingGradingCount > 0).length;
  }

  public applyActivityFilter(): void {
    let list = [...this.activities];
    if (this.activityStatusFilter === 'ACTIVE') {
      list = list.filter(a => a.isActive);
    } else if (this.activityStatusFilter === 'ARCHIVED') {
      list = list.filter(a => !a.isActive);
    }

    if (this.activityKindFilter === 'Tarea') {
      list = list.filter(a => a.kind === 'Tarea');
    } else if (this.activityKindFilter === 'Material') {
      list = list.filter(a => a.kind === 'Material');
    } else if (this.activityKindFilter === 'PENDING_GRADING') {
      list = list.filter(a => a.kind === 'Tarea' && a.pendingGradingCount > 0);
    }

    if (this.activitySearchQuery.trim()) {
      const q = this.activitySearchQuery.toLowerCase().trim();
      list = list.filter(a =>
        a.title.toLowerCase().includes(q) ||
        a.courseName.toLowerCase().includes(q) ||
        (a.description && a.description.toLowerCase().includes(q))
      );
    }

    this.filteredActivities = list;
  }

  // Crear Actividad
  public openCreateModal(): void {
    const defaultCourse = this.courses.length > 0 ? this.courses[0] : undefined;
    this.uploadedResourceFileName = '';
    this.newContent = {
      title: '',
      description: '',
      type: 'Assignment',
      subject: defaultCourse ? defaultCourse.nombre : 'Matemáticas',
      gradeLevel: defaultCourse ? defaultCourse.grado : '1°',
      courseId: defaultCourse ? defaultCourse.id : undefined,
      fileUrl: '',
      maxScore: 5.0,
      dueDate: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString().substring(0, 10)
    };
    this.showCreateModal = true;
  }

  public onCourseSelectForContent(courseId?: number): void {
    if (!courseId) return;
    const found = this.courses.find(c => c.id === Number(courseId));
    if (found) {
      this.newContent.courseId = found.id;
      this.newContent.subject = found.nombre;
      this.newContent.gradeLevel = found.grado;
    }
  }

  public appendInstructionTemplate(kind: 'steps' | 'rubric' | 'deliverable'): void {
    const templates: Record<'steps' | 'rubric' | 'deliverable', string> = {
      steps: '📌 PASOS DE LA MISIÓN:\n1. Lee con atención la guía de clase.\n2. Desarrolla los ejercicios propuestos en tu cuaderno o documento.\n3. Sube tu evidencia antes de la fecha límite.',
      rubric: '⭐ CRITERIOS DE EVALUACIÓN:\n• Orden y presentación clara (30%)\n• Desarrollo completo de los ejercicios (50%)\n• Puntualidad en la entrega (20%)',
      deliverable: '📎 ENTREGABLE ESPERADO:\nSube un archivo PDF, foto nítida (JPG/PNG) o documento Word con tu nombre y grado.'
    };
    const snippet = templates[kind];
    this.newContent.description = this.newContent.description?.trim()
      ? `${this.newContent.description.trim()}\n\n${snippet}`
      : snippet;
  }

  public closeCreateModal(): void {
    this.showCreateModal = false;
  }

  public onResourceFileSelected(event: any): void {
    const file = event.target.files?.[0];
    if (!file) return;

    this.isUploadingResourceFile = true;
    this.teacherService.uploadResource(file)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.isUploadingResourceFile = false;
          this.newContent.fileUrl = res.fileUrl;
          this.uploadedResourceFileName = res.originalName;
          this.showToast(`Archivo guía "${res.originalName}" adjuntado exitosamente.`, 'success');
        },
        error: (err) => {
          this.isUploadingResourceFile = false;
          alert('Error al subir archivo: ' + (err.error?.message || err.message));
        }
      });
  }

  public onEditResourceFileSelected(event: any): void {
    const file = event.target.files?.[0];
    if (!file) return;

    this.isUploadingEditResourceFile = true;
    this.teacherService.uploadResource(file)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.isUploadingEditResourceFile = false;
          this.editActivityForm.resourceUrl = res.fileUrl;
          this.showToast(`Guía "${res.originalName}" actualizada exitosamente.`, 'success');
        },
        error: (err) => {
          this.isUploadingEditResourceFile = false;
          alert('Error al subir archivo: ' + (err.error?.message || err.message));
        }
      });
  }

  public downloadResourceUrl(url?: string, filename?: string): void {
    if (!url) return;
    this.fileDownload.save(url, filename || 'guia-clase');
  }

  public saveContent(): void {
    if (!this.newContent.title.trim()) {
      alert('Por favor ingresa un título para la actividad.');
      return;
    }

    this.isSavingContent = true;
    this.teacherService.createContent(this.newContent)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.isSavingContent = false;
          this.closeCreateModal();
          this.showToast('¡Actividad creada y publicada con éxito! 🎉', 'success');
          this.loadActivities();
          this.loadAcademicMetrics();
        },
        error: (err) => {
          this.isSavingContent = false;
          alert('Error al publicar la actividad: ' + (err.error?.message || err.message));
        }
      });
  }

  // Editar Actividad
  public openEditModal(act: TeacherActivity): void {
    this.selectedActivityToEdit = act;
    this.editActivityForm = {
      title: act.title,
      description: act.description || '',
      dueDate: act.dueDate ? new Date(act.dueDate).toISOString().substring(0, 10) : '',
      maxScore: act.maxScore || 5.0,
      resourceUrl: act.resourceUrl || ''
    };
    this.showEditModal = true;
  }

  public closeEditModal(): void {
    this.showEditModal = false;
    this.selectedActivityToEdit = null;
  }

  public saveEditActivity(): void {
    if (!this.selectedActivityToEdit) return;
    if (!this.editActivityForm.title.trim()) {
      alert('El título es requerido.');
      return;
    }

    this.isEditingActivity = true;
    const kind = this.selectedActivityToEdit.kind;
    const id = this.selectedActivityToEdit.id;

    this.teacherService.updateActivity(id, kind, this.editActivityForm)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.isEditingActivity = false;
          this.closeEditModal();
          this.showToast(res.message, 'success');
          this.loadActivities();
        },
        error: (err) => {
          this.isEditingActivity = false;
          alert('Error al editar la actividad: ' + (err.error?.message || err.message));
        }
      });
  }

  public toggleActivityStatus(act: TeacherActivity): void {
    const actionText = act.isActive ? 'archivar' : 'reactivar';
    if (!confirm(`¿Estás seguro de ${actionText} "${act.title}"? Los datos históricos no se eliminarán.`)) {
      return;
    }

    this.teacherService.toggleActivityStatus(act.id, act.kind)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.showToast(res.message, 'info');
          act.isActive = res.isActive;
          this.applyActivityFilter();
          this.loadAcademicMetrics();
        },
        error: (err) => {
          alert('Error al cambiar el estado de la actividad: ' + (err.error?.message || err.message));
        }
      });
  }

  // --- Módulo de Entregas y Calificación ---
  public openSubmissionsModal(act: TeacherActivity): void {
    this.selectedActivityForSubmissions = act;
    this.submissionFilter = 'ALL';
    this.showSubmissionsModal = true;
    this.gradingSubmission = null;
    this.loadSubmissions(act.id);
  }

  public closeSubmissionsModal(): void {
    this.showSubmissionsModal = false;
    this.selectedActivityForSubmissions = null;
    this.gradingSubmission = null;
  }

  public loadSubmissions(activityId: number): void {
    this.isLoadingSubmissions = true;
    this.teacherService.getAssignmentSubmissions(activityId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (subs) => {
          this.submissionsList = subs;
          this.isLoadingSubmissions = false;
        },
        error: () => {
          this.isLoadingSubmissions = false;
        }
      });
  }

  public get filteredSubmissionsList(): StudentSubmission[] {
    if (this.submissionFilter === 'PENDING') {
      return this.submissionsList.filter(s => s.grade === null || s.grade === undefined);
    }
    if (this.submissionFilter === 'GRADED') {
      return this.submissionsList.filter(s => s.grade !== null && s.grade !== undefined);
    }
    return this.submissionsList;
  }

  public get ungradedSubmissionsCount(): number {
    return this.submissionsList.filter(s => s.grade === null || s.grade === undefined).length;
  }

  public get gradedSubmissionsCount(): number {
    return this.submissionsList.filter(s => s.grade !== null && s.grade !== undefined).length;
  }

  public get averageSubmissionGrade(): string {
    const graded = this.submissionsList.filter(s => s.grade !== null && s.grade !== undefined);
    if (graded.length === 0) return '—';
    const avg = graded.reduce((acc, cur) => acc + (cur.grade || 0), 0) / graded.length;
    return avg.toFixed(1);
  }

  public getQuickGradePresets(): number[] {
    const max = this.selectedActivityForSubmissions?.maxScore || 5.0;
    if (max === 100) return [100, 90, 80, 70, 60];
    if (max === 10) return [10, 9, 8, 7, 6];
    return [5.0, 4.5, 4.0, 3.5, 3.0];
  }

  public setQuickGrade(score: number): void {
    this.gradeInput = score;
  }

  public formatFileSize(bytes?: number): string {
    if (!bytes) return '';
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / 1048576).toFixed(1) + ' MB';
  }

  public startGrading(sub: StudentSubmission): void {
    this.gradingSubmission = sub;
    const defaultMax = this.selectedActivityForSubmissions?.maxScore || 5.0;
    this.gradeInput = (sub.grade !== undefined && sub.grade !== null) ? sub.grade : defaultMax;
    this.feedbackInput = sub.feedback || '';
  }

  public cancelGrading(): void {
    this.gradingSubmission = null;
  }

  public saveGrade(): void {
    if (!this.gradingSubmission) return;
    const subId = this.gradingSubmission.submissionId || parseInt(this.gradingSubmission.id, 10);
    if (!subId) {
      alert('Identificador de entrega no válido.');
      return;
    }

    this.isSavingGrade = true;
    this.teacherService.gradeSubmission(subId, this.gradeInput, this.feedbackInput)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.isSavingGrade = false;
          this.showToast(res.message, 'success');
          if (this.gradingSubmission) {
            this.gradingSubmission.grade = res.grade;
            this.gradingSubmission.feedback = res.feedback;
            this.gradingSubmission.status = res.status;
            this.gradingSubmission.gradedAt = new Date().toISOString();
          }
          this.gradingSubmission = null;
          this.loadAcademicMetrics();
          this.loadActivities();
        },
        error: (err) => {
          this.isSavingGrade = false;
          alert('Error al calificar: ' + (err?.error?.message || err.message));
        }
      });
  }

  // --- Módulo Semáforo ---
  public loadSemaforoData(): void {
    this.isLoadingSemaforo = true;
    this.teacherService.getStudentsStatus(this.selectedGrade)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.students = data;
          this.applySemaforoFilter();
          this.isLoadingSemaforo = false;
        },
        error: () => this.isLoadingSemaforo = false
      });

    this.teacherService.getMetrics(this.selectedGrade)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => this.metrics = res
      });
  }

  public setStatusFilter(status: 'All' | 'Green' | 'Yellow' | 'Red'): void {
    this.selectedStatusFilter = status;
    this.applySemaforoFilter();
  }

  private applySemaforoFilter(): void {
    if (this.selectedStatusFilter === 'All') {
      this.filteredStudents = this.students;
    } else {
      this.filteredStudents = this.students.filter(s => s.semaforoColor === this.selectedStatusFilter);
    }
  }

  // --- Cursos y Alumnos ---
  public loadCourses(): void {
    this.teacherService.getCourses()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (c) => {
          this.courses = c;
          if (c.length > 0 && !this.selectedCourseId) {
            this.selectedCourseId = c[0].id;
          }
        }
      });
  }

  public onCourseChange(): void {
    // Al cambiar de curso, se refresca la vista
  }

  public get currentSelectedCourse(): Course | undefined {
    return this.courses.find(c => c.id === this.selectedCourseId);
  }

  public get enrolledStudentsInSelectedCourse(): TeacherStudent[] {
    if (!this.selectedCourseId) return [];
    return this.filteredAllStudents.filter(s => this.isStudentEnrolledInCourse(s, this.selectedCourseId));
  }

  public get availableStudentsForSelectedCourse(): TeacherStudent[] {
    if (!this.selectedCourseId) return [];
    return this.filteredAllStudents.filter(s => !this.isStudentEnrolledInCourse(s, this.selectedCourseId));
  }

  public loadAllStudents(): void {
    this.isLoadingStudents = true;
    this.teacherService.getAllStudents()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.allStudents = res;
          this.onStudentFilterChange();
          this.isLoadingStudents = false;
        },
        error: () => this.isLoadingStudents = false
      });
  }

  public onStudentFilterChange(): void {
    let list = this.allStudents;
    if (this.studentGradeFilter) {
      list = list.filter(s => s.grade === this.studentGradeFilter || s.grade.startsWith(this.studentGradeFilter));
    }
    if (this.studentSearch.trim()) {
      const q = this.studentSearch.toLowerCase().trim();
      list = list.filter(s => s.fullName.toLowerCase().includes(q) || s.username.toLowerCase().includes(q));
    }
    this.filteredAllStudents = list;
  }

  public enrollStudent(student: TeacherStudent): void {
    if (!this.selectedCourseId) {
      alert('Por favor selecciona un curso para matricular al estudiante.');
      return;
    }

    const courseId = this.selectedCourseId;
    this.enrollingMap[student.id] = true;

    this.teacherService.enrollStudent(courseId, student.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.enrollingMap[student.id] = false;
          const currentCourse = this.courses.find(c => c.id === courseId);
          const courseName = currentCourse ? currentCourse.nombre : 'el curso';

          if (res.yaMatriculado) {
            this.showToast(`${student.fullName} ya estaba matriculado(a) en ${courseName}.`, 'info');
          } else {
            this.showToast(`¡${student.fullName} fue matriculado(a) exitosamente en ${courseName}! 🎉`, 'success');
            student.enrolledCourses.push({
              courseId: courseId,
              courseName: courseName,
              group: currentCourse?.grupo || 'A',
              status: 'ACTIVA'
            });
            if (currentCourse) currentCourse.totalStudents++;
            this.loadAcademicMetrics();
          }
        },
        error: (err) => {
          this.enrollingMap[student.id] = false;
          alert(err?.error?.message || 'Error al matricular al estudiante.');
        }
      });
  }

  public unenrollStudent(student: TeacherStudent): void {
    if (!this.selectedCourseId) return;
    const currentCourse = this.courses.find(c => c.id === this.selectedCourseId);
    const courseName = currentCourse ? currentCourse.nombre : 'este curso';

    if (!confirm(`¿Estás seguro de desmatricular a ${student.fullName} de ${courseName}?`)) {
      return;
    }

    this.teacherService.unenrollStudent(this.selectedCourseId, student.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.showToast(res.message, 'info');
          student.enrolledCourses = student.enrolledCourses.filter(c => c.courseId !== this.selectedCourseId);
          if (currentCourse && currentCourse.totalStudents > 0) currentCourse.totalStudents--;
          this.loadAcademicMetrics();
        },
        error: (err) => {
          alert(err?.error?.message || 'Error al desmatricular al estudiante.');
        }
      });
  }

  public isStudentEnrolledInCourse(student: TeacherStudent, courseId: number | null): boolean {
    if (!courseId) return false;
    return student.enrolledCourses.some(c => c.courseId === courseId && c.status === 'ACTIVA');
  }

  public openCreateCourseModal(): void {
    this.newCourse = { nombre: '', grado: '1°', grupo: 'A', descripcion: '' };
    this.showCreateCourseModal = true;
  }

  public closeCreateCourseModal(): void {
    this.showCreateCourseModal = false;
  }

  public saveCourse(): void {
    if (!this.newCourse.nombre.trim()) {
      alert('El nombre del curso es requerido.');
      return;
    }
    this.isCreatingCourse = true;
    this.teacherService.createCourse(this.newCourse)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (course) => {
          this.isCreatingCourse = false;
          this.showCreateCourseModal = false;
          this.courses.push(course);
          this.selectedCourseId = course.id;
          this.showToast(`¡Materia ${course.nombre} creada con éxito! 📘`, 'success');
        },
        error: (err) => {
          this.isCreatingCourse = false;
          alert(err?.error?.message || 'Error al crear el curso.');
        }
      });
  }

  public openCreateStudentModal(): void {
    this.newStudent = { username: '', fullName: '', password: '', grade: '1°' };
    this.createStudentError = null;
    this.showCreateStudentModal = true;
  }

  public closeCreateStudentModal(): void {
    this.showCreateStudentModal = false;
  }

  public saveStudent(): void {
    if (!this.newStudent.username.trim() || !this.newStudent.fullName.trim()) {
      this.createStudentError = 'Por favor completa el Documento/Carnet y el Nombre Completo.';
      return;
    }

    const password = this.newStudent.password.trim();
    if (password.length > 0) {
      const hasMinLength = password.length >= 8;
      const hasUpper = /[A-ZÁÉÍÓÚÑ]/.test(password);
      const hasLower = /[a-záéíóúñ]/.test(password);
      const hasDigit = /\d/.test(password);
      const hasSpecial = /[^A-Za-z0-9\s]/.test(password);

      if (!hasMinLength || !hasUpper || !hasLower || !hasDigit || !hasSpecial) {
        this.createStudentError = 'La contraseña personalizada debe tener mínimo 8 caracteres e incluir mayúscula, minúscula, número y carácter especial.';
        return;
      }
    }

    this.isCreatingStudent = true;
    this.createStudentError = null;

    this.teacherService.createStudent({
      username: this.newStudent.username.trim(),
      fullName: this.newStudent.fullName.trim(),
      password,
      grade: this.newStudent.grade
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (created) => {
          this.isCreatingStudent = false;
          this.showCreateStudentModal = false;

          const message = created.temporaryPassword
            ? `Alumno ${created.fullName} registrado. Contraseña temporal: ${created.temporaryPassword} — entrégala al acudiente.`
            : `¡Alumno ${created.fullName} registrado con éxito! 🎓`;

          this.showToast(message, 'success');
          this.loadAllStudents();
          this.loadAcademicMetrics();
        },
        error: (err) => {
          this.isCreatingStudent = false;
          this.createStudentError = err?.error?.message || 'Error al registrar al estudiante.';
        }
      });
  }

  public showToast(message: string, type: 'success' | 'info' | 'error'): void {
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
    }
    this.enrollNotification = { message, type };
    this.toastTimeoutId = setTimeout(() => {
      this.enrollNotification = null;
      this.toastTimeoutId = null;
    }, 4500);
  }

  public exportCurrentTabReport(format: 'csv' | 'pdf'): void {
    const authorName = this.authService.currentUser()?.fullName || 'Docente Titular';
    const today = new Date().toISOString().slice(0, 10);

    if (this.activeTab === 'activities') {
      const headers = ['ID', 'Título', 'Materia', 'Grado', 'Tipo', 'Fecha Límite', 'Entregas', 'Por Calificar', 'Estado'];
      const rows = this.filteredActivities.map(act => [
        act.id,
        act.title,
        act.courseName || '-',
        act.gradeName || '-',
        act.kind || 'Tarea',
        act.dueDate ? new Date(act.dueDate).toLocaleDateString('es-CO') : 'Sin fecha límite',
        act.submissionsCount,
        act.pendingGradingCount,
        act.isActive ? 'Publicada' : 'Archivada'
      ]);

      if (format === 'csv') {
        this.reportService.exportToCsv(`reporte_actividades_docente_${today}`, headers, rows);
        this.showToast('Reporte de actividades en CSV descargado exitosamente 📊', 'success');
      } else {
        this.reportService.exportToPdf({
          title: 'Reporte de Actividades, Tareas y Evaluaciones',
          subtitle: `Filtro de catálogo: ${this.activityStatusFilter === 'ALL' ? 'Todas' : this.activityStatusFilter === 'ACTIVE' ? 'Publicadas' : 'Archivadas'} • Total: ${this.filteredActivities.length} actividades`,
          author: authorName,
          institution: 'LMS Primaria • Portal Docente',
          headers,
          rows,
          filename: `reporte_actividades_docente_${today}`,
          summaryCards: [
            { label: 'Alumnos a Cargo', value: this.academicMetrics.totalStudents, color: 'blue' },
            { label: 'Tareas Publicadas', value: this.academicMetrics.activeTasksCount, color: 'green' },
            { label: 'Por Calificar', value: this.academicMetrics.pendingGradingCount, color: 'yellow' },
            { label: 'Cumplimiento', value: `${this.academicMetrics.submissionRate}%`, color: 'purple' }
          ]
        });
        this.showToast('Reporte de actividades en PDF generado exitosamente 📄', 'success');
      }
    } else if (this.activeTab === 'semaforo') {
      const headers = ['Estudiante', 'Grado', 'Estado Semáforo', 'Días Ausente', 'Último Acceso', 'Detalle y Recomendación'];
      const rows = this.filteredStudents.map(s => [
        s.fullName,
        s.gradeLevel,
        s.semaforoLabel || (s.semaforoColor === 'Green' ? 'Al día' : s.semaforoColor === 'Yellow' ? 'Alerta' : 'Riesgo'),
        s.inactiveDays > 0 ? `${s.inactiveDays} días` : 'Ingresó hoy',
        s.lastLoginDate ? new Date(s.lastLoginDate).toLocaleDateString('es-CO') : 'Sin registro',
        s.description || '-'
      ]);

      if (format === 'csv') {
        this.reportService.exportToCsv(`reporte_semaforo_asistencia_${today}`, headers, rows);
        this.showToast('Reporte de semáforo escolar en CSV descargado exitosamente 📊', 'success');
      } else {
        this.reportService.exportToPdf({
          title: 'Reporte de Monitoreo Semáforo y Alerta Temprana',
          subtitle: `Filtro: ${this.selectedStatusFilter} • Grado: ${this.selectedGrade || 'Todos los grados'} • Evaluados: ${this.filteredStudents.length}`,
          author: authorName,
          institution: 'LMS Primaria • Monitoreo Académico',
          headers,
          rows,
          filename: `reporte_semaforo_asistencia_${today}`,
          summaryCards: [
            { label: 'Total Alumnos', value: this.metrics.totalStudents, color: 'blue' },
            { label: 'Al Día (Verde)', value: this.metrics.greenCount, color: 'green' },
            { label: 'Alerta (Amarillo)', value: this.metrics.yellowCount, color: 'yellow' },
            { label: 'Riesgo (Rojo)', value: this.metrics.redCount, color: 'red' }
          ]
        });
        this.showToast('Reporte de semáforo en PDF generado exitosamente 📄', 'success');
      }
    } else if (this.activeTab === 'students') {
      const selectedCourseName = this.courses.find(c => c.id === this.selectedCourseId)?.nombre || 'Todos los cursos';
      const headers = ['Documento / Carnet', 'Nombre del Estudiante', 'Grado', 'Materias Asignadas', 'Total Asignaciones'];
      const rows = this.filteredAllStudents.map(st => [
        st.username,
        st.fullName,
        st.grade || '1°',
        st.enrolledCourses?.map(c => c.courseName).join(', ') || 'Sin cursos',
        st.enrolledCourses?.length || 0
      ]);

      if (format === 'csv') {
        this.reportService.exportToCsv(`reporte_alumnos_matricula_${today}`, headers, rows);
        this.showToast('Reporte de alumnos en CSV descargado exitosamente 📊', 'success');
      } else {
        this.reportService.exportToPdf({
          title: 'Reporte de Directorio Escolar y Matrícula',
          subtitle: `Materia seleccionada: ${selectedCourseName} • Total de alumnos registrados: ${this.filteredAllStudents.length}`,
          author: authorName,
          institution: 'LMS Primaria • Directorio Institucional',
          headers,
          rows,
          filename: `reporte_alumnos_matricula_${today}`,
          summaryCards: [
            { label: 'Total Estudiantes', value: this.filteredAllStudents.length, color: 'blue' },
            { label: 'Materias en Sistema', value: this.courses.length, color: 'green' }
          ]
        });
        this.showToast('Reporte de alumnos en PDF generado exitosamente 📄', 'success');
      }
    }
  }

  public logout(): void {
    this.authService.logout();
  }
}
