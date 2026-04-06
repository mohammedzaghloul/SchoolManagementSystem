import { Component, OnInit, OnDestroy, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { LiveSessionService, AgoraConfig } from '../../../core/services/live-session.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
    selector: 'app-live-classroom',
    standalone: true,
    imports: [CommonModule],
    template: `
    <div class="live-container bg-dark text-white min-vh-100 p-0 overflow-hidden d-flex flex-column">
      <!-- Top Bar -->
      <div class="top-bar d-flex justify-content-between align-items-center px-4 py-2 bg-black bg-opacity-50">
        <div class="d-flex align-items-center gap-3">
          <div class="live-badge bg-danger px-2 py-1 rounded-1 fw-bold small animate-pulse">LIVE</div>
          <h6 class="mb-0 fw-bold">{{ sessionTitle }}</h6>
        </div>
        <div class="d-flex align-items-center gap-3">
          <span class="text-muted small">المشاركون: {{ participantCount }}</span>
          <button class="btn btn-danger btn-sm px-4 rounded-pill fw-bold" (click)="leave()">مغادرة</button>
        </div>
      </div>

      <!-- Main Stages -->
      <div class="flex-grow-1 position-relative d-flex">
        <!-- Video Grid -->
        <div class="video-grid w-100 h-100 p-3" #videoContainer>
          <div id="local-player" class="local-player rounded-3 overflow-hidden bg-secondary shadow-lg">
             <div *ngIf="!isCameraOn" class="w-100 h-100 d-flex align-items-center justify-content-center flex-column">
                <i class="fas fa-video-slash fs-1 opacity-25"></i>
                <small class="mt-2">الكاميرا متوقفة</small>
             </div>
          </div>
          <div id="remote-players" class="remote-players"></div>
        </div>

        <!-- Right Side Chat (Optional) -->
        <div class="chat-sidebar bg-black bg-opacity-25 border-start border-white border-opacity-10 d-none d-lg-block" style="width: 300px;">
           <!-- Chat content would go here -->
           <div class="p-3 border-bottom border-white border-opacity-10 h-100">
              <h6 class="fw-bold mb-3">الدردشة المباشرة</h6>
              <div class="messages flex-grow-1 overflow-auto small text-muted">
                 مرحباً بك في الحصة المباشرة...
              </div>
           </div>
        </div>
      </div>

      <!-- Control Bar -->
      <div class="control-bar d-flex justify-content-center gap-4 py-3 bg-black bg-opacity-75">
        <button class="control-btn" [class.active]="isMicOn" (click)="toggleMic()">
          <i class="fas" [ngClass]="isMicOn ? 'fa-microphone' : 'fa-microphone-slash'"></i>
        </button>
        <button class="control-btn" [class.active]="isCameraOn" (click)="toggleCamera()">
          <i class="fas" [ngClass]="isCameraOn ? 'fa-video' : 'fa-video-slash'"></i>
        </button>
        <button class="control-btn btn-danger-soft" (click)="leave()">
          <i class="fas fa-phone-slash text-danger"></i>
        </button>
      </div>
    </div>
  `,
    styles: [`
    .animate-pulse { animation: pulse 2s infinite; }
    @keyframes pulse { 0% { opacity: 1; } 50% { opacity: 0.5; } 100% { opacity: 1; } }
    .video-grid { display: block; position: relative; }
    .local-player { width: 100%; height: 100%; max-width: 100%; max-height: 100%; position: absolute; inset: 0; background: #222; }
    .control-btn {
      width: 50px; height: 50px; border-radius: 50%; border: none; background: rgba(255,255,255,0.1);
      color: white; font-size: 1.2rem; transition: all 0.2s;
    }
    .control-btn:hover { background: rgba(255,255,255,0.2); }
    .control-btn.active { background: #0d6efd; }
    .btn-danger-soft { background: rgba(220, 53, 69, 0.2); }
  `]
})
export class LiveClassroomComponent implements OnInit, OnDestroy {
    sessionId!: number;
    sessionTitle = 'جاري التحميل...';
    participantCount = 1;
    isMicOn = true;
    isCameraOn = true;

    config: AgoraConfig | null = null;
    isTeacher = false;

    constructor(
        private route: ActivatedRoute,
        private router: Router,
        private liveService: LiveSessionService,
        private authService: AuthService
    ) { }

    async ngOnInit() {
        this.sessionId = Number(this.route.snapshot.paramMap.get('id'));
        this.isTeacher = this.authService.isTeacher();

        try {
            if (this.isTeacher) {
                this.config = await this.liveService.startSession(this.sessionId);
            } else {
                this.config = await this.liveService.joinSession(this.sessionId);
            }
            this.initAgora();
        } catch (err) {
            alert('فشل في الانضمام للحصة المباشرة');
            this.router.navigate(['/']);
        }
    }

    ngOnDestroy() {
        this.leave();
    }

    private async initAgora() {
        // Note: Here you would import AgoraRTC from 'agora-rtc-sdk-ng'
        // Since we don't have the SDK installed, we'll log the config for now.
        console.log('Agora Client Initializing with:', this.config);
    }

    toggleMic() { this.isMicOn = !this.isMicOn; }
    toggleCamera() { this.isCameraOn = !this.isCameraOn; }

    async leave() {
        if (this.isTeacher) {
            await this.liveService.endSession(this.sessionId);
        }
        this.router.navigate(['/']);
    }
}
