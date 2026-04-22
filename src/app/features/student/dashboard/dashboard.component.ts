import { Component, OnInit, OnDestroy } from '@angular/core';
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
export class DashboardComponent implements OnInit, OnDestroy {
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
  timeUntilSession = '';
  private timer: any;

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

  ngOnDestroy() {
    if (this.timer) clearInterval(this.timer);
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

      if (this.nextSession) {
        this.startCountdown();
      }
    } catch (err: any) {
      console.error('Student Dashboard load error:', err);
    } finally {
      this.loading = false;
    }
  }

  private startCountdown() {
    if (!this.nextSession || !this.nextSession.startTime) return;
    
    if (this.timer) clearInterval(this.timer);

    const updateTimer = () => {
      const sessionDate = new Date(this.nextSession.startTime);
      const now = new Date();
      const diff = sessionDate.getTime() - now.getTime();

      if (diff <= 0) {
        this.timeUntilSession = 'متاحة الآن';
        if (this.timer) clearInterval(this.timer);
        return;
      }

      const hours = Math.floor(diff / (1000 * 60 * 60));
      const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
      const seconds = Math.floor((diff % (1000 * 60)) / 1000);

      this.timeUntilSession = `${hours}س ${minutes}د ${seconds}ث`;
    };

    updateTimer();
    this.timer = setInterval(updateTimer, 1000);
  }
}
