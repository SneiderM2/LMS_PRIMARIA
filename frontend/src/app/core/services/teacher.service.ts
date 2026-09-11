import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StudentStatus, SemaforoSummaryMetrics } from '../models/semaforo.model';
import { Content, CreateContent, StudentSubmission } from '../models/content.model';
import { Course, TeacherStudent } from '../models/user.model';

@Injectable({
  providedIn: 'root'
})
export class TeacherService {
  private readonly API_URL = 'http://localhost:5000/api/teacher';

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

  public enrollStudent(courseId: number, studentId: string | number): Observable<{ message: string; yaMatriculado?: boolean; studentName?: string; courseName?: string }> {
    return this.http.post<{ message: string; yaMatriculado?: boolean; studentName?: string; courseName?: string }>(
      `${this.API_URL}/courses/${courseId}/enroll`,
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

  public getAssignmentSubmissions(assignmentId: string): Observable<StudentSubmission[]> {
    return this.http.get<StudentSubmission[]>(`${this.API_URL}/submissions/${assignmentId}`);
  }
}
