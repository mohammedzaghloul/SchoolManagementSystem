import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { DashboardService } from '../../../core/services/dashboard.service';
import { AnnouncementService, Announcement } from '../../../core/services/announcement.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  teacherName = '';
  stats = {
    totalStudents: 0,
    attendanceAvg: '—',
    todayClasses: 0
  };
  currentSession: any = null;
  upcomingSessions: any[] = [];
  todaySessions: any[] = [];
  announcements: Announcement[] = [];
  loading = true;

  constructor(
    private dashboardService: DashboardService,
    private authService: AuthService,
    private announcementService: AnnouncementService
  ) { }

  async ngOnInit() {
    const user = this.authService.getCurrentUser();
    this.teacherName = user?.fullName || 'المعلم';
    await this.loadDashboardData();
  }

  async loadDashboardData() {
    this.loading = true;
    try {
      const [data, announcements] = await Promise.all([
        this.dashboardService.getTeacherStats(),
        this.announcementService.getAnnouncements()
      ]);

      this.announcements = announcements.slice(0, 3);
      
      // Map API data to component state
      this.stats.totalStudents = data?.totalStudents || 0;
      this.stats.attendanceAvg = data?.attendanceAvg || '95%';
      this.todaySessions = data?.todaySessions || [];
      this.stats.todayClasses = data?.todayClasses || this.todaySessions.length;

      const now = new Date();

      // Find current active session
      this.currentSession = this.todaySessions.find((s: any) => {
        const start = this.parseSessionTime(s.startTime);
        const end = this.parseSessionTime(s.endTime || '23:59:59');
        return now >= start && now <= end;
      }) || null;

      this.upcomingSessions = this.todaySessions.filter((s: any) => {
        return this.parseSessionTime(s.startTime) > now;
      });

    } catch (err: any) {
      console.error('[Dashboard] DB Connection error:', err);
    } finally {
      this.loading = false;
    }
  }

  formatTime(time: any): string {
    if (!time) return '—';
    // If it's an ISO date string like "2026-04-05T08:00:00"
    if (typeof time === 'string' && time.includes('T')) {
      const d = new Date(time);
      return d.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
    }
    // If it's a plain time string like "08:00:00" or "08:00"
    if (typeof time === 'string') {
      const parts = time.split(':');
      if (parts.length < 2) return time;
      let hours = parseInt(parts[0], 10);
      const minutes = parts[1] || '00';
      const period = hours >= 12 ? 'م' : 'ص';
      if (hours > 12) hours -= 12;
      if (hours === 0) hours = 12;
      return `${hours}:${minutes} ${period}`;
    }
    // If it's a Date object
    if (time instanceof Date) {
      return time.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
    }
    return String(time);
  }

  private parseSessionTime(timeStr: string): Date {
    const now = new Date();
    if (!timeStr) return now;

    // Handle full ISO date strings or just time strings
    if (timeStr.includes('T')) {
      return new Date(timeStr);
    }

    const [hours, minutes, seconds] = timeStr.split(':').map(Number);
    const date = new Date(now.getFullYear(), now.getMonth(), now.getDate(), hours, minutes, seconds || 0);
    return date;
  }
}
