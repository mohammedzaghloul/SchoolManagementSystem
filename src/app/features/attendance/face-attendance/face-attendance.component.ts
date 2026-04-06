import { Component, OnDestroy, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { AttendanceService } from '../../../core/services/attendance.service';
import { SessionService } from '../../../core/services/session.service';
import { AuthService } from '../../../core/services/auth.service';
import { ApiService } from '../../../core/services/api.service';

@Component({
  selector: 'app-face-attendance',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="face-attendance-simple py-5" dir="rtl">
        <div class="container">
            <!-- العنوان -->
            <div class="mb-5 d-flex align-items-center gap-3">
                <div class="icon-box-simple shadow-sm">
                    <i class="fas fa-camera text-primary"></i>
                </div>
                <div>
                    <h2 class="fw-bold text-dark mb-0">تسجيل الحضور عبر الوجه</h2>
                    <p class="text-muted mb-0">استخدم الكاميرا للتحقق من هوية الطالب وتسجيل حضوره</p>
                </div>
            </div>

            <div class="row g-4">
                <!-- قسم الكاميرا -->
                <div class="col-12 col-lg-7">
                    <div class="card border-0 shadow-sm rounded-4 overflow-hidden">
                        <div class="card-header bg-white p-3 border-bottom d-flex align-items-center justify-content-between">
                            <span class="fw-bold"><i class="fas fa-video me-2 text-primary"></i>معاينة الكاميرا</span>
                            <div class="d-flex align-items-center gap-2" *ngIf="isCameraOn">
                                <span class="spinner-grow spinner-grow-sm text-success" role="status"></span>
                                <small class="text-success fw-bold">الكاميرا تعمل</small>
                            </div>
                        </div>
                        
                        <div class="card-body p-0 bg-light">
                            <div class="camera-wrapper position-relative" [class.scanning]="isScanning">
                                <!-- الكاميرا -->
                                <video #videoElement [hidden]="!isCameraOn" autoplay playsinline class="video-preview"></video>
                                <canvas #canvasElement style="display: none;"></canvas>

                                <!-- في حالة الإغلاق -->
                                <div *ngIf="!isCameraOn" class="w-100 h-100 d-flex flex-column align-items-center justify-content-center py-5 text-muted min-h-400">
                                    <i class="fas fa-video-slash fs-1 mb-3 opacity-25"></i>
                                    <p class="fw-bold">الكاميرا مغلقة حالياً</p>
                                    <button class="btn btn-primary rounded-3 px-4" (click)="toggleCamera()">تشغيل الكاميرا</button>
                                </div>

                                <!-- إطار توضيحي للوجه -->
                                <div *ngIf="isCameraOn" class="face-frame-simple"></div>
                                <div *ngIf="isScanning" class="scan-bar-simple"></div>
                            </div>
                        </div>

                        <!-- أزرار التحكم -->
                        <div class="card-footer bg-white p-4 border-top">
                            <div class="row align-items-center">
                                <div class="col-md-6 mb-3 mb-md-0">
                                    <label class="form-label small fw-bold">اختر الحصة المراد رصدها:</label>
                                    <select class="form-select border-2" [(ngModel)]="selectedSessionId">
                                        <option [ngValue]="null" disabled>قائمة حصص اليوم...</option>
                                        <option *ngFor="let s of sessions" [ngValue]="s.id">
                                            {{ s.subjectName }} - {{ s.classRoomName }}
                                        </option>
                                    </select>
                                </div>
                                <div class="col-md-6 d-flex gap-2 justify-content-md-end">
                                    <button class="btn btn-outline-secondary px-3" (click)="toggleCamera()">
                                        {{ isCameraOn ? 'إغلاق الكاميرا' : 'فتح الكاميرا' }}
                                    </button>
                                    <button class="btn btn-primary px-4 fw-bold shadow-sm" [disabled]="!isCameraOn || isScanning || !selectedSessionId" (click)="captureAndScan()">
                                        <i class="fas fa-id-card me-2"></i> تعرف على الطالب
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- قسم النتائج والسجل -->
                <div class="col-12 col-lg-5">
                    <!-- نتيجة التعرف حالياً -->
                    <div class="alert alert-success border-0 shadow-sm rounded-4 p-4 mb-4" *ngIf="recognizedStudent" class="animate-fade-in">
                        <div class="d-flex align-items-center gap-3">
                            <div class="student-avatar-box">
                                <img [src]="'https://ui-avatars.com/api/?name=' + recognizedStudent.name + '&background=0d6efd&color=fff'" class="rounded-circle shadow-sm" width="60">
                            </div>
                            <div class="flex-grow-1">
                                <h5 class="fw-bold text-dark mb-1">تم التعرف بنجاح</h5>
                                <div class="text-primary fw-bold fs-5">{{ recognizedStudent.name }}</div>
                            </div>
                            <i class="fas fa-check-circle text-success fs-1"></i>
                        </div>
                    </div>

                    <!-- رسالة الحالة -->
                    <div class="alert alert-info border-0 shadow-sm rounded-4 d-flex align-items-center gap-2 mb-4" *ngIf="!recognizedStudent && !isScanning">
                        <i class="fas fa-info-circle"></i> {{ statusMessage }}
                    </div>
                    
                    <div class="alert alert-warning border-0 shadow-sm rounded-4 d-flex align-items-center gap-3" *ngIf="isScanning">
                        <div class="spinner-border spinner-border-sm text-warning"></div>
                        <span class="fw-bold">{{ statusMessage }}</span>
                    </div>

                    <!-- سجل الحضور الأخير -->
                    <div class="card border-0 shadow-sm rounded-4">
                        <div class="card-header bg-white p-3 border-bottom">
                            <span class="fw-bold"><i class="fas fa-list me-2 text-primary"></i>آخر حضور تم رصده</span>
                        </div>
                        <div class="card-body p-0 log-body-simple">
                            <div *ngIf="attendanceLog.length === 0" class="text-center py-5 text-muted">لا توجد سجلات حالياً</div>
                            <div class="list-group list-group-flush">
                                <div *ngFor="let log of attendanceLog" class="list-group-item d-flex justify-content-between align-items-center p-3 animate-fade-in">
                                    <div class="d-flex align-items-center gap-3">
                                        <div class="avatar-sm bg-light text-primary rounded-circle d-flex align-items-center justify-content-center fw-bold">
                                            {{ log.name.charAt(0) }}
                                        </div>
                                        <div>
                                            <div class="fw-bold small text-dark">{{ log.name }}</div>
                                            <div class="tiny text-muted">تم تسجيله بواسطة الوجه</div>
                                        </div>
                                    </div>
                                    <span class="badge bg-light text-muted fw-normal rounded-pill px-3">{{ log.time | date:'shortTime' }}</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
  `,
  styles: [`
    .face-attendance-simple {
        background-color: #f6f8fb;
        min-height: 100vh;
        font-family: 'Inter', 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    }

    .icon-box-simple {
        width: 50px; height: 50px;
        background: white; border-radius: 12px;
        display: flex; align-items: center; justify-content: center;
        font-size: 1.5rem;
    }

    .camera-wrapper {
        background: #000;
        min-height: 400px;
    }

    .min-h-400 { min-height: 400px; }

    .video-preview {
        width: 100%; height: 100%;
        max-height: 500px;
        object-fit: cover;
    }

    .face-frame-simple {
        position: absolute;
        top: 50%; left: 50%;
        transform: translate(-50%, -50%);
        width: 250px; height: 300px;
        border: 2px solid rgba(255, 255, 255, 0.4);
        border-radius: 40%;
        box-shadow: 0 0 0 2000px rgba(0, 0, 0, 0.2);
        pointer-events: none;
    }

    .scan-bar-simple {
        position: absolute;
        width: 100%; height: 2px;
        background: #0d6efd;
        box-shadow: 0 0 8px #0d6efd;
        animation: scanAnim 2s infinite linear;
    }

    @keyframes scanAnim {
        0% { top: 0; }
        100% { top: 100%; }
    }

    .avatar-sm { width: 35px; height: 35px; }

    .log-body-simple {
        max-height: 350px;
        overflow-y: auto;
    }

    .tiny { font-size: 0.7rem; }
    .animate-fade-in { animation: fadeIn 0.3s ease-in; }
    @keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }

    .btn-primary { background-color: #0d6efd; border: none; padding: 12px 24px; border-radius: 12px; }
    .btn-primary:hover { background-color: #0b5ed7; }
    .btn-outline-secondary { border-radius: 12px; border-width: 2px; }
    .form-select { border-radius: 12px; padding: 10px; }
  `]
})
export class FaceAttendanceComponent implements OnInit, OnDestroy {
  @ViewChild('videoElement') videoElement!: ElementRef<HTMLVideoElement>;
  @ViewChild('canvasElement') canvasElement!: ElementRef<HTMLCanvasElement>;

  isCameraOn = false;
  isScanning = false;
  scanResult: 'none' | 'success' | 'error' = 'none';
  statusMessage = 'النظام جاهز للتشغيل';
  stream: MediaStream | null = null;

  sessions: any[] = [];
  selectedSessionId: number | null = null;
  recognizedStudent: any = null;
  attendanceLog: any[] = [];

  constructor(
    private attendanceService: AttendanceService,
    private sessionService: SessionService,
    private authService: AuthService,
    private api: ApiService,
    private route: ActivatedRoute
  ) { }

  async ngOnInit() {
    this.route.queryParams.subscribe(async params => {
      if (params['sessionId']) {
        this.selectedSessionId = Number(params['sessionId']);
      }
      await this.loadSessions();
    });
  }

  ngOnDestroy(): void {
    this.stopCamera();
  }

  async loadSessions() {
    try {
      const user = this.authService.getCurrentUser();
      const teacherId = user?.id;
      const res = await this.sessionService.getTeacherSessions(teacherId);
      this.sessions = Array.isArray(res) ? res : (res as any)?.data || [];
    } catch (err) {
      console.error('[FaceID] Failed to load sessions:', err);
      this.sessions = [];
    }
  }

  async toggleCamera() {
    if (this.isCameraOn) {
      this.stopCamera();
    } else {
      await this.startCamera();
    }
  }

  async startCamera() {
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user' } });
      if (this.videoElement && this.videoElement.nativeElement) {
        this.videoElement.nativeElement.srcObject = this.stream;
      }
      this.isCameraOn = true;
      this.statusMessage = 'يرجى وضع الوجه أمام الكاميرا';
      this.scanResult = 'none';
      this.recognizedStudent = null;
    } catch (error) {
      this.statusMessage = 'مشكلة في تشغيل الكاميرا';
      this.scanResult = 'error';
    }
  }

  stopCamera() {
    if (this.stream) {
      this.stream.getTracks().forEach(track => track.stop());
      this.stream = null;
    }
    this.isCameraOn = false;
    this.statusMessage = 'الكاميرا مغلقة';
    this.scanResult = 'none';
  }

  async captureAndScan() {
    if (!this.isCameraOn || !this.videoElement || !this.canvasElement || !this.selectedSessionId) return;

    this.isScanning = true;
    this.statusMessage = 'جاري التحقق من هوية الطالب...';
    this.scanResult = 'none';
    this.recognizedStudent = null;

    const video = this.videoElement.nativeElement;
    const canvas = this.canvasElement.nativeElement;
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    const context = canvas.getContext('2d');
    if (!context) return;
    context.drawImage(video, 0, 0, canvas.width, canvas.height);

    try {
      const blob = await new Promise<Blob>((resolve, reject) => {
        canvas.toBlob(b => b ? resolve(b) : reject('Failed'), 'image/jpeg', 0.85);
      });
      const file = new File([blob], 'face.jpg', { type: 'image/jpeg' });
      const result: any = await this.attendanceService.markFace(this.selectedSessionId!, file);

      this.isScanning = false;
      if (result && (result.success || result.studentName || result.recognized)) {
        this.scanResult = 'success';
        this.statusMessage = 'تم تسجيل الحضور بنجاح!';
        this.recognizedStudent = {
          name: result.studentName || result.name || 'طالب',
          id: result.studentId || result.id
        };
        this.attendanceLog.unshift({ name: this.recognizedStudent.name, time: new Date() });
      } else {
        this.scanResult = 'error';
        this.statusMessage = result?.message || 'لم يتم التعورف، يرجى المحاولة.';
      }
    } catch (err: any) {
      this.isScanning = false;
      this.scanResult = 'error';
      this.statusMessage = 'خطأ في الاتصال بالنظام';
    }
  }
}
