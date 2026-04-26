import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { AttendanceService } from '../../../core/services/attendance.service';
import { AuthService } from '../../../core/services/auth.service';
import { SessionService } from '../../../core/services/session.service';

type StatusTone = 'neutral' | 'success' | 'warning' | 'danger';

interface TeacherSessionOption {
  id: number;
  subjectName: string;
  classRoomName: string;
  gradeName?: string;
  startTime?: string;
  endTime?: string;
  studentCount?: number;
  attendanceCount?: number;
  isRecorded?: boolean;
}

interface RecognizedStudentCard {
  name: string;
  id?: number;
  confidence?: number;
  alreadyPresent?: boolean;
}

interface FaceLogEntry {
  name: string;
  note: string;
  time: Date;
  tone: Exclude<StatusTone, 'neutral' | 'danger'>;
}

@Component({
  selector: 'app-face-attendance',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './face-attendance.component.html',
  styleUrls: ['./face-attendance.component.css']
})
export class FaceAttendanceComponent implements OnInit, OnDestroy {
  @ViewChild('videoElement') videoElement!: ElementRef<HTMLVideoElement>;
  @ViewChild('canvasElement') canvasElement!: ElementRef<HTMLCanvasElement>;

  isCameraOn = false;
  isScanning = false;
  statusTone: StatusTone = 'neutral';
  statusMessage = 'Ø§Ù„Ù†Ø¸Ø§Ù… Ø¬Ø§Ù‡Ø² Ù„ØªØ´ØºÙŠÙ„ Ø¨ØµÙ…Ø© Ø§Ù„ÙˆØ¬Ù‡.';
  stream: MediaStream | null = null;

  sessions: TeacherSessionOption[] = [];
  selectedSessionId: number | null = null;
  selectedDate = this.getDateInputValue(new Date());
  recognizedStudent: RecognizedStudentCard | null = null;
  attendanceLog: FaceLogEntry[] = [];
  showSuccessOverlay = false;
  successOverlayTitle = 'ØªÙ… Ø§Ù„Ø­ÙØ¸ Ø¨Ù†Ø¬Ø§Ø­!';
  successOverlayMessage = 'ØªÙ… ØªØ­Ø¯ÙŠØ« Ø³Ø¬Ù„ Ø­Ø¶ÙˆØ± Ø§Ù„Ø·Ø§Ù„Ø¨ ÙÙŠ Ù‚Ø§Ø¹Ø¯Ø© Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª Ø¨Ù†Ø¬Ø§Ø­.';

  private liveScanTimer: ReturnType<typeof setInterval> | null = null;
  private successOverlayTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private attendanceService: AttendanceService,
    private sessionService: SessionService,
    private authService: AuthService,
    private route: ActivatedRoute
  ) {}

  async ngOnInit(): Promise<void> {
    this.route.queryParams.subscribe(async params => {
      if (params['sessionId']) {
        this.selectedSessionId = Number(params['sessionId']);
      }

      if (params['date']) {
        this.selectedDate = String(params['date']);
      }

      await this.loadSessions();
    });
  }

  ngOnDestroy(): void {
    if (this.successOverlayTimer) {
      clearTimeout(this.successOverlayTimer);
      this.successOverlayTimer = null;
    }

    this.stopCamera();
  }

  get selectedSession(): TeacherSessionOption | undefined {
    return this.sessions.find(session => session.id === this.selectedSessionId);
  }

  get selectedSessionLabel(): string {
    if (!this.selectedSession) {
      return 'Ù„Ù… ÙŠØªÙ… Ø§Ø®ØªÙŠØ§Ø± Ø§Ù„Ø­ØµØ© Ø¨Ø¹Ø¯';
    }

    return `${this.selectedSession.subjectName} - ${this.selectedSession.classRoomName}`;
  }

  get selectedSessionTimeLabel(): string {
    if (!this.selectedSession?.startTime) {
      return 'Ø­Ø¯Ø¯ Ø§Ù„Ø­ØµØ© Ù„ØªÙØ¹ÙŠÙ„ Ø§Ù„Ø±ØµØ¯';
    }

    const start = this.formatTime(this.selectedSession.startTime);
    const end = this.selectedSession.endTime ? this.formatTime(this.selectedSession.endTime) : '';
    return end ? `${start} - ${end}` : start;
  }

  get cameraButtonLabel(): string {
    return this.isCameraOn ? 'Ø¥ØºÙ„Ø§Ù‚ Ø§Ù„ÙƒØ§Ù…ÙŠØ±Ø§' : 'ØªØ´ØºÙŠÙ„ Ø§Ù„ÙƒØ§Ù…ÙŠØ±Ø§';
  }

  get canScan(): boolean {
    return this.isCameraOn && !!this.selectedSessionId && !this.isScanning;
  }

  get confidencePercent(): number {
    return Math.min(100, Math.max(0, Math.round((this.recognizedStudent?.confidence || 0) * 100)));
  }

  get statusToneClass(): string {
    return `tone-${this.statusTone}`;
  }

  async loadSessions(): Promise<void> {
    try {
      const user = this.authService.getCurrentUser();
      const teacherId = user?.id;
      const result = await this.sessionService.getTeacherSessions(teacherId, this.selectedDate);
      this.sessions = Array.isArray(result) ? result : (result as any)?.data || [];

      if (!this.selectedSessionId && this.sessions.length === 1) {
        this.selectedSessionId = this.sessions[0].id;
      }
    } catch (error) {
      console.error('[FaceID] Failed to load sessions:', error);
      this.sessions = [];
      this.setStatus('danger', 'ØªØ¹Ø°Ø± ØªØ­Ù…ÙŠÙ„ Ø­ØµØµ Ø§Ù„ÙŠÙˆÙ…. Ø­Ø§ÙˆÙ„ ØªØ­Ø¯ÙŠØ« Ø§Ù„ØµÙØ­Ø© Ø£Ùˆ Ø§Ø³ØªØ®Ø¯Ù… Ø§Ù„Ø±ØµØ¯ Ø§Ù„ÙŠØ¯ÙˆÙŠ Ù…Ø¤Ù‚ØªÙ‹Ø§.');
    }
  }

  async toggleCamera(): Promise<void> {
    if (this.isCameraOn) {
      this.stopCamera();
      return;
    }

    await this.startCamera();
  }

  async startCamera(): Promise<void> {
    if (!navigator?.mediaDevices?.getUserMedia) {
      this.setStatus('danger', 'Ù‡Ø°Ø§ Ø§Ù„Ù…ØªØµÙØ­ Ù„Ø§ ÙŠØ¯Ø¹Ù… ØªØ´ØºÙŠÙ„ Ø§Ù„ÙƒØ§Ù…ÙŠØ±Ø§.');
      return;
    }

    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: 'user',
          width: { ideal: 1280 },
          height: { ideal: 720 }
        },
        audio: false
      });

      const video = this.videoElement?.nativeElement;
      if (video) {
        video.srcObject = this.stream;
        await video.play().catch(() => undefined);
      }

      this.isCameraOn = true;
      this.recognizedStudent = null;
      this.setStatus('neutral', 'Ø§Ù„ÙƒØ§Ù…ÙŠØ±Ø§ ØªØ¹Ù…Ù„ Ø§Ù„Ø¢Ù†. ÙˆØ¬Ù‘Ù‡ ÙˆØ¬Ù‡ Ø§Ù„Ø·Ø§Ù„Ø¨ Ø¯Ø§Ø®Ù„ Ø§Ù„Ø¥Ø·Ø§Ø± Ø«Ù… Ø§Ø¨Ø¯Ø£ Ø§Ù„Ø±ØµØ¯.');
      this.startLiveScanning();
    } catch (error) {
      console.error('[FaceID] Failed to start camera:', error);
      this.setStatus('danger', 'ØªØ¹Ø°Ø± ØªØ´ØºÙŠÙ„ Ø§Ù„ÙƒØ§Ù…ÙŠØ±Ø§. ØªØ£ÙƒØ¯ Ù…Ù† Ø§Ù„Ø³Ù…Ø§Ø­ Ø¨Ø§Ù„ÙˆØµÙˆÙ„ Ù„Ù„ÙƒØ§Ù…ÙŠØ±Ø§ Ù…Ù† Ø§Ù„Ù…ØªØµÙØ­.');
    }
  }

  stopCamera(): void {
    if (this.liveScanTimer) {
      clearInterval(this.liveScanTimer);
      this.liveScanTimer = null;
    }

    if (this.stream) {
      this.stream.getTracks().forEach(track => track.stop());
      this.stream = null;
    }

    this.isCameraOn = false;
    this.isScanning = false;
    this.setStatus('neutral', 'Ø§Ù„ÙƒØ§Ù…ÙŠØ±Ø§ Ù…ØªÙˆÙ‚ÙØ©. ÙŠÙ…ÙƒÙ†Ùƒ ØªØ´ØºÙŠÙ„Ù‡Ø§ Ù…Ø±Ø© Ø£Ø®Ø±Ù‰ Ø¹Ù†Ø¯ Ø§Ù„Ø­Ø§Ø¬Ø©.');
  }

  async captureAndScan(trigger: 'manual' | 'live' = 'manual'): Promise<void> {
    if (!this.selectedSessionId) {
      this.setStatus('warning', 'Ø§Ø®ØªØ± Ø§Ù„Ø­ØµØ© Ø£ÙˆÙ„Ù‹Ø§ Ù‚Ø¨Ù„ Ø¨Ø¯Ø¡ Ø±ØµØ¯ Ø§Ù„Ø­Ø¶ÙˆØ±.');
      return;
    }

    if (!this.isCameraOn || !this.videoElement || !this.canvasElement || this.isScanning) {
      return;
    }

    const video = this.videoElement.nativeElement;
    if (!video.videoWidth || !video.videoHeight) {
      if (trigger === 'manual') {
        this.setStatus('warning', 'Ø§Ù„ÙƒØ§Ù…ÙŠØ±Ø§ Ù…Ø§ Ø²Ø§Ù„Øª ØªØ¬Ù‡Ø² Ø§Ù„ØµÙˆØ±Ø©. Ø¬Ø±Ù‘Ø¨ Ø¨Ø¹Ø¯ Ø«Ø§Ù†ÙŠØ© ÙˆØ§Ø­Ø¯Ø©.');
      }
      return;
    }

    this.isScanning = true;
    this.recognizedStudent = null;
    this.setStatus('warning', 'Ø¬Ø§Ø±Ù ØªØ­Ù„ÙŠÙ„ Ø§Ù„ØµÙˆØ±Ø© ÙˆØ§Ù„ØªØ£ÙƒØ¯ Ù…Ù† Ù‡ÙˆÙŠØ© Ø§Ù„Ø·Ø§Ù„Ø¨...');

    const canvas = this.canvasElement.nativeElement;
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;

    const context = canvas.getContext('2d');
    if (!context) {
      this.isScanning = false;
      this.setStatus('danger', 'ØªØ¹Ø°Ø± ØªØ¬Ù‡ÙŠØ² Ø¥Ø·Ø§Ø± Ø§Ù„ØµÙˆØ±Ø© Ù„Ù„Ø±ØµØ¯.');
      return;
    }

    context.drawImage(video, 0, 0, canvas.width, canvas.height);

    try {
      const blob = await new Promise<Blob>((resolve, reject) => {
        canvas.toBlob(currentBlob => currentBlob ? resolve(currentBlob) : reject(new Error('blob-failed')), 'image/jpeg', 0.9);
      });

      const file = new File([blob], 'face-attendance.jpg', { type: 'image/jpeg' });
      const result: any = await this.attendanceService.markFace(this.selectedSessionId, file);

      if (result && (result.success || result.recognized || result.studentName)) {
        const alreadyPresent = !!result.alreadyPresent;
        const studentName = result.studentName || result.name || 'Ø·Ø§Ù„Ø¨';

        this.recognizedStudent = {
          name: studentName,
          id: result.studentId || result.id,
          confidence: Number(result.confidence || 0),
          alreadyPresent
        };

        this.prependLog({
          name: studentName,
          note: alreadyPresent ? 'Ù…Ø³Ø¬Ù„ Ø¨Ø§Ù„ÙØ¹Ù„' : 'ØªÙ… Ø§Ù„Ø±ØµØ¯ Ø¨Ø¨ØµÙ…Ø© Ø§Ù„ÙˆØ¬Ù‡',
          time: new Date(),
          tone: alreadyPresent ? 'warning' : 'success'
        });

        const activeSession = this.sessions.find(session => session.id === this.selectedSessionId);
        if (activeSession && !alreadyPresent) {
          activeSession.attendanceCount = Number(activeSession.attendanceCount || 0) + 1;
          activeSession.isRecorded = true;
        }

        if (!alreadyPresent) {
          this.flashSuccessOverlay();
        }

        this.setStatus(
          alreadyPresent ? 'warning' : 'success',
          result.message || (alreadyPresent ? 'ØªÙ… Ø§Ù„Ø¹Ø«ÙˆØ± Ø¹Ù„Ù‰ Ø§Ù„Ø·Ø§Ù„Ø¨ Ù„ÙƒÙ†Ù‡ Ù…Ø³Ø¬Ù„ Ø¨Ø§Ù„ÙØ¹Ù„ ÙÙŠ Ù‡Ø°Ù‡ Ø§Ù„Ø­ØµØ©.' : 'ØªÙ… ØªØ³Ø¬ÙŠÙ„ Ø§Ù„Ø­Ø¶ÙˆØ± Ø¨Ù†Ø¬Ø§Ø­.')
        );
      } else {
        this.setStatus('danger', result?.message || 'ØªØ¹Ø°Ø± Ø§Ù„ØªØ¹Ø±Ù Ø¹Ù„Ù‰ Ø§Ù„ÙˆØ¬Ù‡. Ø­Ø§ÙˆÙ„ Ø¨ØµÙˆØ±Ø© Ø£ÙˆØ¶Ø­ Ø£Ùˆ Ø§Ù†ØªÙ‚Ù„ Ø¥Ù„Ù‰ Ø§Ù„Ø±ØµØ¯ Ø§Ù„ÙŠØ¯ÙˆÙŠ.');
      }
    } catch (error: any) {
      console.error('[FaceID] Scan failed:', error);
      const message = error?.error?.message || error?.message || 'Ø­Ø¯Ø« Ø®Ø·Ø£ Ø£Ø«Ù†Ø§Ø¡ Ø§Ù„Ø§ØªØµØ§Ù„ Ø¨Ø®Ø¯Ù…Ø© Ø¨ØµÙ…Ø© Ø§Ù„ÙˆØ¬Ù‡.';
      this.setStatus('danger', message);
    } finally {
      this.isScanning = false;
    }
  }

  formatTime(value?: string): string {
    if (!value) {
      return 'â€”';
    }

    const date = new Date(value);
    if (!Number.isNaN(date.getTime())) {
      return date.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
    }

    const parts = value.split(':');
    const hours = Number(parts[0] || 0);
    const minutes = Number(parts[1] || 0);
    const normalized = new Date();
    normalized.setHours(hours, minutes, 0, 0);
    return normalized.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
  }

  trackBySession(_: number, session: TeacherSessionOption): number {
    return session.id;
  }

  trackByLog(_: number, entry: FaceLogEntry): string {
    return `${entry.name}-${entry.time.toISOString()}`;
  }

  private startLiveScanning(): void {
    if (this.liveScanTimer) {
      clearInterval(this.liveScanTimer);
    }

    this.liveScanTimer = setInterval(() => {
      if (this.canScan) {
        void this.captureAndScan('live');
      }
    }, 3500);
  }

  private prependLog(entry: FaceLogEntry): void {
    this.attendanceLog = [entry, ...this.attendanceLog].slice(0, 8);
  }

  private setStatus(tone: StatusTone, message: string): void {
    this.statusTone = tone;
    this.statusMessage = message;
  }

  private flashSuccessOverlay(): void {
    if (this.successOverlayTimer) {
      clearTimeout(this.successOverlayTimer);
    }

    this.showSuccessOverlay = true;
    this.successOverlayTimer = setTimeout(() => {
      this.showSuccessOverlay = false;
      this.successOverlayTimer = null;
    }, 2600);
  }

  private getDateInputValue(value: Date): string {
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}

