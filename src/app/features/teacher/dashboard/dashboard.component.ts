import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { Announcement, AnnouncementService } from '../../../core/services/announcement.service';
import { AuthService } from '../../../core/services/auth.service';
import { DashboardService } from '../../../core/services/dashboard.service';
import { SessionService } from '../../../core/services/session.service';

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
  attendanceFocusSession: any = null;
  lastCompletedSession: any = null;
  nextUpcomingSession: any = null;
  upcomingSessions: any[] = [];
  todaySessions: any[] = [];
  announcements: Announcement[] = [];
  loading = true;

  readonly quickLinks = [
    { label: 'رصد الحضور', icon: 'fas fa-clipboard-check', route: '/teacher/attendance', tone: 'blue' },
    { label: 'جدول الأسبوع', icon: 'fas fa-calendar-week', route: '/teacher/timetable', tone: 'slate' },
    { label: 'درجات الطلاب', icon: 'fas fa-pen-to-square', route: '/teacher/grades', tone: 'green' },
    { label: 'الواجبات', icon: 'fas fa-list-check', route: '/teacher/assignments', tone: 'amber' },
    { label: 'الفيديوهات', icon: 'fas fa-circle-play', route: '/teacher/videos', tone: 'red' },
    { label: 'الامتحانات', icon: 'fas fa-file-lines', route: '/teacher/exams', tone: 'violet' },
    { label: 'الشات', icon: 'fas fa-comments', route: '/chat', tone: 'cyan' }
  ];

  constructor(
    private router: Router,
    private dashboardService: DashboardService,
    private authService: AuthService,
    private announcementService: AnnouncementService,
    private sessionService: SessionService
  ) {}

  async ngOnInit() {
    const user = this.authService.getCurrentUser();
    this.teacherName = user?.fullName || 'المعلم';
    await this.loadDashboardData();
  }

  async loadDashboardData() {
    this.loading = true;

    try {
      const user = this.authService.getCurrentUser();
      const todayKey = new Date().toISOString().slice(0, 10);

      const [data, announcements, detailedSessions] = await Promise.all([
        this.dashboardService.getTeacherStats(),
        this.announcementService.getAnnouncements(),
        user?.id ? this.sessionService.getTeacherSessions(user.id, todayKey).catch(() => []) : Promise.resolve([])
      ]);

      const detailsById = new Map(
        (Array.isArray(detailedSessions) ? detailedSessions : []).map((session: any) => [Number(session.id), session])
      );

      this.announcements = announcements.slice(0, 3);
      this.stats.totalStudents = data?.totalStudents || 0;
      this.stats.attendanceAvg = data?.attendanceAvg || '95%';

      this.todaySessions = (data?.todaySessions || []).map((session: any) => ({
        ...session,
        ...(detailsById.get(Number(session.id)) || {})
      }));

      this.stats.todayClasses = data?.todayClasses || this.todaySessions.length;

      const now = new Date();
      this.currentSession = this.todaySessions.find((session: any) => this.isSessionLive(session, now)) || null;
      this.attendanceFocusSession = this.currentSession ? null : this.selectAttendanceFocusSession(this.todaySessions);
      this.lastCompletedSession = this.findLastCompletedSession(this.todaySessions, now);
      this.nextUpcomingSession = this.findNextUpcomingSession(this.todaySessions, now);
      this.upcomingSessions = this.todaySessions.filter((session: any) => this.parseSessionTime(session.startTime) > now);
    } catch (err: any) {
      console.error('[Dashboard] DB Connection error:', err);
    } finally {
      this.loading = false;
    }
  }

  get heroSession(): any | null {
    return this.currentSession || this.attendanceFocusSession;
  }

  get isAttendanceWindowState(): boolean {
    return !this.currentSession && !!this.attendanceFocusSession;
  }

  get qrTargetSession(): any | null {
    return this.pickPreferredActionSession(this.todaySessions);
  }

  get qrTargetHint(): string {
    if (!this.qrTargetSession) {
      return 'لا توجد حصة متاحة للرصد الآن.';
    }

    if (this.heroSession && this.qrTargetSession.id !== this.heroSession.id) {
      return `${this.qrTargetSession.subjectName} - ${this.qrTargetSession.classRoomName} ستكون الحصة المفتوحة لبث QR.`;
    }

    return 'سيتم فتح نفس الحصة المتاحة لبث رمز QR.';
  }

  get pendingAttendanceSessions(): number {
    return this.todaySessions.filter((session: any) => !!session?.needsAttention).length;
  }

  get recordedSessionsCount(): number {
    return this.todaySessions.filter((session: any) => !!session?.isRecorded).length;
  }

  get todayProgressLabel(): string {
    if (!this.todaySessions.length) {
      return 'لا توجد حصص';
    }

    return `${this.recordedSessionsCount}/${this.todaySessions.length}`;
  }

  formatTime(time: any): string {
    if (!time) return '—';

    if (typeof time === 'string' && time.includes('T')) {
      const parsedDate = new Date(time);
      return parsedDate.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
    }

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

    if (time instanceof Date) {
      return time.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
    }

    return String(time);
  }

  async openQrBroadcast(): Promise<void> {
    if (!this.qrTargetSession) {
      return;
    }

    await this.router.navigate(
      ['/teacher/attendance/qr'],
      { queryParams: { sessionId: this.qrTargetSession.id } }
    );
  }

  private isSessionLive(session: any, reference: Date): boolean {
    const start = this.parseSessionTime(session?.startTime);
    const end = this.parseSessionTime(session?.endTime || session?.startTime || '23:59:59');
    return reference >= start && reference <= end;
  }

  private pickPreferredActionSession(sessions: any[]): any | null {
    if (!sessions.length) {
      return null;
    }

    const reference = new Date();
    return [...sessions].sort((first: any, second: any) => {
      const scoreDifference = this.getActionSessionScore(second, reference) - this.getActionSessionScore(first, reference);
      if (scoreDifference !== 0) {
        return scoreDifference;
      }

      return this.parseSessionTime(second.startTime).getTime() - this.parseSessionTime(first.startTime).getTime();
    })[0] || null;
  }

  private getActionSessionScore(session: any, reference: Date): number {
    let score = 0;

    if (!!session?.canRecordAttendance) {
      score += 100;
    }

    if (this.isSessionLive(session, reference)) {
      score += 50;
    }

    if (!!session?.needsAttention) {
      score += 25;
    }

    const studentCount = Number(session?.studentCount || 0);
    const attendanceCount = Number(session?.attendanceCount || 0);
    score += Math.min(Math.max(studentCount - attendanceCount, 0), 10);

    return score;
  }

  private selectAttendanceFocusSession(sessions: any[]): any | null {
    return [...sessions]
      .filter((session: any) => !!session?.canRecordAttendance)
      .filter((session: any) =>
        !!session?.needsAttention ||
        !session?.isRecorded ||
        Number(session?.attendanceCount || 0) < Number(session?.studentCount || 0)
      )
      .sort((first: any, second: any) =>
        this.parseSessionTime(second.endTime || second.startTime).getTime() -
        this.parseSessionTime(first.endTime || first.startTime).getTime()
      )[0] || null;
  }

  private findLastCompletedSession(sessions: any[], reference: Date): any | null {
    const referenceTime = reference.getTime();

    return [...sessions]
      .filter((session: any) => this.parseSessionTime(session.endTime || session.startTime).getTime() < referenceTime)
      .sort((first: any, second: any) =>
        this.parseSessionTime(second.endTime || second.startTime).getTime() -
        this.parseSessionTime(first.endTime || first.startTime).getTime()
      )[0] || null;
  }

  private findNextUpcomingSession(sessions: any[], reference: Date): any | null {
    const referenceTime = reference.getTime();

    return [...sessions]
      .filter((session: any) => this.parseSessionTime(session.startTime).getTime() > referenceTime)
      .sort((first: any, second: any) =>
        this.parseSessionTime(first.startTime).getTime() -
        this.parseSessionTime(second.startTime).getTime()
      )[0] || null;
  }

  private parseSessionTime(timeStr: string): Date {
    const now = new Date();
    if (!timeStr) return now;

    if (timeStr.includes('T')) {
      return new Date(timeStr);
    }

    const [hours, minutes, seconds] = timeStr.split(':').map(Number);
    return new Date(now.getFullYear(), now.getMonth(), now.getDate(), hours, minutes, seconds || 0);
  }
}
