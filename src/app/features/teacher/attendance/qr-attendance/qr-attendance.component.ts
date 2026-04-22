import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { QRCodeModule } from 'angularx-qrcode';

import { AttendanceService } from '../../../../core/services/attendance.service';
import { SessionService } from '../../../../core/services/session.service';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
  selector: 'app-qr-attendance',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, QRCodeModule],
  templateUrl: './qr-attendance.component.html',
  styleUrls: ['./qr-attendance.component.css']
})
export class QrAttendanceComponent implements OnInit, OnDestroy {
  sessions: any[] = [];
  selectedSessionId: number | null = null;
  selectedSessionData: any = null;
  selectedDate = this.getDateInputValue(new Date());
  qrToken = '';
  countdown = 300;
  attendanceList: any[] = [];
  stats = { present: 0, absent: 0, none: 0, total: 0 };
  loading = false;
  errorMessage = '';
  warningMessage = '';
  showSuccess = false;

  private timer: any;
  private autoRefreshTimer: any;
  private listRefreshTimer: any;

  constructor(
    private attendanceService: AttendanceService,
    private sessionService: SessionService,
    private authService: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  get availableQrSessions(): any[] {
    return this.sessions.filter(session => this.isQrSession(session));
  }

  async ngOnInit(): Promise<void> {
    this.route.queryParams.subscribe(async params => {
      this.clearTimers();
      this.warningMessage = '';
      this.errorMessage = '';

      if (params['date']) {
        this.selectedDate = String(params['date']);
      }

      this.selectedSessionId = params['sessionId'] ? Number(params['sessionId']) : null;

      await this.loadSessions();
      this.resolveSelectedSession();

      if (this.selectedSessionId) {
        await this.confirmSession();
      }
    });
  }

  ngOnDestroy(): void {
    this.clearTimers();
  }

  async loadSessions(): Promise<void> {
    try {
      const user = this.authService.getCurrentUser();
      const teacherId = user?.id;
      const result = await this.sessionService.getTeacherSessions(teacherId, this.selectedDate);
      this.sessions = Array.isArray(result) ? result : (result as any)?.data || [];
      this.selectedSessionData = this.selectedSessionId
        ? this.sessions.find(session => session.id === this.selectedSessionId) || null
        : null;
    } catch (error: any) {
      console.error('[QR] Failed to load sessions:', error);
      this.sessions = [];
      this.selectedSessionData = null;
      this.errorMessage = error?.message || 'تعذر تحميل حصص اليوم من قاعدة البيانات.';
    }
  }

  async confirmSession(): Promise<void> {
    if (!this.selectedSessionId) {
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.qrToken = '';
    this.selectedSessionData = this.sessions.find(session => session.id === this.selectedSessionId) || this.selectedSessionData;

    if (!this.selectedSessionData || !this.isQrSession(this.selectedSessionData)) {
      this.attendanceList = [];
      this.computeStats();
      this.loading = false;
      this.errorMessage = 'هذه الحصة ليست مضبوطة على QR. اختر حصة QR من القائمة.';
      return;
    }

    try {
      const qrResponse: any = await this.attendanceService.generateQr(this.selectedSessionId);
      this.qrToken = qrResponse?.token || qrResponse?.Token || '';

      const attendanceResponse: any = await this.attendanceService.getSessionAttendance(this.selectedSessionId);
      this.attendanceList = Array.isArray(attendanceResponse) ? attendanceResponse : attendanceResponse?.data || [];

      this.computeStats();
      this.startQrCountdown();
    } catch (error: any) {
      console.error('[QR] Error while preparing QR attendance:', error);
      this.errorMessage = error?.message || 'تعذر تجهيز بث رمز QR الآن.';
      this.attendanceList = [];
      this.computeStats();
      this.clearTimers();
    } finally {
      this.loading = false;
    }
  }

  async selectSession(session: any): Promise<void> {
    this.selectedSessionId = Number(session?.id || 0) || null;
    this.selectedSessionData = session || null;
    this.warningMessage = '';
    this.syncQueryParams();
    await this.confirmSession();
  }

  async saveAttendance(): Promise<void> {
    this.showSuccess = true;
    setTimeout(() => this.showSuccess = false, 3000);
  }

  clearTimers(): void {
    if (this.timer) {
      clearInterval(this.timer);
    }

    if (this.autoRefreshTimer) {
      clearInterval(this.autoRefreshTimer);
    }

    if (this.listRefreshTimer) {
      clearInterval(this.listRefreshTimer);
    }
  }

  private resolveSelectedSession(): void {
    const requestedSession = this.selectedSessionId
      ? this.sessions.find(session => session.id === this.selectedSessionId) || null
      : null;

    if (requestedSession && this.isQrSession(requestedSession)) {
      this.selectedSessionData = requestedSession;
      return;
    }

    const fallbackSession = this.pickPreferredQrSession();
    if (fallbackSession) {
      if (requestedSession && !this.isQrSession(requestedSession)) {
        this.warningMessage = 'الحصة المطلوبة ليست QR، لذلك فتحنا أقرب حصة QR متاحة للرصد.';
      }

      this.selectedSessionId = fallbackSession.id;
      this.selectedSessionData = fallbackSession;
      this.syncQueryParams();
      return;
    }

    this.selectedSessionData = requestedSession;

    if (requestedSession) {
      this.errorMessage = 'لا توجد حصة QR متاحة لهذا التاريخ. اختر يومًا آخر أو جهّز حصة QR من الجدول.';
    }
  }

  private pickPreferredQrSession(): any | null {
    if (!this.availableQrSessions.length) {
      return null;
    }

    return [...this.availableQrSessions].sort((first, second) => {
      const scoreDifference = this.getQrSessionScore(second) - this.getQrSessionScore(first);
      if (scoreDifference !== 0) {
        return scoreDifference;
      }

      return this.parseSessionTime(second.startTime).getTime() - this.parseSessionTime(first.startTime).getTime();
    })[0] || null;
  }

  private getQrSessionScore(session: any): number {
    let score = 0;

    if (session?.canRecordAttendance) {
      score += 100;
    }

    if (session?.needsAttention) {
      score += 30;
    }

    const studentCount = Number(session?.studentCount || 0);
    const attendanceCount = Number(session?.attendanceCount || 0);
    score += Math.min(Math.max(studentCount - attendanceCount, 0), 10);

    return score;
  }

  private isQrSession(session: any): boolean {
    return String(session?.attendanceType || '').toLowerCase() === 'qr';
  }

  private syncQueryParams(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        sessionId: this.selectedSessionId,
        date: this.selectedDate
      },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  private startQrCountdown(): void {
    this.clearTimers();
    this.countdown = 300;

    this.timer = setInterval(() => {
      this.countdown--;
      if (this.countdown <= 0) {
        void this.regenerateQr();
        this.countdown = 300;
      }
    }, 1000);

    this.autoRefreshTimer = setInterval(() => {
      void this.regenerateQr();
    }, 5000);

    let lastPresentCount = this.stats.present;
    this.listRefreshTimer = setInterval(async () => {
      if (!this.selectedSessionId) {
        return;
      }

      try {
        const attendanceResponse: any = await this.attendanceService.getSessionAttendance(this.selectedSessionId);
        this.attendanceList = Array.isArray(attendanceResponse) ? attendanceResponse : attendanceResponse?.data || [];
        this.computeStats();

        if (this.stats.present > lastPresentCount) {
          lastPresentCount = this.stats.present;
          await this.regenerateQr();
        }
      } catch {
        // Ignore transient refresh failures while the QR screen is open.
      }
    }, 2000);
  }

  private async regenerateQr(): Promise<void> {
    if (!this.selectedSessionId || !this.selectedSessionData || !this.isQrSession(this.selectedSessionData)) {
      return;
    }

    try {
      const response: any = await this.attendanceService.generateQr(this.selectedSessionId);
      this.qrToken = response?.token || response?.Token || this.qrToken;
    } catch {
      // Ignore token refresh failures; the current token remains visible until the next successful refresh.
    }
  }

  private computeStats(): void {
    this.stats.present = this.attendanceList.filter(attendance =>
      attendance.status === 'Present' || attendance.status === 'حاضر' || attendance.isPresent === true
    ).length;

    this.stats.absent = this.attendanceList.filter(attendance =>
      attendance.status === 'Absent' || attendance.status === 'غائب'
    ).length;

    this.stats.none = this.attendanceList.filter(attendance =>
      attendance.status === 'None' ||
      attendance.status === 'Unrecorded' ||
      attendance.status === 'لم يُرصد' ||
      attendance.status === 'Late'
    ).length;

    this.stats.total = this.attendanceList.length;
  }

  private parseSessionTime(time?: string): Date {
    if (!time) {
      return new Date(0);
    }

    const parsed = new Date(time);
    if (!Number.isNaN(parsed.getTime())) {
      return parsed;
    }

    const [hours, minutes, seconds] = time.split(':').map(Number);
    const reference = new Date(`${this.selectedDate}T00:00:00`);
    reference.setHours(hours || 0, minutes || 0, seconds || 0, 0);
    return reference;
  }

  private getDateInputValue(value: Date): string {
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
