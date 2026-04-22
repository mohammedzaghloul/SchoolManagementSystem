import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationCenterService } from '../../../core/services/notification-center.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-bottom-nav',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './bottom-nav.component.html',
  styleUrls: ['./bottom-nav.component.css']
})
export class BottomNavComponent implements OnInit, OnDestroy {
  isAuthenticated = false;
  userRole = '';
  showMenu = false;
  unreadChatCount = 0;
  private authSub?: Subscription;
  private notifSub?: Subscription;

  // Role-based route shortcuts
  dashboardRoute = '/';
  attendanceRoute: string | null = null;
  attendanceLabel = 'الحضور';
  classesRoute: string | null = null;
  classesLabel = 'الحصص';
  classesIcon = 'fa-calendar-alt';
  paymentsRoute: string | null = null;

  constructor(
    private authService: AuthService,
    private notificationCenter: NotificationCenterService,
    private router: Router
  ) { }

  ngOnInit() {
    this.authSub = this.authService.currentUser$.subscribe(user => {
      this.isAuthenticated = !!user;
      if (user) {
        this.userRole = user.role || '';
        this.setRoutes(this.userRole);
      }
    });

    this.notifSub = this.notificationCenter.unreadMessageCount$.subscribe(count => {
      this.unreadChatCount = this.router.url.includes('/chat') ? 0 : count;
    });
  }

  setRoutes(role: string) {
    switch (role) {
      case 'Admin':
        this.dashboardRoute = '/admin/dashboard';
        this.attendanceRoute = '/admin/teachers';
        this.attendanceLabel = 'المدرسين';
        this.classesRoute = '/admin/students';
        this.classesLabel = 'الطلاب';
        this.classesIcon = 'fa-user-graduate';
        this.paymentsRoute = null;
        break;
      case 'Teacher':
        this.dashboardRoute = '/teacher/dashboard';
        this.attendanceRoute = '/teacher/attendance';
        this.attendanceLabel = 'الحضور';
        this.classesRoute = '/teacher/classes';
        this.classesLabel = 'حصصي';
        this.classesIcon = 'fa-chalkboard';
        this.paymentsRoute = null;
        break;
      case 'Student':
        this.dashboardRoute = '/student/dashboard';
        this.attendanceRoute = '/student/attendance';
        this.attendanceLabel = 'حضوري';
        this.classesRoute = '/student/timetable';
        this.classesLabel = 'جدولي';
        this.classesIcon = 'fa-calendar-alt';
        this.paymentsRoute = null;
        break;
      case 'Parent':
        this.dashboardRoute = '/parent/dashboard';
        this.attendanceRoute = null;
        this.classesRoute = null;
        this.paymentsRoute = '/parent/payments';
        break;
      default:
        this.dashboardRoute = '/';
    }
  }

  toggleMenu() {
    this.showMenu = !this.showMenu;
  }

  onChatClick() {
    this.unreadChatCount = 0;
    this.notificationCenter.markAllAsRead('message');
  }

  logout() {
    this.showMenu = false;
    this.authService.logout();
  }

  ngOnDestroy() {
    this.authSub?.unsubscribe();
    this.notifSub?.unsubscribe();
  }
}
