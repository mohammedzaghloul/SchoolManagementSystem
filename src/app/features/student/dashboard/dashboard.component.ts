import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { DashboardService } from '../../../core/services/dashboard.service';
import { AnnouncementService, Announcement } from '../../../core/services/announcement.service';

@Component({
  selector: 'app-student-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, DatePipe],
  templateUrl: './student-dashboard.component.html',
  styleUrls: ['./student-dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  studentName = '';
  attendancePercent: number | string = '—';
  className = '';
  gradeLevel = '';
  todaySessions: any[] = [];
  recentGrades: any[] = [];
  nextSession: any = null;
  upcomingExams: any[] = [];
  upcomingAssignmentsCount = 0;
  announcements: Announcement[] = [];
  loading = true;

  constructor(
    private authService: AuthService,
    private dashboardService: DashboardService,
    private announcementService: AnnouncementService
  ) { }

  async ngOnInit() {
    const user = this.authService.getCurrentUser();
    this.studentName = user?.fullName || 'الطالب';

    await this.loadDashboardData();
  }

  private async loadDashboardData() {
    this.loading = true;
    try {
      const [data, announcements] = await Promise.all([
        this.dashboardService.getStudentStats(),
        this.announcementService.getAnnouncements()
      ]);

      this.attendancePercent = data.attendanceRate || '—';
      this.className = data.className || '';
      this.gradeLevel = data.gradeLevel || '';
      this.todaySessions = data.todaySessions || [];
      this.recentGrades = data.recentGrades || [];
      this.nextSession = data.nextSession || null;
      this.upcomingExams = data.upcomingExams || [];
      this.upcomingAssignmentsCount = data.upcomingAssignmentsCount || 0;
      this.announcements = announcements.slice(0, 3);
    } catch (err: any) {
      console.error('Student Dashboard load error:', err);
    } finally {
      this.loading = false;
    }
  }
}
