import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StudentStatus, SemaforoSummaryMetrics } from '../models/semaforo.model';
import { Content, CreateContent, StudentSubmission } from '../models/content.model';
import { Course, TeacherStudent } from '../models/user.model';

export interface TeacherMetrics {
  totalStudents: number;
  pendingGradingCount: number;
  activeTasksCount: number;
  submissionRate: number;
}

export interface TeacherActivity {
  id: number;
  kind: 'Tarea' | 'Material';
  courseId: number;
  courseName: string;
  gradeName: string;
  title: string;
  description?: string;
  createdAt: string;
  dueDate?: string;
  maxScore?: number;
  isActive: boolean;
  submissionsCount: number;
  pendingGradingCount: number;
  resourceUrl?: string;
  resourceType?: string;
}

export interface UpdateActivityDto {
  title: string;
  description?: string;
  dueDate?: string;
  maxScore?: number;
  resourceUrl?: string;
}

import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class TeacherService {
  private readonly API_URL = `${environment.apiUrl}/teacher`;

  constructor(private http: HttpClient) {}

  public getStudentsStatus(gradeLevel?: string): Observable<StudentStatus[]> {
    let params = new HttpParams();
    if (gradeLevel) {
      params = params.set('gradeLevel', gradeLevel);
    }
    return this.http.get<StudentStatus[]>(`${this.API_URL}/students-status`, { params });
  }

  public getMetrics(gradeLevel?: string): Observable<SemaforoSummaryMetrics> {
    let params = new HttpParams();
    if (gradeLevel) {
      params = params.set('gradeLevel', gradeLevel);
    }
    return this.http.get<SemaforoSummaryMetrics>(`${this.API_URL}/metrics`, { params });
  }

  // --- Métricas Académicas y Gestión de Actividades CRUD ---
  public getAcademicMetrics(): Observable<TeacherMetrics> {
    return this.http.get<TeacherMetrics>(`${this.API_URL}/academic-metrics`);
  }

  public getActivities(): Observable<TeacherActivity[]> {
    return this.http.get<TeacherActivity[]>(`${this.API_URL}/activities`);
  }

  public updateActivity(id: number, kind: string, dto: UpdateActivityDto): Observable<{ message: string }> {
    const params = new HttpParams().set('kind', kind);
    return this.http.put<{ message: string }>(`${this.API_URL}/activities/${id}`, dto, { params });
  }

  public toggleActivityStatus(id: number, kind: string): Observable<{ message: string; isActive: boolean }> {
    const params = new HttpParams().set('kind', kind);
    return this.http.patch<{ message: string; isActive: boolean }>(`${this.API_URL}/activities/${id}/toggle-status`, {}, { params });
  }

  // --- Gestión de Estudiantes y Matrícula ---
  public getAllStudents(grade?: string): Observable<TeacherStudent[]> {
    let params = new HttpParams();
    if (grade) {
      params = params.set('grade', grade);
    }
    return this.http.get<TeacherStudent[]>(`${this.API_URL}/students`, { params });
  }

  public createStudent(dto: { username: string; fullName: string; password?: string; grade: string }): Observable<TeacherStudent> {
    return this.http.post<TeacherStudent>(`${this.API_URL}/students`, dto);
  }

  public getCourses(): Observable<Course[]> {
    return this.http.get<Course[]>(`${this.API_URL}/courses`);
  }

  public createCourse(dto: { nombre: string; grado: string; grupo: string; descripcion?: string }): Observable<Course> {
    return this.http.post<Course>(`${this.API_URL}/courses`, dto);
  }

  public updateCourse(courseId: number, dto: { nombre: string; grado: string; grupo: string; descripcion?: string }): Observable<any> {
    return this.http.put<any>(`${this.API_URL}/courses/${courseId}`, dto);
  }

  public enrollStudent(courseId: number, studentId: string | number): Observable<{ message: string; yaMatriculado?: boolean; studentName?: string; courseName?: string }> {
    return this.http.post<{ message: string; yaMatriculado?: boolean; studentName?: string; courseName?: string }>(
      `${this.API_URL}/courses/${courseId}/enroll`,
      { studentId: studentId.toString() }
    );
  }

  public unenrollStudent(courseId: number, studentId: string | number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.API_URL}/courses/${courseId}/unenroll`,
      { studentId: studentId.toString() }
    );
  }

  // --- Contenidos y Tareas ---
  public createContent(dto: CreateContent): Observable<Content> {
    return this.http.post<Content>(`${this.API_URL}/contents`, dto);
  }

  public uploadResource(file: File): Observable<{ fileUrl: string; originalName: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ fileUrl: string; originalName: string }>(`${this.API_URL}/upload-resource`, formData);
  }

  public getAssignmentSubmissions(assignmentId: string | number): Observable<StudentSubmission[]> {
    return this.http.get<StudentSubmission[]>(`${this.API_URL}/submissions/${assignmentId}`);
  }

  public gradeSubmission(submissionId: number, grade: number, feedback?: string): Observable<{ message: string; submissionId: number; grade: number; feedback?: string; status: string }> {
    return this.http.post<{ message: string; submissionId: number; grade: number; feedback?: string; status: string }>(
      `${this.API_URL}/submissions/${submissionId}/grade`,
      { grade, feedback }
    );
  }
}
