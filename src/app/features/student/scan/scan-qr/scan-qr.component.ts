import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AttendanceService } from '../../../../core/services/attendance.service';
import { SessionService } from '../../../../core/services/session.service';
import { Html5Qrcode } from 'html5-qrcode';
import { NotificationService } from '../../../../core/services/notification.service';

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
  attendanceRecorded?: boolean;
  attendanceStatus?: string;
  attendanceMethod?: string;
  canMarkWithQr?: boolean;
}

@Component({
  selector: 'app-scan-qr',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './scan-qr.component.html',
  styleUrls: ['./scan-qr.component.css']
})
export class ScanQrComponent implements OnInit, OnDestroy {
  scanning = false;
  cameraActive = false;
  success = false;
  error = '';
  loading = true;
  activeSession: StudentAttendanceSession | null = null;
  nextSession: StudentAttendanceSession | null = null;
  lastAttendance: any = null;

  private html5QrCode: Html5Qrcode | null = null;

  constructor(
    private attendanceService: AttendanceService,
    private sessionService: SessionService,
    private notify: NotificationService
  ) { }

  async ngOnInit() {
    await Promise.all([
      this.loadAttendanceContext(),
      this.loadLastAttendance()
    ]);
  }

  ngOnDestroy() {
    this.stopScanner();
  }

  async loadAttendanceContext() {
    try {
      const context = await this.sessionService.getMyAttendanceContext();
      this.activeSession = context?.activeSession || null;
      this.nextSession = context?.nextSession || null;
    } catch {
      this.activeSession = null;
      this.nextSession = null;
    }
  }

  async loadLastAttendance() {
    try {
      const stats: any = await this.attendanceService.getMyStats().catch(() => null);
      this.lastAttendance = stats;
    } finally {
      this.loading = false;
    }
  }

  async toggleScanner() {
    if (this.cameraActive) {
      this.stopScanner();
      return;
    }

    if (!this.canStartScanner) {
      this.error = this.scannerBlockedMessage;
      return;
    }

    this.startScanner();
  }

  async startScanner() {
    this.error = '';
    this.success = false;
    this.cameraActive = true;

    setTimeout(() => {
      this.html5QrCode = new Html5Qrcode('reader');
      const config = { fps: 10, qrbox: { width: 250, height: 250 } };

      this.html5QrCode.start(
        { facingMode: 'environment' },
        config,
        (decodedText) => {
          this.onQrScanned(decodedText);
          this.stopScanner();
        },
        () => {
          // Ignore transient scan errors while camera is active.
        }
      ).catch(() => {
        this.error = 'فشل فتح الكاميرا. تأكد من إعطاء الصلاحية.';
        this.cameraActive = false;
      });
    }, 100);
  }

  async stopScanner() {
    if (this.html5QrCode) {
      try {
        await this.html5QrCode.stop();
        this.html5QrCode.clear();
      } catch {
        // Ignore cleanup failures when scanner is already stopped.
      }

      this.html5QrCode = null;
    }

    this.cameraActive = false;
  }

  async onQrScanned(qrToken: string) {
    this.scanning = true;
    this.error = '';

    try {
      await this.attendanceService.markQR({
        qrToken,
        deviceId: this.getDeviceId()
      });

      this.success = true;
      this.notify.success('تم تسجيل حضورك بنجاح.');

      await Promise.all([
        this.loadAttendanceContext(),
        this.loadLastAttendance()
      ]);
    } catch (err: any) {
      this.error = err?.error?.message || err?.message || 'رمز QR غير صحيح أو منتهي الصلاحية.';
      this.notify.error(this.error);
    } finally {
      this.scanning = false;
    }
  }

  private getDeviceId(): string {
    let id = localStorage.getItem('device_id');
    if (!id) {
      id = crypto.randomUUID();
      localStorage.setItem('device_id', id);
    }

    return id;
  }

  get canStartScanner(): boolean {
    return !!this.activeSession?.canMarkWithQr && !this.scanning;
  }

  get scannerBlockedMessage(): string {
    if (this.activeSession?.attendanceRecorded) {
      return 'تم تسجيل حضورك بالفعل في الحصة الحالية.';
    }

    if (this.activeSession && !this.activeSession.canMarkWithQr) {
      return `الحصة الحالية لا تدعم QR. طريقة الرصد: ${this.getAttendanceTypeLabel(this.activeSession.attendanceType)}`;
    }

    if (this.nextSession?.startTime) {
      return `لا توجد حصة نشطة الآن. أقرب حصة تبدأ ${new Date(this.nextSession.startTime).toLocaleString('ar-EG')}.`;
    }

    return 'لا توجد حصة نشطة متاحة للتسجيل الآن.';
  }

  get statusMessageText(): string {
    if (this.activeSession?.canMarkWithQr) {
      return 'وجّه الكاميرا إلى رمز QR الخاص بالحصة النشطة.';
    }

    return this.scannerBlockedMessage;
  }

  getAttendanceTypeLabel(type?: string): string {
    switch (String(type || '').toLowerCase()) {
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
}
