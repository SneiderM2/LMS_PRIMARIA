import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { AdminService, AdminMetrics, AdminUser, CreateAdminUserDto, UpdateAdminUserDto, AdminCourse, CreateAdminCourseDto, UpdateAdminCourseDto } from '../../core/services/admin.service';
import { SystemInspectorComponent } from './system-inspector/system-inspector.component';
import { ReportExportService } from '../../core/services/report-export.service';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, SystemInspectorComponent],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.css']
})
export class AdminDashboardComponent implements OnInit {
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

  // Toast Flotante
  public toastMessage: string | null = null;
  public toastType: 'success' | 'info' | 'warning' = 'info';

  constructor(
    public authService: AuthService,
    private adminService: AdminService,
    private router: Router,
    private reportService: ReportExportService
  ) {}

  ngOnInit(): void {
    this.loadMetrics();
    this.loadUsers();
    this.loadAdminCourses();
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

  // --- Métricas Globales ---
  public loadMetrics(): void {
    this.isLoadingMetrics = true;
    this.adminService.getMetrics().subscribe({
      next: (m) => {
        this.metrics = m;
        this.isLoadingMetrics = false;
      },
      error: () => {
        this.isLoadingMetrics = false;
      }
    });
  }

  // --- Usuarios CRUD ---
  public loadUsers(): void {
    this.isLoadingUsers = true;
    this.adminService.getUsers(this.roleFilter, this.userSearchQuery).subscribe({
      next: (data) => {
        this.users = data;
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
    this.filteredUsers = list;
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

  public saveNewUser(): void {
    if (!this.newUser.username.trim() || !this.newUser.nombre.trim() || !this.newUser.apellido.trim()) {
      this.createUserError = 'Documento, Nombre y Apellido son obligatorios.';
      return;
    }

    this.isCreatingUser = true;
    this.createUserError = null;

    this.adminService.createUser(this.newUser).subscribe({
      next: (created) => {
        this.isCreatingUser = false;
        this.closeCreateUserModal();
        this.showToast(`Usuario ${created.fullName} registrado correctamente. 🎉`, 'success');
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

    this.isEditingUser = true;
    this.adminService.updateUser(this.selectedUserToEdit.id, this.editUserForm).subscribe({
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

    this.adminService.toggleUserStatus(user.id).subscribe({
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
    this.adminService.getCourses().subscribe({
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

    this.adminService.createCourse(this.newCourse).subscribe({
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

    this.adminService.updateCourse(this.selectedCourseToEdit.id, this.editCourseForm).subscribe({
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

    this.adminService.toggleCourseStatus(course.id).subscribe({
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

  // --- Toasts y Navegación ---
  public showToast(message: string, type: 'success' | 'info' | 'warning'): void {
    this.toastMessage = message;
    this.toastType = type;
    setTimeout(() => {
      this.toastMessage = null;
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
      const headers = ['ID', 'Nombre de Materia / Curso', 'Grado', 'Grupo', 'Docente Responsable', 'Alumnos Inscritos', 'Estado', 'Descripción'];
      const rows = this.filteredCourses.map(c => [
        c.id,
        c.nombre,
        c.grado,
        c.grupo,
        c.docenteNombre || 'Sin profesor asignado',
        c.totalStudents || 0,
        c.activo ? 'Habilitada' : 'Suspendida',
        c.descripcion || 'Sin descripción'
      ]);

      if (format === 'csv') {
        this.reportService.exportToCsv(`reporte_materias_institucionales_${today}`, headers, rows);
        this.showToast('Catálogo de materias exportado en CSV exitosamente 📊', 'success');
      } else {
        this.reportService.exportToPdf({
          title: 'Reporte Directivo: Asignaturas y Cursos Escolares',
          subtitle: `Filtro Grado: ${this.courseGradeFilter} • Total de materias listadas: ${this.filteredCourses.length}`,
          author: authorName,
          institution: 'LMS Primaria • Control de Cursos Institucionales',
          headers,
          rows,
          filename: `reporte_materias_institucionales_${today}`,
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
        ['Estado General del Servidor', this.metrics.systemStatus, 'Kestrel Web Server (.NET 8) y MySQL 8.0'],
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
