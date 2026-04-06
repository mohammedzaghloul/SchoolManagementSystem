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
  classesRoute: string | null = null;
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
        this.attendanceRoute = null;
        this.classesRoute = '/admin/students';
        this.paymentsRoute = null;
        break;
      case 'Teacher':
        this.dashboardRoute = '/teacher/dashboard';
        this.attendanceRoute = '/teacher/attendance';
        this.classesRoute = '/teacher/videos';
        this.paymentsRoute = null;
        break;
      case 'Student':
        this.dashboardRoute = '/student/dashboard';
        this.attendanceRoute = '/student/attendance';
        this.classesRoute = '/student/timetable';
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
        this.paymentsRoute = null;
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
