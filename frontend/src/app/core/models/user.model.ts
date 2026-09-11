export type UserRole = 'Admin' | 'Teacher' | 'Student' | '.admin';

export interface User {
  id: string;
  internalId?: number;
  fullName: string;
  role: UserRole;
  gradeLevel?: string;
  lastLoginDate?: string | Date;
  avatarUrl: string;
}

export interface LoginRequest {
  id: string;
  password: string;
}

export interface RegisterRequest {
  id: string;
  fullName: string;
  password: string;
  role: UserRole;
  grade?: string;
}

export interface LoginResponse {
  token: string;
  expiration: string;
  user: User;
}

export interface TeacherStudent {
  id: number;
  username: string;
  fullName: string;
  grade: string;
  avatarUrl: string;
  createdAt: string | Date;
  enrolledCourses: {
    courseId: number;
    courseName: string;
    group: string;
    status: string;
  }[];
}

export interface Course {
  id: number;
  nombre: string;
  grado: string;
  grupo: string;
  docenteId: number;
  docenteNombre: string;
  descripcion?: string;
  totalStudents: number;
  fechaCreacion: string | Date;
  activo: boolean;
}
