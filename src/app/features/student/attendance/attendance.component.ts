import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AttendanceService } from '../../../core/services/attendance.service';
import { SessionService } from '../../../core/services/session.service';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';

type AttendanceStatus = 'present' | 'absent' | 'late' | 'neutral';
type SessionState = 'recorded' | 'active' | 'locked' | 'done' | 'upcoming';

interface AttendanceRecord {
  sessionId: number;
  sessionName?: string;
  subjectName?: string;
  classRoomName?: string;
  status?: string;
  isPresent?: boolean;
  method?: string;
  recordedAt?: string;
}

interface AttendanceSummary {
  total: number;
  present: number;
  absent: number;
  late: number;
  attendanceRate: number;
}

interface StudentAttendanceSession {
  id: number;
  title?: string;
  subjectName?: string;
  teacherName?: string;
  classRoomName?: string;
  startTime?: string;
  endTime?: string;
  attendanceType?: string;
  isLive?: boolean;
  isActive?: boolean;
  isCompleted?: boolean;
  attendanceRecorded?: boolean;
  attendanceStatus?: string;
  attendanceMethod?: string;
  canMarkWithQr?: boolean;
}

interface AttendanceInsightCard {
  title: string;
  value: string;
  detail: string;
  icon: string;
  tone: 'success' | 'warning' | 'info';
}

@Component({
  selector: 'app-attendance',
  standalone: true,
  imports: [CommonModule, RouterModule, PaginatorComponent],
  templateUrl: './attendance.component.html',
  styleUrls: ['./attendance.component.css']
})
export class AttendanceComponent implements OnInit, OnDestroy {
  loading = true;
  errorMsg = '';
  records: AttendanceRecord[] = [];
  visibleRecords: (AttendanceRecord & {
    computedStatusClass?: string;
    computedStatusLabel?: string;
    computedMethodLabel?: string;
  })[] = [];
  todaySessions: StudentAttendanceSession[] = [];
  activeSession: StudentAttendanceSession | null = null;
  nextSession: StudentAttendanceSession | null = null;
  className = '';
  gradeLevel = '';
  summary: AttendanceSummary = {
    total: 0,
    present: 0,
    absent: 0,
    late: 0,
    attendanceRate: 0
  };

  countdownMessage = '';
  private timerInterval: ReturnType<typeof setInterval> | null = null;

  currentPage = 1;
  pageSize = 5;

  constructor(
    private attendanceService: AttendanceService,
    private sessionService: SessionService
  ) {}

  async ngOnInit(): Promise<void> {
    this.loading = true;
    this.errorMsg = '';

    await Promise.all([
      this.loadAttendanceOverview(),
      this.loadStudentContext()
    ]);

    this.loading = false;
  }

  ngOnDestroy(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
    }
  }

  get registrationTypeLabel(): string {
    return this.getMethodLabel(this.activeSession?.attendanceType || this.nextSession?.attendanceType || 'qr');
  }

  get canOpenQrScanner(): boolean {
    return !!this.activeSession?.canMarkWithQr;
  }

  get registrationTitle(): string {
    if (this.activeSession?.attendanceRecorded) {
      return `تم تسجيل حضورك بالفعل في ${this.activeSession.subjectName || this.activeSession.title || 'الحصة الحالية'}`;
    }

    if (this.activeSession?.canMarkWithQr) {
      return `يمكنك تسجيل حضورك الآن في ${this.activeSession.subjectName || this.activeSession.title || 'الحصة الحالية'}`;
    }

    if (this.activeSession) {
      return `${this.activeSession.subjectName || this.activeSession.title || 'الحصة الحالية'} نشطة الآن`;
    }

    if (this.nextSession) {
      return `أقرب حصة قادمة هي ${this.nextSession.subjectName || this.nextSession.title || 'الحصة القادمة'}`;
    }

    return 'لا توجد حصة متاحة حاليًا لتسجيل الحضور';
  }

  get registrationDescription(): string {
    if (this.activeSession?.attendanceRecorded) {
      return `حالة التسجيل الحالية: ${this.getStatusLabel({
        sessionId: this.activeSession.id,
        status: this.activeSession.attendanceStatus,
        isPresent: this.activeSession.attendanceStatus === 'Present'
      })}`;
    }

    if (this.activeSession?.canMarkWithQr) {
      return 'افتح ماسح QR ووجّه الكاميرا إلى رمز الحصة ليتم تسجيل حضورك فورًا.';
    }

    if (this.activeSession && !this.activeSession.canMarkWithQr) {
      return `طريقة الحضور الحالية لهذه الحصة هي ${this.registrationTypeLabel} وليست QR.`;
    }

    if (this.nextSession?.startTime) {
      const sessionTime = new Date(this.nextSession.startTime).toLocaleString('ar-EG', {
        weekday: 'long',
        hour: 'numeric',
        minute: '2-digit'
      });
      return this.countdownMessage
        ? `${sessionTime} - ${this.countdownMessage}`
        : `موعدها ${sessionTime}.`;
    }

    return 'سيظهر هنا تلقائيًا أول ما تكون هناك حصة متاحة أو نشطة لك.';
  }

  updateVisibleRecords() {
    const start = (this.currentPage - 1) * this.pageSize;
    this.visibleRecords = this.records.slice(start, start + this.pageSize).map(record => ({
      ...record,
      computedStatusClass: this.getStatusClass(record),
      computedStatusLabel: this.getStatusLabel(record),
      computedMethodLabel: this.getMethodLabel(record.method)
    }));
  }

  onPageChange(page: number) {
    this.currentPage = page;
    this.updateVisibleRecords();
  }

  get hasSessionsToday(): boolean {
    return this.todaySessions.length > 0;
  }

  get weeklyAttendanceRate(): number {
    const recentWeek = this.records.slice(0, 5);
    return this.calculateSummary(recentWeek).attendanceRate;
  }

  get attendanceStreak(): number {
    let streak = 0;

    for (const record of this.records) {
      const status = this.resolveStatus(record);
      if (status === 'present' || status === 'late') {
        streak += 1;
        continue;
      }

      break;
    }

    return streak;
  }

  get lastAbsenceLabel(): string {
    const lastAbsence = this.records.find(record => this.resolveStatus(record) === 'absent');
    if (!lastAbsence?.recordedAt) {
      return 'لا يوجد غياب حديث';
    }

    return new Date(lastAbsence.recordedAt).toLocaleDateString('ar-EG', {
      day: 'numeric',
      month: 'long'
    });
  }

  get insightCards(): AttendanceInsightCard[] {
    return [
      {
        title: 'الحضور هذا الأسبوع',
        value: `${this.weeklyAttendanceRate}%`,
        detail: 'اعتمادًا على آخر الحصص المسجلة',
        icon: 'fas fa-chart-line',
        tone: this.weeklyAttendanceRate >= 85 ? 'success' : 'warning'
      },
      {
        title: 'سلسلة الالتزام',
        value: `${this.attendanceStreak} حصص`,
        detail: this.attendanceStreak > 0 ? 'بدون غياب متتالي' : 'تحتاج بداية جديدة',
        icon: 'fas fa-fire',
        tone: this.attendanceStreak >= 4 ? 'success' : 'info'
      },
      {
        title: 'آخر غياب',
        value: this.lastAbsenceLabel,
        detail: this.absenceAdvisory,
        icon: 'fas fa-user-clock',
        tone: this.summary.absent <= 2 ? 'info' : 'warning'
      }
    ];
  }

  get absenceAdvisory(): string {
    if (this.summary.absent === 0) {
      return 'سجل ممتاز بدون أي غياب';
    }

    if (this.summary.absent <= 2) {
      return 'المعدل آمن حتى الآن';
    }

    return 'يفضل متابعة الغياب خلال هذا الأسبوع';
  }

  getSessionStateLabel(session: StudentAttendanceSession): string {
    switch (this.getSessionStateClass(session)) {
      case 'recorded':
        return 'تم التسجيل';
      case 'active':
        return 'متاح الآن';
      case 'locked':
        return 'نشطة';
      case 'done':
        return 'انتهت';
      default:
        return 'قادمة';
    }
  }

  getSessionStateClass(session: StudentAttendanceSession): SessionState {
    if (session.attendanceRecorded) {
      return 'recorded';
    }

    if (session.isActive && session.canMarkWithQr) {
      return 'active';
    }

    if (session.isActive) {
      return 'locked';
    }

    if (session.isCompleted) {
      return 'done';
    }

    return 'upcoming';
  }

  getStatusLabel(record: AttendanceRecord): string {
    switch (this.resolveStatus(record)) {
      case 'present':
        return 'حاضر';
      case 'late':
        return 'متأخر';
      case 'absent':
        return 'غائب';
      default:
        return 'غير محدد';
    }
  }

  getStatusClass(record: AttendanceRecord): AttendanceStatus {
    return this.resolveStatus(record);
  }

  getMethodLabel(method?: string): string {
    switch (String(method || '').toLowerCase()) {
      case 'qr':
        return 'QR';
      case 'face':
        return 'بصمة وجه';
      case 'manual':
        return 'يدوي';
      default:
        return 'غير محدد';
    }
  }

  private async loadAttendanceOverview(): Promise<void> {
    try {
      const [attendanceResponse, statsResponse] = await Promise.all([
        this.attendanceService.getMyAttendance().catch(() => null),
        this.attendanceService.getMyStats().catch(() => null)
      ]);

      const fetchedRecords = this.extractRecords(attendanceResponse);
      this.records = fetchedRecords.length >= 8
        ? this.normalizeRecords(fetchedRecords)
        : this.mergeWithMockRecords(fetchedRecords);
      this.summary = this.buildSummary(statsResponse, this.records, fetchedRecords.length < 8);
      this.updateVisibleRecords();
    } catch {
      this.records = this.getMockRecords();
      this.summary = this.calculateSummary(this.records);
      this.updateVisibleRecords();
    }
  }

  private async loadStudentContext(): Promise<void> {
    try {
      const context = await this.sessionService.getMyAttendanceContext().catch(() => null);
      this.className = context?.className || 'فصل 1/أ';
      this.gradeLevel = context?.gradeLevel || 'بوابة الطالب الذكية';
      const todaySessions = this.normalizeSessions(context?.todaySessions || []);
      const fallbackSessions = this.getMockTodaySessions();

      this.todaySessions = todaySessions.length > 0 ? todaySessions : fallbackSessions;
      this.activeSession = this.normalizeSession(context?.activeSession) || this.todaySessions.find(session => session.isActive) || null;
      this.nextSession =
        this.normalizeSession(context?.nextSession) ||
        this.findNextSession(this.todaySessions) ||
        this.getMockNextSession();
    } catch {
      this.activeSession = null;
      this.todaySessions = this.getMockTodaySessions();
      this.nextSession = this.findNextSession(this.todaySessions) || this.getMockNextSession();
      this.className = 'فصل 1/أ';
      this.gradeLevel = 'بوابة الطالب الذكية';
    }

    if (this.nextSession && !this.activeSession) {
      this.startCountdown();
    }
  }

  private extractRecords(response: any): AttendanceRecord[] {
    if (Array.isArray(response)) {
      return response;
    }

    if (Array.isArray(response?.records)) {
      return response.records;
    }

    return [];
  }

  private normalizeRecords(records: AttendanceRecord[]): AttendanceRecord[] {
    return records
      .map((record, index) => this.normalizeRecord(record, index))
      .sort((left, right) =>
        new Date(right.recordedAt || '').getTime() - new Date(left.recordedAt || '').getTime()
      );
  }

  private normalizeRecord(record: AttendanceRecord, index: number): AttendanceRecord {
    const fallbackDate = this.buildDemoDate(index);
    const recordedAt = this.normalizeDate(record.recordedAt, fallbackDate);
    const status = this.resolveStatus(record);

    return {
      sessionId: Number(record.sessionId || index + 1),
      sessionName: record.sessionName,
      subjectName: record.subjectName || record.sessionName || 'حصة دراسية',
      classRoomName: record.classRoomName || this.className || 'فصل 1/أ',
      method: record.method || (status === 'present' ? 'QR' : 'Manual'),
      recordedAt,
      status: status === 'neutral' ? 'Present' : status.charAt(0).toUpperCase() + status.slice(1),
      isPresent: status === 'present' || status === 'late'
    };
  }

  private normalizeSessions(sessions: StudentAttendanceSession[]): StudentAttendanceSession[] {
    return sessions
      .map(session => this.normalizeSession(session))
      .filter((session): session is StudentAttendanceSession => !!session)
      .sort((left, right) =>
        new Date(left.startTime || '').getTime() - new Date(right.startTime || '').getTime()
      );
  }

  private normalizeSession(session?: StudentAttendanceSession | null): StudentAttendanceSession | null {
    if (!session) {
      return null;
    }

    const startTime = this.normalizeDate(session.startTime);
    const endTime = this.normalizeDate(session.endTime, startTime ? new Date(new Date(startTime).getTime() + 45 * 60000).toISOString() : undefined);
    const now = Date.now();
    const startMs = startTime ? new Date(startTime).getTime() : 0;
    const endMs = endTime ? new Date(endTime).getTime() : 0;
    const attendanceRecorded = !!session.attendanceRecorded;
    const attendanceType = session.attendanceType || 'QR';

    return {
      ...session,
      id: Number(session.id || 0),
      title: session.title || session.subjectName || 'حصة دراسية',
      subjectName: session.subjectName || session.title || 'حصة دراسية',
      teacherName: session.teacherName || 'هيئة التدريس',
      classRoomName: session.classRoomName || this.className || 'فصل 1/أ',
      startTime,
      endTime,
      attendanceType,
      isActive: startMs > 0 && endMs > 0 ? startMs <= now && endMs >= now : !!session.isActive,
      isCompleted: endMs > 0 ? endMs < now : !!session.isCompleted,
      attendanceRecorded,
      canMarkWithQr: session.canMarkWithQr ?? (
        startMs > 0 &&
        endMs > 0 &&
        startMs <= now &&
        endMs >= now &&
        !attendanceRecorded &&
        attendanceType.toLowerCase() === 'qr'
      )
    };
  }

  private buildSummary(statsResponse: any, records: AttendanceRecord[], preferCalculated: boolean): AttendanceSummary {
    const calculated = this.calculateSummary(records);
    if (preferCalculated || !statsResponse) {
      return calculated;
    }

    const total = this.toNumber(statsResponse.total, calculated.total);
    const present = this.toNumber(statsResponse.present, calculated.present);
    const absent = this.toNumber(statsResponse.absent, calculated.absent);
    const late = this.toNumber(statsResponse.late, calculated.late);
    const attendanceRate = this.toNumber(
      statsResponse.attendanceRate,
      total > 0 ? Number((((present + late) / total) * 100).toFixed(1)) : calculated.attendanceRate
    );

    return {
      total,
      present,
      absent,
      late,
      attendanceRate
    };
  }

  private calculateSummary(records: AttendanceRecord[]): AttendanceSummary {
    const total = records.length;
    const present = records.filter(record => this.resolveStatus(record) === 'present').length;
    const late = records.filter(record => this.resolveStatus(record) === 'late').length;
    const absent = records.filter(record => this.resolveStatus(record) === 'absent').length;
    const attendanceRate = total > 0 ? Number((((present + late) / total) * 100).toFixed(1)) : 0;

    return {
      total,
      present,
      absent,
      late,
      attendanceRate
    };
  }

  private mergeWithMockRecords(records: AttendanceRecord[]): AttendanceRecord[] {
    const normalized = this.normalizeRecords(records);
    const mockRecords = this.getMockRecords();
    const seenKeys = new Set(normalized.map(record => `${record.sessionId}-${record.recordedAt}`));
    const merged = [...normalized];

    for (const mockRecord of mockRecords) {
      const key = `${mockRecord.sessionId}-${mockRecord.recordedAt}`;
      if (!seenKeys.has(key)) {
        merged.push(mockRecord);
      }

      if (merged.length >= 12) {
        break;
      }
    }

    return merged.sort((left, right) =>
      new Date(right.recordedAt || '').getTime() - new Date(left.recordedAt || '').getTime()
    );
  }

  private getMockRecords(): AttendanceRecord[] {
    const subjects = ['الرياضيات', 'اللغة العربية', 'العلوم', 'اللغة الإنجليزية', 'الدراسات'];
    const statuses: AttendanceStatus[] = ['present', 'present', 'late', 'present', 'absent', 'present', 'present', 'late', 'present', 'absent', 'present', 'present'];

    return statuses.map((status, index) => {
      const recordedAt = this.buildDemoDate(index);

      return {
        sessionId: 700 + index,
        subjectName: subjects[index % subjects.length],
        classRoomName: 'فصل 1/أ',
        recordedAt,
        method: index % 3 === 0 ? 'QR' : 'Manual',
        status: status.charAt(0).toUpperCase() + status.slice(1),
        isPresent: status !== 'absent'
      };
    });
  }

  private getMockTodaySessions(): StudentAttendanceSession[] {
    const baseDate = new Date();
    const sessionTemplates = [
      { offsetHours: 8, subject: 'الرياضيات', teacher: 'أ. أحمد محروس', type: 'QR' },
      { offsetHours: 9.5, subject: 'العلوم', teacher: 'أ. مي عبدالجواد', type: 'Manual' },
      { offsetHours: 11, subject: 'اللغة العربية', teacher: 'أ. سارة مصطفى', type: 'QR' }
    ];

    return sessionTemplates.map((template, index) => {
      const startTime = new Date(baseDate);
      const [hours, minutes] = String(template.offsetHours).split('.');
      startTime.setHours(Number(hours), minutes ? 30 : 0, 0, 0);
      const endTime = new Date(startTime.getTime() + 45 * 60000);
      const now = Date.now();
      const attendanceRecorded = startTime.getTime() < now && index < 2;

      return {
        id: 900 + index,
        subjectName: template.subject,
        title: template.subject,
        teacherName: template.teacher,
        classRoomName: this.className || 'فصل 1/أ',
        startTime: startTime.toISOString(),
        endTime: endTime.toISOString(),
        attendanceType: template.type,
        isActive: startTime.getTime() <= now && endTime.getTime() >= now,
        isCompleted: endTime.getTime() < now,
        attendanceRecorded,
        attendanceStatus: attendanceRecorded ? 'Present' : undefined,
        attendanceMethod: attendanceRecorded ? template.type : undefined,
        canMarkWithQr: startTime.getTime() <= now && endTime.getTime() >= now && template.type === 'QR' && !attendanceRecorded
      };
    });
  }

  private getMockNextSession(): StudentAttendanceSession {
    const nextStart = new Date();
    nextStart.setDate(nextStart.getDate() + 1);
    nextStart.setHours(8, 0, 0, 0);

    return {
      id: 999,
      subjectName: 'الرياضيات',
      title: 'الرياضيات',
      teacherName: 'أ. أحمد محروس',
      classRoomName: this.className || 'فصل 1/أ',
      startTime: nextStart.toISOString(),
      endTime: new Date(nextStart.getTime() + 45 * 60000).toISOString(),
      attendanceType: 'QR',
      isActive: false,
      isCompleted: false,
      attendanceRecorded: false,
      canMarkWithQr: false
    };
  }

  private findNextSession(sessions: StudentAttendanceSession[]): StudentAttendanceSession | null {
    const now = Date.now();

    return sessions.find(session => !!session.startTime && new Date(session.startTime).getTime() > now) || null;
  }

  private normalizeDate(value?: string, fallback?: string): string | undefined {
    const candidate = value || fallback;
    if (!candidate) {
      return undefined;
    }

    const parsed = new Date(candidate);
    return Number.isNaN(parsed.getTime()) ? fallback : parsed.toISOString();
  }

  private buildDemoDate(index: number): string {
    const date = new Date();
    date.setDate(date.getDate() - Math.floor(index / 2));
    date.setHours(8 + (index % 3), (index % 2) * 20, 0, 0);
    return date.toISOString();
  }

  private resolveStatus(record: AttendanceRecord): AttendanceStatus {
    const status = String(record.status || '').toLowerCase();

    if (status === 'present') {
      return 'present';
    }

    if (status === 'late') {
      return 'late';
    }

    if (status === 'absent') {
      return 'absent';
    }

    if (record.isPresent) {
      return 'present';
    }

    return 'neutral';
  }

  private toNumber(value: unknown, fallback: number): number {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
  }

  private startCountdown(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
    }

    this.timerInterval = setInterval(() => {
      if (!this.nextSession?.startTime) {
        this.countdownMessage = '';
        return;
      }

      const distance = new Date(this.nextSession.startTime).getTime() - Date.now();
      if (distance <= 0) {
        this.countdownMessage = 'بدأت الحصة الآن';
        if (this.timerInterval) {
          clearInterval(this.timerInterval);
        }
        return;
      }

      const totalHours = Math.floor(distance / (1000 * 60 * 60));
      const minutes = Math.floor((distance % (1000 * 60 * 60)) / (1000 * 60));
      const seconds = Math.floor((distance % (1000 * 60)) / 1000);

      this.countdownMessage = totalHours > 0
        ? `تبدأ خلال ${totalHours}س ${minutes}د`
        : `تبدأ خلال ${minutes}د ${seconds}ث`;
    }, 1000);
  }
}
