import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../../core/services/auth.service';
import { SessionService } from '../../../../core/services/session.service';
import { PaginatorComponent } from '../../../../shared/components/paginator/paginator.component';

interface TeacherAttendanceSession {
  id: number;
  subjectName: string;
  gradeName?: string;
  classRoomName: string;
  classRoomId?: number;
  sessionDate?: string;
  startTime?: string;
  endTime?: string;
  studentCount?: number;
  isRecorded?: boolean;
  attendanceCount?: number;
  attendanceType?: string;
  attendanceWindowStatus?: string;
  attendanceWindowLabel?: string;
  canRecordAttendance?: boolean;
  needsAttention?: boolean;
}

@Component({
  selector: 'app-attendance-sessions',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PaginatorComponent],
  templateUrl: './attendance-sessions.component.html',
  styleUrls: ['./attendance-sessions.component.css']
})
export class AttendanceSessionsComponent implements OnInit {
  sessions: TeacherAttendanceSession[] = [];
  loading = false;
  errorMessage = '';
  lastUpdated: Date | null = null;
  selectedDate = this.getDateInputValue(new Date());
  searchTerm = '';
  statusFilter = 'all';
  classFilter = 'all';

  currentPage = 1;
  pageSize = 100;
  visibleSessions: (TeacherAttendanceSession & {
    computedStatusClass?: string;
    computedStatusLabel?: string;
    computedStartTime?: string;
    computedEndTime?: string;
    computedTypeLabel?: string;
    computedModesLabel?: string;
    computedWindowBadgeClass?: string;
    computedAttendanceSummary?: string;
    computedAttendanceProgress?: number;
    computedActionDisabled?: boolean;
  })[] = [];

  constructor(
    private sessionService: SessionService,
    private authService: AuthService
  ) {}

  async ngOnInit(): Promise<void> {
    await this.loadSessions();
  }

  get classOptions(): string[] {
    return Array.from(new Set(this.sessions.map(session => session.classRoomName).filter(Boolean))).sort((first, second) =>
      first.localeCompare(second, 'ar')
    );
  }

  get filteredSessions(): TeacherAttendanceSession[] {
    const search = this.searchTerm.trim().toLowerCase();

    return this.sessions.filter(session => {
      const matchesSearch = !search ||
        session.subjectName.toLowerCase().includes(search) ||
        session.classRoomName.toLowerCase().includes(search) ||
        (session.gradeName || '').toLowerCase().includes(search);

      const matchesClass = this.classFilter === 'all' || session.classRoomName === this.classFilter;
      const matchesStatus = this.matchesStatusFilter(session);

      return matchesSearch && matchesClass && matchesStatus;
    });
  }

  get totalSessions(): number {
    return this.filteredSessions.length;
  }

  updateVisibleSessions() {
    const start = (this.currentPage - 1) * this.pageSize;
    this.visibleSessions = this.filteredSessions.slice(start, start + this.pageSize).map(session => ({
      ...session,
      computedStatusClass: this.getSessionStatusClass(session),
      computedStatusLabel: this.getSessionStatusLabel(session),
      computedStartTime: this.formatTime(session.startTime),
      computedEndTime: this.formatTime(session.endTime),
      computedTypeLabel: this.getAttendanceTypeLabel(session),
      computedModesLabel: this.getAttendanceModesLabel(session),
      computedWindowBadgeClass: this.getWindowBadgeClass(session),
      computedAttendanceSummary: this.getAttendanceSummary(session),
      computedAttendanceProgress: this.getAttendanceProgress(session),
      computedActionDisabled: this.getActionDisabled(session)
    }));
  }

  onPageChange(page: number) {
    this.currentPage = page;
    this.updateVisibleSessions();
  }

  onFilterChange() {
    this.currentPage = 1; // reset to page 1 on filter
    this.updateVisibleSessions();
  }

  get recordedSessionsCount(): number {
    return this.filteredSessions.filter(session => !!session.isRecorded).length;
  }

  get pendingSessionsCount(): number {
    return this.filteredSessions.filter(session => !!session.needsAttention).length;
  }

  get totalStudentsAcrossSessions(): number {
    return this.filteredSessions.reduce((sum, session) => sum + Number(session.studentCount || 0), 0);
  }

  get totalRecordedStudents(): number {
    return this.filteredSessions.reduce((sum, session) => sum + Number(session.attendanceCount || 0), 0);
  }

  get completionRate(): number {
    if (!this.filteredSessions.length) {
      return 0;
    }

    return Math.round((this.recordedSessionsCount / this.filteredSessions.length) * 100);
  }

  get selectedDateLabel(): string {
    const parsed = new Date(`${this.selectedDate}T00:00:00`);
    if (Number.isNaN(parsed.getTime())) {
      return 'اليوم غير محدد';
    }

    return parsed.toLocaleDateString('ar-EG', {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric'
    });
  }

  async loadSessions(): Promise<void> {
    this.loading = true;
    this.errorMessage = '';

    try {
      const user = this.authService.getCurrentUser();
      const teacherId = user?.id;
      const response: any = await this.sessionService.getTeacherSessions(teacherId, this.selectedDate);
      const sessions: TeacherAttendanceSession[] = Array.isArray(response) ? response : response?.data || [];
      this.sessions = sessions.sort((first, second) => this.getTimeValue(first.startTime) - this.getTimeValue(second.startTime));
      this.lastUpdated = new Date();
      this.updateVisibleSessions();
    } catch (error: any) {
      this.sessions = [];
      this.errorMessage = error?.message || 'تعذر تحميل حصص اليوم من قاعدة البيانات.';
      this.updateVisibleSessions();
    } finally {
      this.loading = false;
    }
  }

  formatTime(time?: string): string {
    if (!time) {
      return '—';
    }

    const parsed = new Date(time);
    if (!Number.isNaN(parsed.getTime())) {
      return parsed.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
    }

    const parts = time.split(':');
    const normalized = new Date();
    normalized.setHours(Number(parts[0] || 0), Number(parts[1] || 0), 0, 0);
    return normalized.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
  }

  getSessionStatusLabel(session: TeacherAttendanceSession): string {
    if (session.attendanceWindowStatus === 'closed') {
      return session.isRecorded ? 'تم الإقفال بعد الرصد' : 'أغلقت النافذة';
    }

    if (session.attendanceWindowStatus === 'upcoming') {
      return 'النافذة لم تبدأ';
    }

    if (session.needsAttention) {
      return 'تحتاج رصد الآن';
    }

    if (session.isRecorded) {
      return 'تم الرصد';
    }

    return 'جاهزة للرصد';
  }

  getSessionStatusClass(session: TeacherAttendanceSession): string {
    if (session.attendanceWindowStatus === 'closed') {
      return 'status-closed';
    }

    if (session.attendanceWindowStatus === 'upcoming') {
      return 'status-upcoming';
    }

    if (session.needsAttention) {
      return 'status-pending';
    }

    return session.isRecorded ? 'status-recorded' : 'status-open';
  }

  getAttendanceSummary(session: TeacherAttendanceSession): string {
    const recorded = Number(session.attendanceCount || 0);
    const total = Number(session.studentCount || 0);

    if (!recorded) {
      return 'لم يبدأ تسجيل الحضور والغياب بعد';
    }

    if (total && recorded >= total) {
      return 'تم رصد جميع طلاب الحصة';
    }

    return `${recorded} من ${total || recorded} طالب تم رصدهم`;
  }

  getAttendanceProgress(session: TeacherAttendanceSession): number {
    const recorded = Number(session.attendanceCount || 0);
    const total = Number(session.studentCount || 0);

    if (!total) {
      return recorded > 0 ? 100 : 0;
    }

    return Math.min(100, Math.round((recorded / total) * 100));
  }

  getAttendanceTypeLabel(session: TeacherAttendanceSession): string {
    switch ((session.attendanceType || '').toLowerCase()) {
      case 'face':
        return 'Face ID';
      case 'manual':
        return 'يدوي';
      default:
        return 'QR';
    }
  }

  getAttendanceModesLabel(session: TeacherAttendanceSession): string {
    return `QR + Face ID + يدوي${session.attendanceType ? ` · الافتراضي ${this.getAttendanceTypeLabel(session)}` : ''}`;
  }

  getWindowBadgeClass(session: TeacherAttendanceSession): string {
    return `window-${session.attendanceWindowStatus || 'upcoming'}`;
  }

  getActionDisabled(session: TeacherAttendanceSession): boolean {
    return !session.id;
  }

  async shiftSelectedDate(days: number): Promise<void> {
    const current = new Date(`${this.selectedDate}T00:00:00`);
    if (Number.isNaN(current.getTime())) {
      this.selectedDate = this.getDateInputValue(new Date());
    } else {
      current.setDate(current.getDate() + days);
      this.selectedDate = this.getDateInputValue(current);
    }

    await this.loadSessions();
  }

  async onDateChanged(): Promise<void> {
    await this.loadSessions();
  }

  trackBySession(_: number, session: TeacherAttendanceSession): number {
    return session.id;
  }

  private matchesStatusFilter(session: TeacherAttendanceSession): boolean {
    switch (this.statusFilter) {
      case 'recorded':
        return !!session.isRecorded;
      case 'needs-attention':
        return !!session.needsAttention;
      case 'open':
        return session.attendanceWindowStatus === 'open';
      case 'upcoming':
        return session.attendanceWindowStatus === 'upcoming';
      case 'closed':
        return session.attendanceWindowStatus === 'closed';
      default:
        return true;
    }
  }

  private getDateInputValue(value: Date): string {
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private getTimeValue(value?: string): number {
    if (!value) {
      return Number.MAX_SAFE_INTEGER;
    }

    const parsed = new Date(value);
    if (!Number.isNaN(parsed.getTime())) {
      return parsed.getTime();
    }

    const [hours, minutes] = value.split(':').map(part => Number(part));
    return (Number.isFinite(hours) ? hours : 99) * 60 + (Number.isFinite(minutes) ? minutes : 0);
  }
}
