import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AttendanceService } from '../../../../core/services/attendance.service';
import { SessionService } from '../../../../core/services/session.service';
import { AuthService } from '../../../../core/services/auth.service';

import { QRCodeModule } from 'angularx-qrcode';

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
  ) { }

  async ngOnInit() {
    this.route.queryParams.subscribe(async params => {
      if (params['date']) {
        this.selectedDate = String(params['date']);
      }

      if (params['sessionId']) {
        this.selectedSessionId = Number(params['sessionId']);
        await this.loadSessions();
        this.confirmSession();
      } else {
        await this.loadSessions();
      }
    });
  }

  ngOnDestroy() {
    this.clearTimers();
  }

  async loadSessions() {
    try {
      const user = this.authService.getCurrentUser();
      const teacherId = user?.id;
      const result = await this.sessionService.getTeacherSessions(teacherId, this.selectedDate);
      this.sessions = Array.isArray(result) ? result : (result as any)?.data || [];

      if (this.selectedSessionId) {
        this.selectedSessionData = this.sessions.find(s => s.id === this.selectedSessionId);
      }
      
      console.log(`[QR] Loaded ${this.sessions.length} sessions from DB`);
    } catch (err) {
      console.error('[QR] Failed to load sessions:', err);
      this.errorMessage = 'فشل في تحميل الحصص من قاعدة البيانات';
      this.sessions = [];
    }
  }

  async confirmSession() {
    if (!this.selectedSessionId) return;
    this.loading = true;
    this.errorMessage = '';
    
    try {
      // 1. Generate QR token from the backend
      const res: any = await this.attendanceService.generateQr(this.selectedSessionId);
      this.qrToken = res?.token || res?.Token || '';
      console.log('[QR] Token generated from DB:', this.qrToken ? 'Success' : 'Empty');

      // 2. Fetch current attendance list from the backend
      const attendance: any = await this.attendanceService.getSessionAttendance(this.selectedSessionId);
      this.attendanceList = Array.isArray(attendance) ? attendance : attendance?.data || [];
      console.log(`[QR] Loaded ${this.attendanceList.length} attendance records from DB`);

      this.computeStats();
      this.startQrCountdown();
    } catch (err: any) {
      console.error('[QR] Error:', err);
      this.errorMessage = err?.message || 'فشل في الاتصال بقاعدة البيانات';
    } finally {
      this.loading = false;
    }
  }

  private startQrCountdown() {
    this.clearTimers();
    this.countdown = 300;

    // 1. Tick countdown every second
    this.timer = setInterval(() => {
      this.countdown--;
      if (this.countdown <= 0) {
        this.regenerateQr();
        this.countdown = 300;
      }
    }, 1000);

    // 2. Refresh QR token from DB every 5 seconds (as requested)
    this.autoRefreshTimer = setInterval(async () => {
      await this.regenerateQr();
    }, 5000);

    // 3. Detect scan and refresh QR immediately when presentCount changes
    let lastPresentCount = 0;
    this.listRefreshTimer = setInterval(async () => {
      if (this.selectedSessionId) {
        try {
          const attendance: any = await this.attendanceService.getSessionAttendance(this.selectedSessionId);
          this.attendanceList = Array.isArray(attendance) ? attendance : attendance?.data || [];
          this.computeStats();
          
          if (this.stats.present > lastPresentCount) {
            console.log('[QR] Student scan detected, refreshing QR token now.');
            lastPresentCount = this.stats.present;
            await this.regenerateQr();
          }
        } catch { }
      }
    }, 2000);
  }

  private async regenerateQr() {
    if (!this.selectedSessionId) return;
    try {
      const res: any = await this.attendanceService.generateQr(this.selectedSessionId);
      this.qrToken = res?.token || res?.Token || this.qrToken;
    } catch { }
  }

  private computeStats() {
    this.stats.present = this.attendanceList.filter(a => 
      a.status === 'Present' || a.status === 'حاضر' || a.isPresent === true
    ).length;
    this.stats.absent = this.attendanceList.filter(a => 
      a.status === 'Absent' || a.status === 'غائب'
    ).length;
    this.stats.none = this.attendanceList.filter(a => 
      a.status === 'None' || a.status === 'لم يرصد' || a.status === 'Late'
    ).length;
    this.stats.total = this.attendanceList.length;
  }

  clearTimers() {
    if (this.timer) clearInterval(this.timer);
    if (this.autoRefreshTimer) clearInterval(this.autoRefreshTimer);
    if (this.listRefreshTimer) clearInterval(this.listRefreshTimer);
  }

  async saveAttendance() {
    this.showSuccess = true;
    setTimeout(() => this.showSuccess = false, 3000);
  }

  private getDateInputValue(value: Date): string {
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
