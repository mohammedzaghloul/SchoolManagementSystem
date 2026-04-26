import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { StudentService } from '../../../../core/services/student.service';
import { NotificationService } from '../../../../core/services/notification.service';

@Component({
    selector: 'app-train-face',
    standalone: true,
    imports: [CommonModule, RouterModule],
    template: `
    <div class="container-fluid py-4" dir="rtl">
      <div class="row justify-content-center">
        <div class="col-12 col-md-8 col-lg-6">
          <div class="card border-0 rounded-4 shadow-sm">
            <div class="card-header bg-transparent border-bottom p-4 d-flex align-items-center gap-3">
              <button class="btn btn-icon btn-light rounded-circle" routerLink="/admin/students">
                <i class="fas fa-arrow-right"></i>
              </button>
              <div>
                <h5 class="fw-bold mb-1">تدريب بصمة الوجه</h5>
                <small class="text-muted" *ngIf="student">للطالب: {{ student.fullName }}</small>
              </div>
            </div>

            <div class="card-body p-4 text-center">
              <div class="camera-container rounded-4 position-relative overflow-hidden mb-4 mx-auto" 
                   [class.success]="isTrained"
                   style="max-width: 500px; min-height: 375px; background: #f8f9fa;">
                
                <video #videoElement [hidden]="!isCameraOn || isTrained" autoplay playsinline 
                       class="w-100 h-100" style="object-fit: cover;"></video>
                
                <div *ngIf="!isCameraOn && !isTrained" class="d-flex flex-column align-items-center justify-content-center h-100 py-5">
                  <i class="fas fa-camera fs-1 opacity-25 mb-3"></i>
                  <p>الكاميرا مغلقة</p>
                </div>

                <div *ngIf="isTrained" class="d-flex flex-column align-items-center justify-content-center h-100 py-5 fade-in">
                  <div class="success-icon mb-3">
                    <i class="fas fa-check-circle text-success" style="font-size: 5rem;"></i>
                  </div>
                  <h4 class="fw-bold text-success">تم التدريب بنجاح!</h4>
                  <p class="text-muted">تم حفظ بصمة الوجه للطالب في قاعدة البيانات</p>
                </div>

                <canvas #canvasElement style="display: none;"></canvas>
                
                <div *ngIf="isCameraOn && !isTrained" class="face-guide"></div>
                <div *ngIf="isUploading" class="upload-overlay">
                  <div class="spinner-border text-light" role="status"></div>
                  <p class="text-light mt-2">جاري المعالجة...</p>
                </div>
              </div>

              <div class="d-flex justify-content-center gap-3">
                <button *ngIf="!isCameraOn && !isTrained" class="btn btn-primary px-4 rounded-3" (click)="startCamera()">
                  <i class="fas fa-video me-2"></i> تشغيل الكاميرا
                </button>
                
                <button *ngIf="isCameraOn && !isTrained" class="btn btn-success px-4 rounded-3" [disabled]="isUploading" (click)="captureAndTrain()">
                  <i class="fas fa-expand me-2"></i> التقاط وتدريب
                </button>

                <button *ngIf="isTrained" class="btn btn-outline-primary px-4 rounded-3" (click)="reset()">
                  <i class="fas fa-redo me-2"></i> إعادة المحاولة
                </button>

                <button *ngIf="isCameraOn && !isTrained" class="btn btn-outline-danger px-4 rounded-3" (click)="stopCamera()">
                  إلغاء
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="success-overlay" *ngIf="showSuccessOverlay">
        <div class="success-content">
          <div class="success-overlay-icon">
            <i class="fas fa-check"></i>
          </div>
          <h2>{{ successOverlayTitle }}</h2>
          <p>{{ successOverlayMessage }}</p>
        </div>
      </div>
    </div>
  `,
    styles: [`
    .camera-container { border: 2px dashed #dee2e6; transition: all 0.3s; }
    .camera-container.success { border: 2px solid #10b981; background: #f0fdf4 !important; }
    .face-guide {
      position: absolute;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      width: 200px;
      height: 260px;
      border: 2px dashed rgba(255,255,255,0.6);
      border-radius: 40%;
      pointer-events: none;
      box-shadow: 0 0 0 1000px rgba(0,0,0,0.3);
    }
    .upload-overlay {
      position: absolute;
      inset: 0;
      background: rgba(0,0,0,0.5);
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      z-index: 10;
    }
    .fade-in { animation: fadeIn 0.5s ease; }
    .success-overlay {
      position: fixed;
      inset: 0;
      background: rgba(255,255,255,0.82);
      backdrop-filter: blur(8px);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1200;
      animation: fadeIn 0.35s ease;
    }
    .success-content {
      width: min(30rem, calc(100% - 2rem));
      padding: 3rem 2rem;
      border-radius: 2rem;
      background: rgba(255,255,255,0.98);
      border: 1px solid rgba(15,23,42,0.08);
      box-shadow: 0 30px 60px rgba(15,23,42,0.16);
      text-align: center;
      animation: liftUp 0.45s cubic-bezier(0.16, 1, 0.3, 1);
    }
    .success-overlay-icon {
      width: 5rem;
      height: 5rem;
      margin: 0 auto 1.25rem;
      border-radius: 50%;
      display: grid;
      place-items: center;
      background: linear-gradient(135deg, #16a34a, #059669);
      color: #fff;
      font-size: 2rem;
      box-shadow: 0 18px 36px rgba(22,163,74,0.28);
    }
    .success-content h2 {
      margin: 0 0 0.75rem;
      color: #172033;
      font-size: clamp(1.9rem, 4vw, 2.4rem);
      font-weight: 900;
    }
    .success-content p {
      margin: 0;
      color: #667085;
      font-size: 1.05rem;
      line-height: 1.9;
    }
    @keyframes fadeIn { from { opacity: 0; transform: scale(0.9); } to { opacity: 1; transform: scale(1); } }
    @keyframes liftUp { from { opacity: 0; transform: translateY(24px) scale(0.96); } to { opacity: 1; transform: translateY(0) scale(1); } }
  `]
})
export class TrainFaceComponent implements OnInit, OnDestroy {
    @ViewChild('videoElement') videoElement!: ElementRef<HTMLVideoElement>;
    @ViewChild('canvasElement') canvasElement!: ElementRef<HTMLCanvasElement>;

    studentId!: number;
    student: any;
    isCameraOn = false;
    isUploading = false;
    isTrained = false;
    stream: MediaStream | null = null;
    showSuccessOverlay = false;
    successOverlayTitle = 'تم الحفظ بنجاح!';
    successOverlayMessage = 'تم تحديث بصمة الطالب في قاعدة البيانات بنجاح.';

    private successOverlayTimer: ReturnType<typeof setTimeout> | null = null;

    constructor(
        private route: ActivatedRoute,
        private router: Router,
        private studentService: StudentService,
        private notification: NotificationService
    ) { }

    async ngOnInit() {
        this.studentId = Number(this.route.snapshot.paramMap.get('id'));
        if (!this.studentId) {
            this.router.navigate(['/admin/students']);
            return;
        }
        await this.loadStudent();
    }

    ngOnDestroy(): void {
        if (this.successOverlayTimer) {
            clearTimeout(this.successOverlayTimer);
            this.successOverlayTimer = null;
        }

        this.stopCamera();
    }

    async loadStudent() {
        try {
            this.student = await this.studentService.getStudentById(this.studentId);
        } catch {
            this.notification.error('فشل في تحميل بيانات الطالب');
            this.router.navigate(['/admin/students']);
        }
    }

    async startCamera() {
        try {
            this.stream = await navigator.mediaDevices.getUserMedia({ video: true });
            if (this.videoElement) {
                this.videoElement.nativeElement.srcObject = this.stream;
            }
            this.isCameraOn = true;
        } catch (err) {
            this.notification.error('لا يمكن الوصول للكاميرا');
        }
    }

    stopCamera() {
        if (this.stream) {
            this.stream.getTracks().forEach(t => t.stop());
            this.stream = null;
        }
        this.isCameraOn = false;
    }

    async captureAndTrain() {
        if (!this.videoElement || !this.canvasElement) return;

        this.isUploading = true;
        const video = this.videoElement.nativeElement;
        const canvas = this.canvasElement.nativeElement;

        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext('2d')?.drawImage(video, 0, 0);

        try {
            const blob = await new Promise<Blob>((resolve) => canvas.toBlob(b => resolve(b!), 'image/jpeg', 0.95));
            const file = new File([blob], `train_${this.studentId}.jpg`, { type: 'image/jpeg' });

            await this.studentService.trainFace(this.studentId, file);

            this.isTrained = true;
            this.stopCamera();
            this.flashSuccessOverlay();
            this.notification.success('تم تدريب الوجه بنجاح');
        } catch (err: any) {
            this.notification.error(err?.message || 'فشل التدريب');
        } finally {
            this.isUploading = false;
        }
    }

    reset() {
        this.isTrained = false;
        this.startCamera();
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
}
