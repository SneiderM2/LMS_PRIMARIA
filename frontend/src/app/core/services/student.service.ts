import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Content, UploadResult } from '../models/content.model';

export interface StudentMetrics {
  enrolledCoursesCount: number;
  cumulativeGpa: number;
  pendingActivitiesCount: number;
  progressPercentage: number;
}

export interface StudentCourseItem {
  id: number;
  nombre: string;
  grado: string;
  grupo: string;
  docenteNombre: string;
  descripcion?: string;
  isEnrolled: boolean;
  enrollmentStatus: string;
  fechaInscripcion?: string;
}

export interface StudentCoursesCatalog {
  myActiveCourses: StudentCourseItem[];
  availableCourses: StudentCourseItem[];
}

import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class StudentService {
  private readonly API_URL = `${environment.apiUrl}/student`;

  constructor(private http: HttpClient) {}

  public getDashboard(): Observable<Content[]> {
    return this.http.get<Content[]>(`${this.API_URL}/dashboard`);
  }

  public getMetrics(): Observable<StudentMetrics> {
    return this.http.get<StudentMetrics>(`${this.API_URL}/metrics`);
  }

  public getCourses(): Observable<StudentCoursesCatalog> {
    return this.http.get<StudentCoursesCatalog>(`${this.API_URL}/courses`);
  }

  public enrollCourse(courseId: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.API_URL}/courses/${courseId}/enroll`, {});
  }

  public withdrawCourse(courseId: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.API_URL}/courses/${courseId}/withdraw`, {});
  }

  public uploadAssignment(assignmentId: string, file: File): Observable<UploadResult> {
    const formData = new FormData();
    formData.append('assignmentId', assignmentId);
    formData.append('file', file);

    return this.http.post<UploadResult>(`${this.API_URL}/upload-assignment`, formData);
  }
}
