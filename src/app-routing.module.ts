// app-routing.module.ts
const routes: Routes = [
  {
    path: '',
    redirectTo: '/auth/login',
    pathMatch: 'full'
  },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.module').then(m => m.AuthModule)
  },
  {
    path: 'admin',
    component: AdminLayoutComponent,
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['Admin'] },
    children: [
      {
        path: 'dashboard',
        loadChildren: () => import('./features/admin/dashboard/dashboard.module').then(m => m.DashboardModule)
      },
      {
        path: 'students',
        loadChildren: () => import('./features/admin/students/students.module').then(m => m.StudentsModule)
      },
      {
        path: 'teachers',
        loadChildren: () => import('./features/admin/teachers/teachers.module').then(m => m.TeachersModule)
      },
      {
        path: 'parents',
        loadChildren: () => import('./features/admin/parents/parents.module').then(m => m.ParentsModule)
      },
      {
        path: 'classrooms',
        loadChildren: () => import('./features/admin/classrooms/classrooms.module').then(m => m.ClassroomsModule)
      },
      {
        path: 'subjects',
        loadChildren: () => import('./features/admin/subjects/subjects.module').then(m => m.SubjectsModule)
      },
      {
        path: 'reports',
        loadChildren: () => import('./features/admin/reports/reports.module').then(m => m.ReportsModule)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  },
  {
    path: 'teacher',
    component: TeacherLayoutComponent,
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['Teacher'] },
    children: [
      {
        path: 'dashboard',
        loadChildren: () => import('./features/teacher/dashboard/dashboard.module').then(m => m.DashboardModule)
      },
      {
        path: 'attendance',
        loadChildren: () => import('./features/teacher/attendance/attendance.module').then(m => m.AttendanceModule)
      },
      {
        path: 'exams',
        loadChildren: () => import('./features/teacher/exams/exams.module').then(m => m.ExamsModule)
      },
      {
        path: 'grades',
        loadChildren: () => import('./features/teacher/grades/grades.module').then(m => m.GradesModule)
      },
      {
        path: 'students',
        loadChildren: () => import('./features/teacher/students/students.module').then(m => m.StudentsModule)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  },
  {
    path: 'student',
    component: StudentLayoutComponent,
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['Student'] },
    children: [
      {
        path: 'dashboard',
        loadChildren: () => import('./features/student/dashboard/dashboard.module').then(m => m.DashboardModule)
      },
      {
        path: 'scan',
        loadChildren: () => import('./features/student/scan/scan.module').then(m => m.ScanModule)
      },
      {
        path: 'exams',
        loadChildren: () => import('./features/student/exams/exams.module').then(m => m.ExamsModule)
      },
      {
        path: 'grades',
        loadChildren: () => import('./features/student/grades/grades.module').then(m => m.GradesModule)
      },
      {
        path: 'schedule',
        loadChildren: () => import('./features/student/schedule/schedule.module').then(m => m.ScheduleModule)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  },
  {
    path: 'parent',
    component: ParentLayoutComponent,
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['Parent'] },
    children: [
      {
        path: 'dashboard',
        loadChildren: () => import('./features/parent/dashboard/dashboard.module').then(m => m.DashboardModule)
      },
      {
        path: 'children',
        loadChildren: () => import('./features/parent/children/children.module').then(m => m.ChildrenModule)
      },
      {
        path: 'notifications',
        loadChildren: () => import('./features/parent/notifications/notifications.module').then(m => m.NotificationsModule)
      },
      {
        path: 'payments',
        loadChildren: () => import('./features/parent/payments/payments.module').then(m => m.PaymentsModule)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  },
  {
    path: 'chat',
    component: ChatLayoutComponent,
    canActivate: [AuthGuard],
    loadChildren: () => import('./features/chat/chat.module').then(m => m.ChatModule)
  },
  {
    path: 'notifications',
    component: NotificationsLayoutComponent,
    canActivate: [AuthGuard],
    loadChildren: () => import('./features/notifications/notifications.module').then(m => m.NotificationsModule)
  },
  {
    path: 'profile',
    loadChildren: () => import('./features/profile/profile.module').then(m => m.ProfileModule)
  },
  {
    path: '**',
    component: NotFoundComponent
  }
];