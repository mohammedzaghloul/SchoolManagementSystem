import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { DashboardService } from '../../../core/services/dashboard.service';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  adminName = '';
  stats = {
    totalStudents: 0,
    totalTeachers: 0,
    totalClasses: 0,
    attendanceRate: '—',
    monthlyRevenue: 0,
    pendingFees: 0
  };
  today = new Date();
  recentStudents: any[] = [];
  loading = true;
  error = '';

  constructor(
    private dashboardService: DashboardService,
    private authService: AuthService
  ) {}

  async ngOnInit(): Promise<void> {
    const user = this.authService.getCurrentUser();
    this.adminName = user?.fullName || 'المدير';
    await this.loadStats();
  }

  async loadStats(): Promise<void> {
    this.loading = true;

    try {
      const data = await this.dashboardService.getAdminStats();
      this.stats.totalStudents = data.totalStudents;
      this.stats.totalTeachers = data.totalTeachers;
      this.stats.totalClasses = data.totalClasses;
      this.stats.attendanceRate = data.attendanceRate || '—';
      this.stats.monthlyRevenue = data.monthlyRevenue || 0;
      this.stats.pendingFees = data.pendingFees || 0;
      this.recentStudents = data.recentStudents || [];
    } catch (err: any) {
      console.error('Dashboard load error:', err);
      this.error = 'حدث خطأ في تحميل بيانات لوحة التحكم.';
    } finally {
      this.loading = false;
    }
  }

  logout(): void {
    this.authService.logout();
  }
}
