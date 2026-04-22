import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';

import { AttendanceService } from '../../../core/services/attendance.service';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SessionService } from '../../../core/services/session.service';

type AttendanceStatus = 'Present' | 'Absent' | 'Late' | 'Unrecorded';

interface TeacherSessionOption {
  id: number;
  subjectName: string;
  gradeName?: string;
  classRoomName: string;
  classRoomId?: number;
  startTime?: string;
  endTime?: string;
  studentCount?: number;
  attendanceCount?: number;
  attendanceWindowStatus?: string;
  attendanceWindowLabel?: string;
  canRecordAttendance?: boolean;
}

interface RosterStudent {
  id: number;
  fullName: string;
  email?: string;
  status: AttendanceStatus;
  isPresent: boolean;
  notes: string;
  method?: string;
  recordedAt?: string;
}

interface SessionRosterResponse {
  sessionId: number;
  subjectName: string;
  classRoomName: string;
  attendanceWindowStatus: string;
  attendanceWindowMessage: string;
  canRecordAttendance: boolean;
  totalStudents: number;
  students: RosterStudent[];
}

@Component({
  selector: 'app-manual-attendance',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './manual-attendance.component.html',
  styleUrls: ['./manual-attendance.component.css']
})
export class ManualAttendanceComponent implements OnInit {
  sessions: TeacherSessionOption[] = [];
  students: RosterStudent[] = [];

  selectedSessionId: number | null = null;
  selectedDate = this.getDateInputValue(new Date());
  searchTerm = '';
  statusFilter = 'all';

  attendanceWindowStatus = 'upcoming';
  attendanceWindowMessage = 'اختر الحصة لعرض نافذة الرصد.';
  canRecordAttendance = false;

  loadingSessions = false;
  loadingRoster = false;
  saving = false;
  lastSavedAt: Date | null = null;

  constructor(
    private attendanceService: AttendanceService,
    private sessionService: SessionService,
    private authService: AuthService,
    private route: ActivatedRoute,
    private notify: NotificationService
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(async params => {
      if (params['date']) {
        this.selectedDate = String(params['date']);
      }

      if (params['sessionId']) {
        this.selectedSessionId = Number(params['sessionId']);
      }

      await this.loadSessions();

      if (this.selectedSessionId) {
        await this.loadSessionRoster();
      }
    });
  }

  get selectedSession(): TeacherSessionOption | undefined {
    return this.sessions.find(session => session.id === this.selectedSessionId);
  }

  get selectedSessionLabel(): string {
    if (!this.selectedSession) {
      return 'اختر الحصة لبدء رصد الغياب والحضور.';
    }

    return `${this.selectedSession.subjectName} - ${this.selectedSession.classRoomName}`;
  }

  get selectedSessionTimeLabel(): string {
    if (!this.selectedSession?.startTime) {
      return 'حدد الحصة لعرض التوقيت';
    }

    const start = this.formatTime(this.selectedSession.startTime);
    const end = this.selectedSession.endTime ? this.formatTime(this.selectedSession.endTime) : '';
    return end ? `${start} - ${end}` : start;
  }

  get totalStudents(): number {
    return this.students.length;
  }

  get presentCount(): number {
    return this.students.filter(student => student.status === 'Present').length;
  }

  get absentCount(): number {
    return this.students.filter(student => student.status === 'Absent').length;
  }

  get lateCount(): number {
    return this.students.filter(student => student.status === 'Late').length;
  }

  get unrecordedCount(): number {
    return this.students.filter(student => student.status === 'Unrecorded').length;
  }

  get filteredStudents(): RosterStudent[] {
    const search = this.searchTerm.trim().toLowerCase();

    return this.students.filter(student => {
      const matchesSearch = !search ||
        student.fullName.toLowerCase().includes(search) ||
        (student.email || '').toLowerCase().includes(search);

      const matchesStatus = this.statusFilter === 'all' ||
        student.status.toLowerCase() === this.statusFilter;

      return matchesSearch && matchesStatus;
    });
  }

  async loadSessions(): Promise<void> {
    this.loadingSessions = true;

    try {
      const user = this.authService.getCurrentUser();
      const teacherId = user?.id;
      const response: any = await this.sessionService.getTeacherSessions(teacherId, this.selectedDate);
      this.sessions = Array.isArray(response) ? response : response?.data || [];

      if (this.selectedSessionId && !this.sessions.some(session => session.id === this.selectedSessionId)) {
        this.selectedSessionId = null;
        this.students = [];
        this.canRecordAttendance = false;
        this.attendanceWindowStatus = 'upcoming';
        this.attendanceWindowMessage = 'لا توجد بيانات لهذه الحصة في التاريخ المختار.';
      }
    } catch (error: any) {
      this.sessions = [];
      this.students = [];
      this.notify.error(error?.message || 'تعذر تحميل حصص اليوم.');
    } finally {
      this.loadingSessions = false;
    }
  }

  async onSessionChanged(): Promise<void> {
    await this.loadSessionRoster();
  }

  async onDateChanged(): Promise<void> {
    await this.loadSessions();

    if (this.selectedSessionId) {
      await this.loadSessionRoster();
    }
  }

  async shiftSelectedDate(days: number): Promise<void> {
    const current = new Date(`${this.selectedDate}T00:00:00`);
    if (Number.isNaN(current.getTime())) {
      this.selectedDate = this.getDateInputValue(new Date());
    } else {
      current.setDate(current.getDate() + days);
      this.selectedDate = this.getDateInputValue(current);
    }

    await this.onDateChanged();
  }

  async loadSessionRoster(): Promise<void> {
    if (!this.selectedSessionId) {
      this.students = [];
      return;
    }

    this.loadingRoster = true;

    try {
      const roster = await this.attendanceService.getSessionRoster(this.selectedSessionId) as SessionRosterResponse;

      this.students = (roster.students || []).map(student => ({
        id: Number(student.id),
        fullName: student.fullName,
        email: student.email,
        status: this.normalizeStatus(student.status),
        isPresent: !!student.isPresent,
        notes: student.notes || '',
        method: student.method,
        recordedAt: student.recordedAt
      }));

      this.attendanceWindowStatus = roster.attendanceWindowStatus || 'upcoming';
      this.attendanceWindowMessage = roster.attendanceWindowMessage || 'لا توجد تفاصيل متاحة لنافذة الرصد.';
      this.canRecordAttendance = !!roster.canRecordAttendance;
    } catch (error: any) {
      this.students = [];
      this.canRecordAttendance = false;
      this.attendanceWindowStatus = 'closed';
      this.attendanceWindowMessage = error?.message || 'تعذر تحميل كشف الطلاب.';
      this.notify.error(error?.message || 'تعذر تحميل كشف الطلاب.');
    } finally {
      this.loadingRoster = false;
    }
  }

  setStudentStatus(student: RosterStudent, status: AttendanceStatus): void {
    if (!this.canRecordAttendance || this.saving) {
      return;
    }

    student.status = status;
    student.isPresent = status === 'Present' || status === 'Late';
  }

  applyBulkStatus(status: AttendanceStatus): void {
    if (!this.canRecordAttendance || this.saving) {
      return;
    }

    this.filteredStudents.forEach(student => {
      student.status = status;
      student.isPresent = status === 'Present' || status === 'Late';
    });
  }

  async saveAttendance(): Promise<void> {
    if (!this.selectedSessionId) {
      this.notify.warning('اختر الحصة أولًا.');
      return;
    }

    if (!this.canRecordAttendance) {
      this.notify.warning(this.attendanceWindowMessage);
      return;
    }

    this.saving = true;

    try {
      await this.attendanceService.markManual({
        sessionId: String(this.selectedSessionId),
        classId: String(this.selectedSessionId),
        subjectId: '0',
        records: this.students.map(student => ({
          id: String(student.id),
          name: student.fullName,
          isPresent: student.status === 'Present' || student.status === 'Late',
          status: student.status,
          notes: student.notes
        }))
      });

      this.lastSavedAt = new Date();
      this.notify.success('تم حفظ الرصد اليدوي بنجاح.');
      await this.loadSessionRoster();
    } catch (error: any) {
      this.notify.error(error?.message || 'حدث خطأ أثناء الحفظ.');
    } finally {
      this.saving = false;
    }
  }

  getWindowToneClass(): string {
    return `window-${this.attendanceWindowStatus || 'upcoming'}`;
  }

  getStatusChipClass(status: AttendanceStatus): string {
    switch (status) {
      case 'Present':
        return 'status-present';
      case 'Absent':
        return 'status-absent';
      case 'Late':
        return 'status-late';
      default:
        return 'status-unrecorded';
    }
  }

  formatTime(value?: string): string {
    if (!value) {
      return '—';
    }

    const parsed = new Date(value);
    if (!Number.isNaN(parsed.getTime())) {
      return parsed.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
    }

    const parts = value.split(':');
    const normalized = new Date();
    normalized.setHours(Number(parts[0] || 0), Number(parts[1] || 0), 0, 0);
    return normalized.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
  }

  trackByStudent(_: number, student: RosterStudent): number {
    return student.id;
  }

  private normalizeStatus(status?: string): AttendanceStatus {
    switch ((status || '').toLowerCase()) {
      case 'present':
        return 'Present';
      case 'absent':
        return 'Absent';
      case 'late':
        return 'Late';
      default:
        return 'Unrecorded';
    }
  }

  private getDateInputValue(value: Date): string {
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
