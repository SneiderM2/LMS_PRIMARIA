import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TeacherService } from '../../core/services/teacher.service';
import { AuthService } from '../../core/services/auth.service';
import { StudentStatus, SemaforoSummaryMetrics } from '../../core/models/semaforo.model';
import { CreateContent } from '../../core/models/content.model';
import { Course, TeacherStudent } from '../../core/models/user.model';


@Component({
  selector: 'app-teacher-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './teacher-dashboard.component.html',
  styleUrls: ['./teacher-dashboard.component.css']
})
export class TeacherDashboardComponent implements OnInit {
  // Pestaña activa: 'semaforo' o 'students'
  public activeTab: 'semaforo' | 'students' = 'semaforo';

  // --- Módulo Semáforo ---
  public students: StudentStatus[] = [];
  public filteredStudents: StudentStatus[] = [];
  public metrics: SemaforoSummaryMetrics = {
    totalStudents: 0,
    greenCount: 0,
    yellowCount: 0,
    redCount: 0
  };

  public isLoading = true;
  public selectedGrade = '';
  public selectedStatusFilter: 'All' | 'Green' | 'Yellow' | 'Red' = 'All';
  public viewMode: 'cards' | 'table' = 'cards';

  // Modal para nueva tarea/recurso
  public showCreateModal = false;
  public isSavingContent = false;
  public newContent: CreateContent = {
    title: '',
    description: '',
    type: 'Assignment',
    subject: 'Matemáticas',
    gradeLevel: '1°',
    fileUrl: '',
    dueDate: ''
  };

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

  // Estado de matrícula
  public enrollingMap: { [key: number]: boolean } = {};
  public enrollNotification: { message: string; type: 'success' | 'info' | 'error' } | null = null;

  constructor(
    public authService: AuthService,
    private teacherService: TeacherService
  ) {}

  ngOnInit(): void {
    this.loadData();
    this.loadCourses();
    this.loadAllStudents();
  }

  public switchTab(tab: 'semaforo' | 'students'): void {
    this.activeTab = tab;
    if (tab === 'students') {
      this.loadAllStudents();
      this.loadCourses();
    } else {
      this.loadData();
    }
  }

  // --- Semáforo Data ---
  public loadData(): void {
    this.isLoading = true;

    this.teacherService.getStudentsStatus(this.selectedGrade).subscribe({
      next: (students) => {
        this.students = students;
        this.applyFilter();
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });

    this.teacherService.getMetrics(this.selectedGrade).subscribe({
      next: (metrics) => {
        this.metrics = metrics;
      }
    });
  }

  public applyFilter(): void {
    let list = [...this.students];
    if (this.selectedStatusFilter !== 'All') {
      list = list.filter(s => s.semaforoColor === this.selectedStatusFilter);
    }
    this.filteredStudents = list;
  }

  public setStatusFilter(filter: 'All' | 'Green' | 'Yellow' | 'Red'): void {
    this.selectedStatusFilter = filter;
    this.applyFilter();
  }

  // --- Tareas y Contenidos ---
  public openCreateModal(): void {
    this.newContent = {
      title: '',
      description: '',
      type: 'Assignment',
      subject: this.courses.length > 0 ? this.courses[0].nombre : 'Matemáticas',
      gradeLevel: '1°',
      fileUrl: '',
      dueDate: new Date(Date.now() + 5 * 86400000).toISOString().split('T')[0]
    };
    this.showCreateModal = true;
  }

  public closeCreateModal(): void {
    this.showCreateModal = false;
  }

  public saveContent(): void {
    if (!this.newContent.title.trim()) return;

    this.isSavingContent = true;
    this.teacherService.createContent(this.newContent).subscribe({
      next: () => {
        this.isSavingContent = false;
        this.showCreateModal = false;
        alert('¡Actividad publicada exitosamente! 🎉');
        this.loadData();
      },
      error: () => {
        this.isSavingContent = false;
        alert('Ocurrió un error al guardar la actividad.');
      }
    });
  }

  // --- Gestión de Cursos ---
  public loadCourses(): void {
    this.teacherService.getCourses().subscribe({
      next: (courses) => {
        this.courses = courses;
        if (!this.selectedCourseId && courses.length > 0) {
          this.selectedCourseId = courses[0].id;
        }
      }
    });
  }

  public openCreateCourseModal(): void {
    this.newCourse = {
      nombre: '',
      grado: '1°',
      grupo: 'A',
      descripcion: ''
    };
    this.showCreateCourseModal = true;
  }

  public closeCreateCourseModal(): void {
    this.showCreateCourseModal = false;
  }

  public saveCourse(): void {
    if (!this.newCourse.nombre.trim()) return;

    this.isCreatingCourse = true;
    this.teacherService.createCourse(this.newCourse).subscribe({
      next: (created) => {
        this.isCreatingCourse = false;
        this.showCreateCourseModal = false;
        this.courses.push(created);
        this.selectedCourseId = created.id;
        this.showToast(`¡Curso "${created.nombre}" creado exitosamente! 📚`, 'success');
      },
      error: (err) => {
        this.isCreatingCourse = false;
        alert(err?.error?.message || 'Error al crear el curso.');
      }
    });
  }

  // --- Gestión de Alumnos ---
  public loadAllStudents(): void {
    this.isLoadingStudents = true;
    this.teacherService.getAllStudents(this.studentGradeFilter).subscribe({
      next: (students) => {
        this.allStudents = students;
        this.applyStudentFilters();
        this.isLoadingStudents = false;
      },
      error: () => {
        this.isLoadingStudents = false;
      }
    });
  }

  public applyStudentFilters(): void {
    let list = [...this.allStudents];
    if (this.studentSearch.trim()) {
      const q = this.studentSearch.toLowerCase();
      list = list.filter(s =>
        s.fullName.toLowerCase().includes(q) ||
        s.username.toLowerCase().includes(q)
      );
    }
    this.filteredAllStudents = list;
  }

  public openCreateStudentModal(): void {
    this.newStudent = {
      username: '',
      fullName: '',
      password: '',
      grade: '1°'
    };
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

    this.isCreatingStudent = true;
    this.createStudentError = null;

    this.teacherService.createStudent({
      username: this.newStudent.username.trim(),
      fullName: this.newStudent.fullName.trim(),
      password: this.newStudent.password.trim() || '123456',
      grade: this.newStudent.grade
    }).subscribe({
      next: (created) => {
        this.isCreatingStudent = false;
        this.showCreateStudentModal = false;
        this.showToast(`¡Alumno ${created.fullName} registrado con éxito! 🎓`, 'success');
        this.loadAllStudents();
        this.loadData();
      },
      error: (err) => {
        this.isCreatingStudent = false;
        this.createStudentError = err?.error?.message || 'Error al registrar al estudiante.';
      }
    });
  }

  // --- Matrícula a Cursos ---
  public isStudentEnrolledInCourse(student: TeacherStudent, courseId: number | null): boolean {
    if (!courseId) return false;
    return student.enrolledCourses.some(c => c.courseId === courseId);
  }

  public enrollStudent(student: TeacherStudent): void {
    if (!this.selectedCourseId) {
      alert('Por favor selecciona o crea primero un curso para matricular al estudiante.');
      return;
    }

    const courseId = this.selectedCourseId;
    this.enrollingMap[student.id] = true;

    this.teacherService.enrollStudent(courseId, student.id).subscribe({
      next: (res) => {
        this.enrollingMap[student.id] = false;
        const currentCourse = this.courses.find(c => c.id === courseId);
        const courseName = currentCourse ? currentCourse.nombre : 'el curso';

        if (res.yaMatriculado) {
          this.showToast(`${student.fullName} ya estaba matriculado(a) en ${courseName}.`, 'info');
        } else {
          this.showToast(`¡${student.fullName} fue matriculado(a) exitosamente en ${courseName}! 🎉`, 'success');
          // Actualizar lista local de inscripciones del estudiante
          student.enrolledCourses.push({
            courseId: courseId,
            courseName: courseName,
            group: currentCourse?.grupo || 'A',
            status: 'ACTIVA'
          });
          if (currentCourse) {
            currentCourse.totalStudents++;
          }
        }
      },
      error: (err) => {
        this.enrollingMap[student.id] = false;
        alert(err?.error?.message || 'Error al matricular al estudiante.');
      }
    });
  }

  public showToast(message: string, type: 'success' | 'info' | 'error'): void {
    this.enrollNotification = { message, type };
    setTimeout(() => {
      this.enrollNotification = null;
    }, 4500);
  }

  public logout(): void {
    this.authService.logout();
  }
}
