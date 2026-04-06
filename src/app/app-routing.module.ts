import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

// Guards
import { AuthGuard } from './core/guards/auth.guard';
import { NoAuthGuard } from './core/guards/no-auth.guard';
import { RoleGuard } from './core/guards/role.guard';

const routes: Routes = [
    {
        path: '',
        redirectTo: '/auth/login',
        pathMatch: 'full'
    },
    {
        path: 'auth',
        canActivate: [NoAuthGuard],
        children: [
            {
                path: 'login',
                loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
            },
            {
                path: 'forgot-password',
                loadComponent: () => import('./features/auth/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent)
            }
        ]
    },
    {
        path: 'support',
        loadComponent: () => import('./features/support/support-center/support-center.component').then(m => m.SupportCenterComponent)
    },
    {
        path: 'admin',
        canActivate: [AuthGuard, RoleGuard],
        data: { roles: ['Admin'] },
        children: [
            {
                path: 'dashboard',
                loadComponent: () => import('./features/admin/dashboard/dashboard.component').then(m => m.DashboardComponent)
            },
            {
                path: 'students',
                children: [
                    {
                        path: '',
                        loadComponent: () => import('./features/admin/students/student-management/student-management.component').then(m => m.StudentManagementComponent)
                    },
                    {
                        path: 'train-face/:id',
                        loadComponent: () => import('./features/admin/students/train-face/train-face.component').then(m => m.TrainFaceComponent)
                    }
                ]
            },
            {
                path: 'parents',
                loadComponent: () => import('./features/admin/parents/parent-management/parent-management.component').then(m => m.ParentManagementComponent)
            },
            {
                path: 'payments',
                loadComponent: () => import('./features/admin/payments/payment-management/payment-management.component').then(m => m.PaymentManagementComponent)
            },
            {
                path: 'teachers',
                loadComponent: () => import('./features/admin/teachers/teacher-management/teacher-management.component').then(m => m.TeacherManagementComponent)
            },
            {
                path: 'grades',
                loadComponent: () => import('./features/admin/grades/grade-management/grade-management.component').then(m => m.GradeManagementComponent)
            },
            {
                path: 'classes',
                loadComponent: () => import('./features/admin/classes/class-management/class-management.component').then(m => m.ClassManagementComponent)
            },
            {
                path: 'subjects',
                loadComponent: () => import('./features/admin/subjects/subject-management/subject-management.component').then(m => m.SubjectManagementComponent)
            },
            {
                path: 'reports',
                loadComponent: () => import('./features/admin/reports/reports-management/reports-management.component').then(m => m.ReportsManagementComponent)
            }
        ]
    },
    {
        path: 'teacher',
        canActivate: [AuthGuard, RoleGuard],
        data: { roles: ['Teacher'] },
        children: [
            {
                path: 'dashboard',
                loadComponent: () => import('./features/teacher/dashboard/dashboard.component').then(m => m.DashboardComponent)
            },
            {
                path: 'attendance',
                children: [
                    {
                        path: '',
                        loadComponent: () => import('./features/teacher/attendance/attendance-sessions/attendance-sessions.component').then(m => m.AttendanceSessionsComponent)
                    },
                    {
                        path: 'qr',
                        loadComponent: () => import('./features/teacher/attendance/qr-attendance/qr-attendance.component').then(m => m.QrAttendanceComponent)
                    },
                    {
                        path: 'face',
                        loadComponent: () => import('./features/attendance/face-attendance/face-attendance.component').then(m => m.FaceAttendanceComponent)
                    },
                    {
                        path: 'manual',
                        loadComponent: () => import('./features/attendance/manual-attendance/manual-attendance.component').then(m => m.ManualAttendanceComponent)
                    }
                ]
            },
            {
                path: 'classes',
                loadComponent: () => import('./features/teacher/timetable/teacher-timetable/teacher-timetable.component').then(m => m.TeacherTimetableComponent)
            },
            {
                path: 'timetable',
                loadComponent: () => import('./features/teacher/timetable/teacher-timetable/teacher-timetable.component').then(m => m.TeacherTimetableComponent)
            },
            {
                path: 'exams',
                children: [
                    {
                        path: '',
                        loadComponent: () => import('./features/teacher/exams/exam-list/exam-list.component').then(m => m.ExamListComponent)
                    },
                    {
                        path: 'create',
                        loadComponent: () => import('./features/teacher/exams/create-exam/create-exam.component').then(m => m.CreateExamComponent)
                    },
                    {
                        path: 'edit/:id',
                        loadComponent: () => import('./features/teacher/exams/create-exam/create-exam.component').then(m => m.CreateExamComponent)
                    },
                    {
                        path: 'results/:id',
                        loadComponent: () => import('./features/teacher/exams/exam-results/exam-results.component').then(m => m.ExamResultsComponent)
                    }
                ]
            },
            {
                path: 'assignments',
                loadComponent: () => import('./features/teacher/assignments/teacher-assignments/teacher-assignments.component').then(m => m.TeacherAssignmentsComponent)
            },
            {
                path: 'videos',
                loadComponent: () => import('./features/teacher/videos/videos.component').then(m => m.VideosComponent)
            }
        ]
    },
    {
        path: 'student',
        canActivate: [AuthGuard, RoleGuard],
        data: { roles: ['Student'] },
        children: [
            {
                path: 'dashboard',
                loadComponent: () => import('./features/student/dashboard/dashboard.component').then(m => m.DashboardComponent)
            },
            {
                path: 'timetable',
                loadComponent: () => import('./features/student/timetable/student-timetable/student-timetable.component').then(m => m.StudentTimetableComponent)
            },
            {
                path: 'assignments',
                loadComponent: () => import('./features/student/assignments/student-assignments/student-assignments.component').then(m => m.StudentAssignmentsComponent)
            },
            {
                path: 'attendance',
                loadComponent: () => import('./features/student/attendance/attendance.component').then(m => m.AttendanceComponent)
            },
            {
                path: 'scan/qr',
                loadComponent: () => import('./features/student/scan/scan-qr/scan-qr.component').then(m => m.ScanQrComponent)
            },
            {
                path: 'grades',
                loadComponent: () => import('./features/student/grades/grades.component').then(m => m.GradesComponent)
            },
            {
                path: 'videos',
                loadComponent: () => import('./features/student/videos/videos.component').then(m => m.VideosComponent)
            },
            {
                path: 'exams',
                children: [
                    {
                        path: '',
                        loadComponent: () => import('./features/student/exams/student-exams/student-exams.component').then(m => m.StudentExamsComponent)
                    },
                    {
                        path: 'take/:id',
                        loadComponent: () => import('./features/student/exams/take-exam/take-exam.component').then(m => m.TakeExamComponent)
                    }
                ]
            }
        ]
    },
    {
        path: 'parent',
        canActivate: [AuthGuard, RoleGuard],
        data: { roles: ['Parent'] },
        children: [
            {
                path: 'dashboard',
                loadComponent: () => import('./features/parent/dashboard/dashboard.component').then(m => m.DashboardComponent)
            },
            {
                path: 'payments',
                loadComponent: () => import('./features/parent/payments/payments.component').then(m => m.PaymentsComponent)
            }
        ]
    },
    {
        path: 'profile',
        canActivate: [AuthGuard],
        loadComponent: () => import('./features/profile/user-profile/user-profile.component').then(m => m.UserProfileComponent)
    },
    {
        path: 'live/:id',
        canActivate: [AuthGuard],
        loadComponent: () => import('./features/live/live-classroom/live-classroom.component').then(m => m.LiveClassroomComponent)
    },
    {
        path: 'chat',
        canActivate: [AuthGuard],
        loadComponent: () => import('./features/chat/chat-room/chat-room.component').then(m => m.ChatRoomComponent)
    },
    {
        path: '**',
        redirectTo: '/auth/login'
    }
];

@NgModule({
    imports: [RouterModule.forRoot(routes)],
    exports: [RouterModule]
})
export class AppRoutingModule { }
