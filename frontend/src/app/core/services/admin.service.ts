import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AdminMetrics {
  totalActiveUsers: number;
  activeTeachersCount: number;
  activeStudentsCount: number;
  systemStatus: string;
  recentErrorsCount: number;
  totalCoursesCount: number;
}

export interface AdminUser {
  id: number;
  username: string;
  fullName: string;
  nombre: string;
  apellido: string;
  role: 'Student' | 'Teacher' | 'Admin';
  gradeLevel?: string;
  activo: boolean;
  avatarUrl?: string;
  fechaCreacion: string;
}

export interface CreateAdminUserDto {
  username: string;
  password?: string;
  nombre: string;
  apellido: string;
  role: 'Student' | 'Teacher' | 'Admin';
  gradeLevel?: string;
}

export interface UpdateAdminUserDto {
  nombre: string;
  apellido: string;
  role?: string;
  gradeLevel?: string;
  password?: string;
}

export interface AdminCourse {
  id: number;
  nombre: string;
  grado: string;
  grupo: string;
  docenteId: number;
  docenteNombre: string;
  descripcion?: string;
  totalStudents: number;
  fechaCreacion: string;
  activo: boolean;
}

export interface CreateAdminCourseDto {
  nombre: string;
  grado: string;
  grupo: string;
  docenteId?: number;
  descripcion?: string;
}

export interface UpdateAdminCourseDto {
  nombre: string;
  grado: string;
  grupo: string;
  docenteId?: number;
  descripcion?: string;
}

import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private readonly API_URL = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient) {}

  public getMetrics(): Observable<AdminMetrics> {
    return this.http.get<AdminMetrics>(`${this.API_URL}/metrics`);
  }

  public getUsers(role?: string, search?: string): Observable<AdminUser[]> {
    let params = new HttpParams();
    if (role && role !== 'ALL') params = params.set('role', role);
    if (search && search.trim()) params = params.set('search', search.trim());

    return this.http.get<AdminUser[]>(`${this.API_URL}/users`, { params });
  }

  public createUser(dto: CreateAdminUserDto): Observable<AdminUser> {
    return this.http.post<AdminUser>(`${this.API_URL}/users`, dto);
  }

  public updateUser(id: number, dto: UpdateAdminUserDto): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.API_URL}/users/${id}`, dto);
  }

  public toggleUserStatus(id: number): Observable<{ message: string; activo: boolean }> {
    return this.http.patch<{ message: string; activo: boolean }>(`${this.API_URL}/users/${id}/toggle-status`, {});
  }

  // --- CRUD Cursos y Materias (Admin) ---
  public getCourses(): Observable<AdminCourse[]> {
    return this.http.get<AdminCourse[]>(`${this.API_URL}/courses`);
  }

  public createCourse(dto: CreateAdminCourseDto): Observable<AdminCourse> {
    return this.http.post<AdminCourse>(`${this.API_URL}/courses`, dto);
  }

  public updateCourse(id: number, dto: UpdateAdminCourseDto): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.API_URL}/courses/${id}`, dto);
  }

  public toggleCourseStatus(id: number): Observable<{ message: string; activo: boolean }> {
    return this.http.patch<{ message: string; activo: boolean }>(`${this.API_URL}/courses/${id}/toggle-status`, {});
  }

  /**
   * Solicita el respaldo SQL completo de la base de datos Supabase como Blob binario.
   */
  public downloadDatabaseBackup(): Observable<Blob> {
    return this.http.get(`${this.API_URL}/backup-database`, {
      responseType: 'blob'
    });
  }
}
