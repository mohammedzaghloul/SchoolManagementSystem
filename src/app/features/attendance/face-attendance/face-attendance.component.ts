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
  statusMessage = 'النظام جاهز لتشغيل بصمة الوجه.';
  stream: MediaStream | null = null;

  sessions: TeacherSessionOption[] = [];
  selectedSessionId: number | null = null;
  selectedDate = this.getDateInputValue(new Date());
  recognizedStudent: RecognizedStudentCard | null = null;
  attendanceLog: FaceLogEntry[] = [];

  private liveScanTimer: ReturnType<typeof setInterval> | null = null;

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
    this.stopCamera();
  }

  get selectedSession(): TeacherSessionOption | undefined {
    return this.sessions.find(session => session.id === this.selectedSessionId);
  }

  get selectedSessionLabel(): string {
    if (!this.selectedSession) {
      return 'لم يتم اختيار الحصة بعد';
    }

    return `${this.selectedSession.subjectName} - ${this.selectedSession.classRoomName}`;
  }

  get selectedSessionTimeLabel(): string {
    if (!this.selectedSession?.startTime) {
      return 'حدد الحصة لتفعيل الرصد';
    }

    const start = this.formatTime(this.selectedSession.startTime);
    const end = this.selectedSession.endTime ? this.formatTime(this.selectedSession.endTime) : '';
    return end ? `${start} - ${end}` : start;
  }

  get cameraButtonLabel(): string {
    return this.isCameraOn ? 'إغلاق الكاميرا' : 'تشغيل الكاميرا';
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
      this.setStatus('danger', 'تعذر تحميل حصص اليوم. حاول تحديث الصفحة أو استخدم الرصد اليدوي مؤقتًا.');
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
      this.setStatus('danger', 'هذا المتصفح لا يدعم تشغيل الكاميرا.');
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
      this.setStatus('neutral', 'الكاميرا تعمل الآن. وجّه وجه الطالب داخل الإطار ثم ابدأ الرصد.');
      this.startLiveScanning();
    } catch (error) {
      console.error('[FaceID] Failed to start camera:', error);
      this.setStatus('danger', 'تعذر تشغيل الكاميرا. تأكد من السماح بالوصول للكاميرا من المتصفح.');
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
    this.setStatus('neutral', 'الكاميرا متوقفة. يمكنك تشغيلها مرة أخرى عند الحاجة.');
  }

  async captureAndScan(trigger: 'manual' | 'live' = 'manual'): Promise<void> {
    if (!this.selectedSessionId) {
      this.setStatus('warning', 'اختر الحصة أولًا قبل بدء رصد الحضور.');
      return;
    }

    if (!this.isCameraOn || !this.videoElement || !this.canvasElement || this.isScanning) {
      return;
    }

    const video = this.videoElement.nativeElement;
    if (!video.videoWidth || !video.videoHeight) {
      if (trigger === 'manual') {
        this.setStatus('warning', 'الكاميرا ما زالت تجهز الصورة. جرّب بعد ثانية واحدة.');
      }
      return;
    }

    this.isScanning = true;
    this.recognizedStudent = null;
    this.setStatus('warning', 'جارٍ تحليل الصورة والتأكد من هوية الطالب...');

    const canvas = this.canvasElement.nativeElement;
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;

    const context = canvas.getContext('2d');
    if (!context) {
      this.isScanning = false;
      this.setStatus('danger', 'تعذر تجهيز إطار الصورة للرصد.');
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
        const studentName = result.studentName || result.name || 'طالب';

        this.recognizedStudent = {
          name: studentName,
          id: result.studentId || result.id,
          confidence: Number(result.confidence || 0),
          alreadyPresent
        };

        this.prependLog({
          name: studentName,
          note: alreadyPresent ? 'مسجل بالفعل' : 'تم الرصد ببصمة الوجه',
          time: new Date(),
          tone: alreadyPresent ? 'warning' : 'success'
        });

        const activeSession = this.sessions.find(session => session.id === this.selectedSessionId);
        if (activeSession && !alreadyPresent) {
          activeSession.attendanceCount = Number(activeSession.attendanceCount || 0) + 1;
          activeSession.isRecorded = true;
        }

        this.setStatus(
          alreadyPresent ? 'warning' : 'success',
          result.message || (alreadyPresent ? 'تم العثور على الطالب لكنه مسجل بالفعل في هذه الحصة.' : 'تم تسجيل الحضور بنجاح.')
        );
      } else {
        this.setStatus('danger', result?.message || 'تعذر التعرف على الوجه. حاول بصورة أوضح أو انتقل إلى الرصد اليدوي.');
      }
    } catch (error: any) {
      console.error('[FaceID] Scan failed:', error);
      const message = error?.error?.message || error?.message || 'حدث خطأ أثناء الاتصال بخدمة بصمة الوجه.';
      this.setStatus('danger', message);
    } finally {
      this.isScanning = false;
    }
  }

  formatTime(value?: string): string {
    if (!value) {
      return '—';
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

  private getDateInputValue(value: Date): string {
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
