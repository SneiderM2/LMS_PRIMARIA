import { Component, OnInit, OnDestroy, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { AdminService, AdminMetrics, AdminUser, CreateAdminUserDto, UpdateAdminUserDto, AdminCourse, CreateAdminCourseDto, UpdateAdminCourseDto } from '../../core/services/admin.service';
import { SystemInspectorComponent } from './system-inspector/system-inspector.component';
import { ReportExportService } from '../../core/services/report-export.service';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { LanguageSelectorComponent } from '../../shared/components/language-selector/language-selector.component';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, SystemInspectorComponent, TranslatePipe, LanguageSelectorComponent],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.css']
})
export class AdminDashboardComponent implements OnInit, OnDestroy {
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  // Pestañas: 'overview' (Usuarios), 'courses' (Materias/Cursos), 'inspector' (Auditoría/Logs)
  public activeSection: 'overview' | 'courses' | 'inspector' = 'overview';

  // Métricas Institucionales
  public metrics: AdminMetrics = {
    totalActiveUsers: 0,
    activeTeachersCount: 0,
    activeStudentsCount: 0,
    systemStatus: 'Operativo',
    recentErrorsCount: 0,
    totalCoursesCount: 0
  };
  public isLoadingMetrics = true;

  // --- CRUD Directorio de Usuarios ---
  public users: AdminUser[] = [];
  public filteredUsers: AdminUser[] = [];
  public roleFilter: string = 'ALL';
  public userStatusFilter: 'ALL' | 'ACTIVE' | 'SUSPENDED' = 'ALL';
  public userSearchQuery: string = '';
  public isLoadingUsers = false;

  // Modal Crear Usuario
  public showCreateUserModal = false;
  public isCreatingUser = false;
  public createUserError: string | null = null;
  public newUser: CreateAdminUserDto = {
    username: '',
    password: '',
    nombre: '',
    apellido: '',
    role: 'Student',
    gradeLevel: '1°'
  };

  // Modal Editar Usuario
  public showEditUserModal = false;
  public isEditingUser = false;
  public selectedUserToEdit: AdminUser | null = null;
  public editUserForm: UpdateAdminUserDto = {
    nombre: '',
    apellido: '',
    gradeLevel: '1°',
    password: ''
  };

  // --- CRUD Institucional de Cursos y Materias ---
  public courses: AdminCourse[] = [];
  public filteredCourses: AdminCourse[] = [];
  public courseSearchQuery: string = '';
  public courseGradeFilter: string = 'ALL';
  public isLoadingCourses = false;

  // Modal Crear Curso
  public showCreateCourseModal = false;
  public isCreatingCourse = false;
  public createCourseError: string | null = null;
  public newCourse: CreateAdminCourseDto = {
    nombre: '',
    grado: '1°',
    grupo: 'A',
    docenteId: undefined,
    descripcion: ''
  };

  // Modal Editar Curso
  public showEditCourseModal = false;
  public isEditingCourse = false;
  public selectedCourseToEdit: AdminCourse | null = null;
  public editCourseForm: UpdateAdminCourseDto = {
    nombre: '',
    grado: '1°',
    grupo: 'A',
    docenteId: undefined,
    descripcion: ''
  };

  // Estados de Operación de Backup
  public isDownloadingBackup = false;
  public isRestoringBackup = false;

  // Toast Flotante
  public toastMessage: string | null = null;
  public toastType: 'success' | 'info' | 'warning' = 'info';
  private readonly destroy$ = new Subject<void>();
  private toastTimeoutId: ReturnType<typeof setTimeout> | null = null;

  constructor(
    public authService: AuthService,
    private adminService: AdminService,
    private router: Router,
    private reportService: ReportExportService
  ) { }

  ngOnInit(): void {
    this.loadMetrics();
    this.loadUsers();
    this.loadAdminCourses();
  }

  ngOnDestroy(): void {
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
      this.toastTimeoutId = null;
    }
    this.destroy$.next();
    this.destroy$.complete();
  }

  public setSection(section: 'overview' | 'courses' | 'inspector'): void {
    this.activeSection = section;
    if (section === 'overview') {
      this.loadUsers();
      this.loadMetrics();
    } else if (section === 'courses') {
      this.loadAdminCourses();
    }
  }

  public onAdminKpiClick(target: 'all-users' | 'teachers' | 'students' | 'inspector' | 'courses'): void {
    if (target === 'courses') {
      this.setSection('courses');
      return;
    }
    if (target === 'inspector') {
      this.setSection('inspector');
      return;
    }
    this.activeSection = 'overview';
    if (target === 'teachers') {
      this.filterByRole('Teacher');
    } else if (target === 'students') {
      this.filterByRole('Student');
    } else {
      this.filterByRole('ALL');
    }
  }

  // --- Métricas Globales ---
  public loadMetrics(): void {
    this.isLoadingMetrics = true;
    this.adminService.getMetrics()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (m) => {
          this.metrics = m;
          this.isLoadingMetrics = false;
        },
        error: () => {
          this.isLoadingMetrics = false;
        }
      });
  }

  public getSystemStatusKey(status?: string): string {
    if (!status) return 'STATUS.OPERATIONAL';
    const s = status.toLowerCase();
    if (s.includes('degradad') || s.includes('fallo')) return 'STATUS.DEGRADED';
    if (s.includes('offline') || s.includes('desconectad')) return 'STATUS.OFFLINE';
    return 'STATUS.OPERATIONAL';
  }

  // --- Usuarios CRUD ---
  public loadUsers(): void {
    this.isLoadingUsers = true;
    this.adminService.getUsers(this.roleFilter, this.userSearchQuery)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.users = this.sortUsers(data);
          this.applyUserFilters();
          this.isLoadingUsers = false;
        },
        error: () => {
          this.isLoadingUsers = false;
        }
      });
  }

  public filterByRole(role: string): void {
    this.roleFilter = role;
    this.loadUsers();
  }

  public filterByUserStatus(status: 'ALL' | 'ACTIVE' | 'SUSPENDED'): void {
    this.userStatusFilter = status;
    this.applyUserFilters();
  }

  public onSearch(): void {
    this.loadUsers();
  }

  private applyUserFilters(): void {
    let list = this.users;
    if (this.userStatusFilter === 'ACTIVE') {
      list = list.filter(u => u.activo);
    } else if (this.userStatusFilter === 'SUSPENDED') {
      list = list.filter(u => !u.activo);
    }
    this.filteredUsers = this.sortUsers(list);
  }

  /**
   * Jerarquía de roles:
   * 1° Administradores (Admin)
   * 2° Docentes (Teacher / Profe)
   * 3° Alumnos (Student / Alumno)
   */
  private getRolePriority(role?: string): number {
    if (!role) return 4;
    const r = role.toLowerCase();
    if (r.includes('admin')) return 1;
    if (r.includes('teacher') || r.includes('docente') || r.includes('profe')) return 2;
    if (r.includes('student') || r.includes('alumno') || r.includes('estudiante')) return 3;
    return 4;
  }

  /**
   * Peso para orden ascendente de grado (1°, 2°, 3°, 4°, 5°).
   * Alumnos sin grado asignado ("—", null o vacíos) van al final (999).
   */
  private getGradeWeight(gradeLevel?: string | null): number {
    if (!gradeLevel) return 999;
    const trimmed = gradeLevel.trim();
    if (!trimmed || trimmed === '—' || trimmed === '-' || trimmed === 'N/A') return 999;
    const match = trimmed.match(/\d+/);
    if (match) {
      return parseInt(match[0], 10);
    }
    return 500;
  }

  /**
   * Ordena los usuarios según prioridades estrictas:
   * 1. Rol: Admin -> Docente -> Alumno
   * 2. Grado escolar (Alumnos): 1°, 2°, 3°, 4°, 5°... y alumnos sin grado ("—") al final.
   * 3. Nombre alfabético (o ID descendente en desempate).
   */
  public sortUsers(usersList: AdminUser[]): AdminUser[] {
    return [...usersList].sort((a, b) => {
      // 1. Orden por Rol
      const roleA = this.getRolePriority(a.role);
      const roleB = this.getRolePriority(b.role);
      if (roleA !== roleB) {
        return roleA - roleB;
      }

      // 2. Orden secundario por Grado Escolar (dentro de Alumnos)
      if (roleA === 3) {
        const gradeA = this.getGradeWeight(a.gradeLevel);
        const gradeB = this.getGradeWeight(b.gradeLevel);
        if (gradeA !== gradeB) {
          return gradeA - gradeB;
        }
      }

      // 3. Orden terciario por Nombre / Apellido (o ID descendente)
      const nameA = (a.nombre || a.fullName || a.username || '').trim().toLowerCase();
      const nameB = (b.nombre || b.fullName || b.username || '').trim().toLowerCase();
      const nameComp = nameA.localeCompare(nameB, 'es', { sensitivity: 'base' });
      if (nameComp !== 0) {
        return nameComp;
      }

      return (b.id || 0) - (a.id || 0);
    });
  }

  public get teachersList(): AdminUser[] {
    return this.users.filter(u => u.role === 'Teacher' && u.activo);
  }

  public openCreateUserModal(): void {
    this.newUser = {
      username: '',
      password: '',
      nombre: '',
      apellido: '',
      role: 'Student',
      gradeLevel: '1°'
    };
    this.createUserError = null;
    this.showCreateUserModal = true;
  }

  public closeCreateUserModal(): void {
    this.showCreateUserModal = false;
  }

  private isNormalizedPassword(password: string): boolean {
    return (
      password.length >= 8 &&
      /[A-ZÁÉÍÓÚÑ]/.test(password) &&
      /[a-záéíóúñ]/.test(password) &&
      /\d/.test(password) &&
      /[^A-Za-z0-9\s]/.test(password)
    );
  }

  public saveNewUser(): void {
    if (!this.newUser.username.trim() || !this.newUser.nombre.trim() || !this.newUser.apellido.trim()) {
      this.createUserError = 'Documento, Nombre y Apellido son obligatorios.';
      return;
    }

    const password = (this.newUser.password || '').trim();
    if (password.length > 0 && !this.isNormalizedPassword(password)) {
      this.createUserError = 'La contraseña debe tener al menos 8 caracteres, incluyendo mayúscula, minúscula, número y carácter especial.';
      return;
    }

    this.isCreatingUser = true;
    this.createUserError = null;

    this.adminService.createUser(this.newUser)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (created) => {
          this.isCreatingUser = false;
          this.closeCreateUserModal();

          const message = created.temporaryPassword
            ? `Usuario ${created.fullName} creado. Contraseña temporal: ${created.temporaryPassword} — entrégala de forma segura.`
            : `Usuario ${created.fullName} registrado correctamente. 🎉`;

          this.showToast(message, 'success');
          this.loadUsers();
          this.loadMetrics();
        },
        error: (err) => {
          this.isCreatingUser = false;
          this.createUserError = err?.error?.message || 'Error al crear la cuenta de usuario.';
        }
      });
  }

  public openEditUserModal(user: AdminUser): void {
    this.selectedUserToEdit = user;
    this.editUserForm = {
      nombre: user.nombre,
      apellido: user.apellido,
      gradeLevel: user.gradeLevel || '1°',
      password: ''
    };
    this.showEditUserModal = true;
  }

  public closeEditUserModal(): void {
    this.showEditUserModal = false;
    this.selectedUserToEdit = null;
  }

  public saveEditUser(): void {
    if (!this.selectedUserToEdit) return;

    const resetPass = (this.editUserForm.password || '').trim();
    if (resetPass.length > 0 && !this.isNormalizedPassword(resetPass)) {
      alert('La nueva contraseña debe tener mínimo 8 caracteres e incluir mayúscula, minúscula, número y carácter especial.');
      return;
    }

    this.isEditingUser = true;
    this.adminService.updateUser(this.selectedUserToEdit.id, this.editUserForm)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.isEditingUser = false;
          this.closeEditUserModal();
          this.showToast(res.message, 'success');
          this.loadUsers();
        },
        error: (err) => {
          this.isEditingUser = false;
          alert('Error al actualizar usuario: ' + (err?.error?.message || err.message));
        }
      });
  }

  public toggleUserStatus(user: AdminUser): void {
    const action = user.activo ? 'suspender' : 'reactivar';
    if (!confirm(`¿Estás seguro de ${action} la cuenta de ${user.fullName}? Sus registros históricos se preservarán.`)) {
      return;
    }

    this.adminService.toggleUserStatus(user.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          user.activo = res.activo;
          this.showToast(res.message, 'info');
          this.applyUserFilters();
          this.loadMetrics();
        },
        error: (err) => {
          alert('Error al cambiar el estado del usuario: ' + (err?.error?.message || err.message));
        }
      });
  }

  // --- Cursos CRUD ---
  public loadAdminCourses(): void {
    this.isLoadingCourses = true;
    this.adminService.getCourses()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.courses = data;
          this.applyCourseFilters();
          this.isLoadingCourses = false;
        },
        error: () => {
          this.isLoadingCourses = false;
        }
      });
  }

  public applyCourseFilters(): void {
    let list = this.courses;
    if (this.courseGradeFilter !== 'ALL') {
      list = list.filter(c => c.grado === this.courseGradeFilter || c.grado.startsWith(this.courseGradeFilter));
    }
    if (this.courseSearchQuery.trim()) {
      const q = this.courseSearchQuery.toLowerCase().trim();
      list = list.filter(c => c.nombre.toLowerCase().includes(q) || c.docenteNombre.toLowerCase().includes(q));
    }
    this.filteredCourses = list;
  }

  public openCreateCourseModal(): void {
    this.newCourse = {
      nombre: '',
      grado: '1°',
      grupo: 'A',
      docenteId: this.teachersList.length > 0 ? this.teachersList[0].id : undefined,
      descripcion: ''
    };
    this.createCourseError = null;
    this.showCreateCourseModal = true;
  }

  public closeCreateCourseModal(): void {
    this.showCreateCourseModal = false;
  }

  public saveNewCourse(): void {
    if (!this.newCourse.nombre.trim()) {
      this.createCourseError = 'El nombre de la materia es requerido.';
      return;
    }
    this.isCreatingCourse = true;
    this.createCourseError = null;

    this.adminService.createCourse(this.newCourse)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (created) => {
          this.isCreatingCourse = false;
          this.closeCreateCourseModal();
          this.showToast(`Materia ${created.nombre} (${created.grado}) creada exitosamente. 📘`, 'success');
          this.loadAdminCourses();
          this.loadMetrics();
        },
        error: (err) => {
          this.isCreatingCourse = false;
          this.createCourseError = err?.error?.message || 'Error al registrar el curso.';
        }
      });
  }

  public openEditCourseModal(course: AdminCourse): void {
    this.selectedCourseToEdit = course;
    this.editCourseForm = {
      nombre: course.nombre,
      grado: course.grado,
      grupo: course.grupo,
      docenteId: course.docenteId,
      descripcion: course.descripcion || ''
    };
    this.showEditCourseModal = true;
  }

  public closeEditCourseModal(): void {
    this.showEditCourseModal = false;
    this.selectedCourseToEdit = null;
  }

  public saveEditCourse(): void {
    if (!this.selectedCourseToEdit) return;
    this.isEditingCourse = true;

    this.adminService.updateCourse(this.selectedCourseToEdit.id, this.editCourseForm)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.isEditingCourse = false;
          this.closeEditCourseModal();
          this.showToast(res.message, 'success');
          this.loadAdminCourses();
        },
        error: (err) => {
          this.isEditingCourse = false;
          alert('Error al editar curso: ' + (err?.error?.message || err.message));
        }
      });
  }

  public toggleCourseStatus(course: AdminCourse): void {
    const action = course.activo ? 'suspender' : 'reactivar';
    if (!confirm(`¿Deseas ${action} la materia "${course.nombre}"?`)) return;

    this.adminService.toggleCourseStatus(course.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          course.activo = res.activo;
          this.showToast(res.message, 'info');
          this.loadMetrics();
        },
        error: (err) => {
          alert('Error al cambiar estado del curso: ' + (err?.error?.message || err.message));
        }
      });
  }

  // --- Funcionalidades de Respaldos (Backup BD) ---
  public downloadDatabaseBackup(): void {
    if (this.isDownloadingBackup) return;

    this.isDownloadingBackup = true;
    this.showToast('Generando dump SQL de la base de datos Supabase... Por favor espera.', 'info');

    this.adminService.downloadDatabaseBackup()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (blob: Blob) => {
          this.isDownloadingBackup = false;
          const now = new Date();
          const timestamp = now.toISOString().replace(/[:.]/g, '-').slice(0, 19);
          const fileName = `backup_lms_supabase_${timestamp}.sql`;

          const blobUrl = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = blobUrl;
          link.download = fileName;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          window.URL.revokeObjectURL(blobUrl);

          this.showToast('Copia de seguridad SQL descargada exitosamente 💾', 'success');
        },
        error: (err) => {
          this.isDownloadingBackup = false;
          console.error('Error al descargar copia de seguridad:', err);
          this.showToast('No fue posible generar el backup de la base de datos. Verifica la conexión con Supabase.', 'warning');
        }
      });
  }

  public triggerRestore(): void {
    if (this.isRestoringBackup) return;
    try {
      if (this.fileInput?.nativeElement) {
        this.fileInput.nativeElement.value = '';
        this.fileInput.nativeElement.click();
        return;
      }
    } catch (e) {
      console.warn('fileInput nativeElement no disponible:', e);
    }

    // Selector dinámico infalible que no depende de ViewChild
    const fileSelector = document.createElement('input');
    fileSelector.type = 'file';
    fileSelector.accept = '.sql';
    fileSelector.style.display = 'none';
    fileSelector.onchange = (e) => this.onFileSelected(e);
    document.body.appendChild(fileSelector);
    fileSelector.click();
    setTimeout(() => {
      if (document.body.contains(fileSelector)) {
        document.body.removeChild(fileSelector);
      }
    }, 2000);
  }

  public onFileSelected(event: Event): void {
    const target = event.target as HTMLInputElement;
    if (!target || !target.files || target.files.length === 0) return;

    const file = target.files[0];
    if (!file) return;

    if (!file.name.toLowerCase().endsWith('.sql')) {
      alert('Por favor selecciona un archivo con extensión .sql válido.');
      target.value = '';
      return;
    }

    if (file.size === 0) {
      alert('El archivo seleccionado está vacío.');
      target.value = '';
      return;
    }

    const sizeKb = (file.size / 1024).toFixed(1);
    const confirmAction = confirm(
      `⚠️ ¡ATENCIÓN! Se restaurará la base de datos de PostgreSQL en Supabase con la información del archivo:\n\n📄 "${file.name}" (${sizeKb} KB)\n\n¿Deseas continuar?`
    );

    if (!confirmAction) {
      target.value = '';
      return;
    }

    this.isRestoringBackup = true;
    // Bloquear recarga del Service Worker durante la operación crítica
    (window as any).__swReloadBlocked = true;
    this.showToast('Subiendo y ejecutando script de restauración en Supabase PostgreSQL... Por favor espera.', 'info');

    this.adminService.restoreBackup(file)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res: { message?: string; fileName?: string; restoredAt?: string }) => {
          this.isRestoringBackup = false;
          (window as any).__swReloadBlocked = false;
          this.showToast(res?.message || '✅ Base de datos restaurada exitosamente.', 'success');
          target.value = '';
          // Recargar datos directamente en memoria sin recargar la página del navegador (evita 404 en SPA)
          this.loadMetrics();
          this.loadUsers();
          this.loadAdminCourses();
        },
        error: (err: any) => {
          this.isRestoringBackup = false;
          (window as any).__swReloadBlocked = false;
          console.error('Error al restaurar backup:', err);
          const errorMsg = err?.error?.message || err?.error?.detail || err?.message || 'Error al comunicarse con el servidor.';
          this.showToast(`❌ Error al restaurar: ${errorMsg}`, 'warning');
          alert(`❌ No fue posible restaurar la base de datos:\n\n${errorMsg}`);
          target.value = '';
        }
      });
  }

  // --- Toasts, Reportes y Navegación ---
  public showToast(message: string, type: 'success' | 'info' | 'warning'): void {
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
    }
    this.toastMessage = message;
    this.toastType = type;
    this.toastTimeoutId = setTimeout(() => {
      this.toastMessage = null;
      this.toastTimeoutId = null;
    }, 4500);
  }

  public goToTeacherView(): void {
    this.router.navigate(['/dashboard/teacher']);
  }

  public exportAdminReport(format: 'csv' | 'pdf'): void {
    const authorName = this.authService.currentUser()?.fullName || 'Super Administrador';
    const today = new Date().toISOString().slice(0, 10);

    if (this.activeSection === 'overview') {
      const headers = ['ID', 'Usuario / Documento', 'Nombre Completo', 'Rol Institucional', 'Grado', 'Estado', 'Fecha Registro'];
      const rows = this.filteredUsers.map(u => [
        u.id,
        u.username,
        u.fullName,
        u.role === 'Admin' ? 'Administrador' : u.role === 'Teacher' ? 'Docente' : 'Estudiante',
        u.gradeLevel || '-',
        u.activo ? 'Activo' : 'Suspendido',
        u.fechaCreacion ? new Date(u.fechaCreacion).toLocaleDateString('es-CO') : '-'
      ]);

      if (format === 'csv') {
        this.reportService.exportToCsv(`reporte_directorio_usuarios_admin_${today}`, headers, rows);
        this.showToast('Directorio de usuarios exportado en CSV exitosamente 📊', 'success');
      } else {
        this.reportService.exportToPdf({
          title: 'Reporte Directivo: Directorio Global de Usuarios',
          subtitle: `Filtro Rol: ${this.roleFilter} • Estado: ${this.userStatusFilter} • Total Registros: ${this.filteredUsers.length}`,
          author: authorName,
          institution: 'LMS Primaria • Panel Institucional Administrativo',
          headers,
          rows,
          filename: `reporte_directorio_usuarios_admin_${today}`,
          summaryCards: [
            { label: 'Usuarios Activos', value: this.metrics.totalActiveUsers, color: 'blue' },
            { label: 'Docentes', value: this.metrics.activeTeachersCount, color: 'green' },
            { label: 'Alumnos', value: this.metrics.activeStudentsCount, color: 'purple' },
            { label: 'Estado Sistema', value: this.metrics.systemStatus, color: 'yellow' }
          ]
        });
        this.showToast('Reporte de usuarios en PDF generado exitosamente 📄', 'success');
      }
    } else if (this.activeSection === 'courses') {
      const headers = ['ID', 'Nombre Materia', 'Grado', 'Grupo', 'Docente Titular', 'Total Alumnos', 'Estado'];
      const rows = this.filteredCourses.map(c => [
        c.id,
        c.nombre,
        c.grado,
        c.grupo,
        c.docenteNombre,
        c.totalStudents,
        c.activo ? 'Activa' : 'Inactiva'
      ]);

      if (format === 'csv') {
        this.reportService.exportToCsv(`reporte_materias_admin_${today}`, headers, rows);
        this.showToast('Reporte de materias en CSV descargado exitosamente 📊', 'success');
      } else {
        this.reportService.exportToPdf({
          title: 'Reporte Directivo: Directorio de Materias y Asignaturas',
          subtitle: `Filtro Grado: ${this.courseGradeFilter} • Total Registros: ${this.filteredCourses.length}`,
          author: authorName,
          institution: 'LMS Primaria • Panel Institucional Administrativo',
          headers,
          rows,
          filename: `reporte_materias_admin_${today}`,
          summaryCards: [
            { label: 'Total Materias', value: this.filteredCourses.length, color: 'blue' },
            { label: 'Alumnos Activos', value: this.metrics.activeStudentsCount, color: 'green' },
            { label: 'Docentes Activos', value: this.metrics.activeTeachersCount, color: 'purple' },
            { label: 'Estado Kestrel', value: this.metrics.systemStatus, color: 'yellow' }
          ]
        });
        this.showToast('Reporte de materias en PDF generado exitosamente 📄', 'success');
      }
    } else if (this.activeSection === 'inspector') {
      const headers = ['Componente / Parámetro', 'Estado / Valor', 'Descripción Técnica'];
      const rows = [
        ['Estado General del Servidor', this.metrics.systemStatus, 'Kestrel Web Server (.NET 8) y PostgreSQL (Supabase)'],
        ['Total Usuarios Habilitados', this.metrics.totalActiveUsers, 'Cuentas con acceso activo a la plataforma'],
        ['Docentes Registrados', this.metrics.activeTeachersCount, 'Profesores titulares de materias'],
        ['Estudiantes Matriculados', this.metrics.activeStudentsCount, 'Alumnos de 1° a 6° de básica primaria'],
        ['Cursos y Materias Creadas', this.metrics.totalCoursesCount, 'Asignaturas operativas en el ciclo lectivo'],
        ['Alertas y Errores (24h)', this.metrics.recentErrorsCount, 'Registros en log de auditoría']
      ];

      if (format === 'csv') {
        this.reportService.exportToCsv(`reporte_auditoria_sistema_${today}`, headers, rows);
        this.showToast('Reporte de auditoría en CSV descargado exitosamente 📊', 'success');
      } else {
        this.reportService.exportToPdf({
          title: 'Reporte de Auditoría e Infraestructura de Servidor',
          subtitle: `Generado para auditoría directiva de plataforma LMS Primaria`,
          author: authorName,
          institution: 'LMS Primaria • Centro de Auditoría y Seguridad',
          headers,
          rows,
          filename: `reporte_auditoria_sistema_${today}`,
          summaryCards: [
            { label: 'Estado', value: this.metrics.systemStatus, color: 'green' },
            { label: 'Usuarios', value: this.metrics.totalActiveUsers, color: 'blue' },
            { label: 'Cursos', value: this.metrics.totalCoursesCount, color: 'purple' },
            { label: 'Alertas 24h', value: this.metrics.recentErrorsCount, color: 'yellow' }
          ]
        });
        this.showToast('Reporte de auditoría en PDF generado exitosamente 📄', 'success');
      }
    }
  }

  public logout(): void {
    this.authService.logout();
  }
}