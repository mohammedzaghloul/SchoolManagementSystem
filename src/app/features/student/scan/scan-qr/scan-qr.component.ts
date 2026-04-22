import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Html5Qrcode } from 'html5-qrcode';

import { AttendanceService } from '../../../../core/services/attendance.service';
import { SessionService } from '../../../../core/services/session.service';
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
  isActive?: boolean;
  attendanceRecorded?: boolean;
  attendanceStatus?: string;
  attendanceMethod?: string;
  canMarkWithQr?: boolean;
  attendanceWindowStatus?: string;
  attendanceWindowLabel?: string;
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
  scanSession: StudentAttendanceSession | null = null;
  nextSession: StudentAttendanceSession | null = null;
  lastAttendance: any = null;

  private html5QrCode: Html5Qrcode | null = null;

  constructor(
    private attendanceService: AttendanceService,
    private sessionService: SessionService,
    private notify: NotificationService
  ) {}

  async ngOnInit(): Promise<void> {
    await Promise.all([
      this.loadAttendanceContext(),
      this.loadLastAttendance()
    ]);
  }

  ngOnDestroy(): void {
    void this.stopScanner();
  }

  async loadAttendanceContext(): Promise<void> {
    try {
      const context = await this.sessionService.getMyAttendanceContext();
      this.activeSession = context?.activeSession || null;
      this.scanSession = context?.scanSession || context?.activeSession || null;
      this.nextSession = context?.nextSession || null;
    } catch {
      this.activeSession = null;
      this.scanSession = null;
      this.nextSession = null;
    }
  }

  async loadLastAttendance(): Promise<void> {
    try {
      const stats: any = await this.attendanceService.getMyStats().catch(() => null);
      this.lastAttendance = stats;
    } finally {
      this.loading = false;
    }
  }

  async toggleScanner(): Promise<void> {
    if (this.cameraActive) {
      await this.stopScanner();
      return;
    }

    if (!this.canStartScanner) {
      this.error = this.scannerBlockedMessage;
      return;
    }

    await this.startScanner();
  }

  async startScanner(): Promise<void> {
    this.error = '';
    this.success = false;
    this.cameraActive = true;

    setTimeout(() => {
      this.html5QrCode = new Html5Qrcode('reader');
      const config = { fps: 10, qrbox: { width: 250, height: 250 } };

      this.html5QrCode.start(
        { facingMode: 'environment' },
        config,
        decodedText => {
          void this.onQrScanned(decodedText);
          void this.stopScanner();
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

  async stopScanner(): Promise<void> {
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

  async onQrScanned(qrToken: string): Promise<void> {
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
    } catch (error: any) {
      this.error = error?.error?.message || error?.message || 'رمز QR غير صحيح أو منتهي الصلاحية.';
      this.notify.error(this.error);
    } finally {
      this.scanning = false;
    }
  }

  get canStartScanner(): boolean {
    return !!this.scanSession?.canMarkWithQr && !this.scanning;
  }

  get displayedSession(): StudentAttendanceSession | null {
    return this.scanSession || this.activeSession;
  }

  get sessionHeadline(): string {
    if (this.scanSession?.canMarkWithQr && !this.scanSession?.isActive) {
      return 'حصة QR متاحة للمسح الآن';
    }

    return this.displayedSession ? 'الحصة النشطة الآن' : 'لا توجد حصة نشطة الآن';
  }

  get sessionAvailabilityNote(): string {
    if (this.scanSession?.canMarkWithQr && !this.scanSession?.isActive) {
      return 'هذه الحصة انتهت بالفعل لكن نافذة الرصد ما زالت مفتوحة.';
    }

    return '';
  }

  get scannerBlockedMessage(): string {
    if (this.scanSession?.attendanceRecorded) {
      return 'تم تسجيل حضورك بالفعل في الحصة المتاحة للمسح الآن.';
    }

    if (this.activeSession && !this.activeSession.canMarkWithQr) {
      return `الحصة الجارية الآن لا تدعم QR. طريقة الرصد الحالية: ${this.getAttendanceTypeLabel(this.activeSession.attendanceType)}`;
    }

    if (this.scanSession && !this.scanSession.canMarkWithQr) {
      return `الحصة المتاحة الآن لا تدعم QR. طريقة الرصد: ${this.getAttendanceTypeLabel(this.scanSession.attendanceType)}`;
    }

    if (this.nextSession?.startTime) {
      return `لا توجد حصة متاحة للمسح الآن. أقرب حصة تبدأ ${new Date(this.nextSession.startTime).toLocaleString('ar-EG')}.`;
    }

    return 'لا توجد حصة متاحة للتسجيل عبر QR الآن.';
  }

  get statusMessageText(): string {
    if (this.scanSession?.canMarkWithQr) {
      if (this.scanSession.isActive) {
        return 'وجّه الكاميرا إلى رمز QR الخاص بالحصة النشطة لتسجيل الحضور مباشرة.';
      }

      return 'نافذة الرصد ما زالت مفتوحة لهذه الحصة. وجّه الكاميرا إلى رمز QR الذي يعرضه المعلم الآن.';
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

  private getDeviceId(): string {
    let id = localStorage.getItem('device_id');
    if (!id) {
      id = crypto.randomUUID();
      localStorage.setItem('device_id', id);
    }

    return id;
  }
}
