import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent),
    title: 'Iniciar sesión · SciKids'
  },
  {
    path: 'dashboard/teacher',
    loadComponent: () => import('./features/teacher/teacher-dashboard.component').then(m => m.TeacherDashboardComponent),
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Teacher', 'Admin'] },
    title: 'Panel Docente · SciKids'
  },
  {
    path: 'dashboard/student',
    loadComponent: () => import('./features/student/student-dashboard.component').then(m => m.StudentDashboardComponent),
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Student'] },
    title: 'Mis Misiones · SciKids'
  },
  {
    path: 'dashboard/admin',
    loadComponent: () => import('./features/admin/admin-dashboard.component').then(m => m.AdminDashboardComponent),
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'] },
    title: 'Panel Administrativo · SciKids'
  },
  {
    path: '',
    redirectTo: '/login',
    pathMatch: 'full'
  },
  {
    path: '**',
    redirectTo: '/login'
  }
];
