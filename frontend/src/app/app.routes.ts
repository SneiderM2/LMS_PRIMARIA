import { Routes } from '@angular/router';
import { LoginComponent } from './features/auth/login/login.component';
import { TeacherDashboardComponent } from './features/teacher/teacher-dashboard.component';
import { StudentDashboardComponent } from './features/student/student-dashboard.component';
import { AdminDashboardComponent } from './features/admin/admin-dashboard.component';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent
  },
  {
    path: 'dashboard/teacher',
    component: TeacherDashboardComponent,
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Teacher', 'Admin'] }
  },
  {
    path: 'dashboard/student',
    component: StudentDashboardComponent,
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Student'] }
  },
  {
    path: 'dashboard/admin',
    component: AdminDashboardComponent,
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'] }
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
