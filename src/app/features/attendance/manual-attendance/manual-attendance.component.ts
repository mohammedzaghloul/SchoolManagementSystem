import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { ApiService } from '../../../core/services/api.service';
import { SessionService } from '../../../core/services/session.service';
import { AuthService } from '../../../core/services/auth.service';

interface StudentAttendance {
  id: string;
  name: string;
  isPresent: boolean;
  notes: string;
}

@Component({
  selector: 'app-manual-attendance',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatTableModule,
    MatSelectModule,
    MatCheckboxModule,
    MatIconModule,
    MatSnackBarModule
  ],
  template: `
    <div class="page-container p-4 p-lg-5" dir="rtl">
        <!-- Header -->
        <div class="page-header d-flex flex-column flex-sm-row justify-content-between align-items-center mb-5 gap-3">
            <div class="animate-fade-in">
                <div class="d-flex align-items-center gap-3 mb-1">
                    <div class="header-icon-box bg-primary rounded-4 p-2 shadow-premium">
                        <i class="fas fa-clipboard-user text-white fs-4"></i>
                    </div>
                    <h2 class="page-title mb-0 fw-800">تسجيل الحضور اليدوي</h2>
                </div>
                <p class="text-secondary small mb-0 ps-5">{{ selectedSessionLabel || 'قم باختيار الحصة لبدء رصد غياب و حضور الطلاب' }}</p>
            </div>
            <button class="btn btn-white btn-action-outline px-4 py-2-5 rounded-15 fw-800 shadow-premium transition d-flex align-items-center gap-2"
                    (click)="saveAttendance()" [disabled]="saving || !selectedSessionId">
                <i class="fas fa-save text-primary"></i>
                {{ saving ? 'جاري الحفظ...' : 'حفظ كشف الحضور' }}
            </button>
        </div>

        <!-- Session Selection (If not provided) -->
        <div class="card filter-card border-0 rounded-25 mb-4 animate-scale-in" *ngIf="!selectedSessionId">
            <div class="card-body p-4">
                <div class="row align-items-center">
                    <div class="col-md-6">
                        <label class="form-label fw-800 text-dark mb-2">اختر الحصة للمتابعة:</label>
                        <select class="form-select border-0 bg-light shadow-sm rounded-15" 
                                [(ngModel)]="selectedSessionId" (change)="onSessionSelected()">
                            <option [ngValue]="null" disabled>قائمة الحصص المتاحة اليوم...</option>
                            <option *ngFor="let s of sessions" [value]="s.id">
                                {{ s.subjectName }} - {{ s.classRoomName }}
                            </option>
                        </select>
                    </div>
                </div>
            </div>
        </div>

        <!-- Students List Table -->
        <div class="card exams-table-card shadow-premium animate-fade-in" *ngIf="selectedSessionId">
            <div class="card-body p-0">
                <div *ngIf="loading" class="text-center py-10">
                    <div class="spinner-grow text-primary" role="status" style="width: 3rem; height: 3rem;"></div>
                    <h5 class="mt-4 text-secondary fw-800 opacity-75">جاري مزامنة قائمة الطلاب...</h5>
                </div>

                <div class="table-responsive" *ngIf="!loading && students.length > 0">
                    <table class="table custom-table align-middle mb-0">
                        <thead>
                            <tr>
                                <th class="px-5 text-end">اسم الطالب</th>
                                <th class="text-center">حالة الحضور</th>
                                <th class="text-center">ملاحظات إضافية</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr *ngFor="let student of students" class="transition">
                                <td class="px-5 text-end">
                                    <div class="d-flex align-items-center gap-3">
                                        <div class="avatar-circle-sm bg-primary-soft text-primary fw-800">
                                            {{ student.name.charAt(0) }}
                                        </div>
                                        <div class="fw-800 text-dark-blue">{{ student.name }}</div>
                                    </div>
                                </td>
                                <td class="text-center">
                                    <div class="d-flex justify-content-center gap-2">
                                        <button class="btn btn-attendance" 
                                                [class.active-present]="student.isPresent"
                                                (click)="student.isPresent = true">
                                            <i class="fas fa-check-circle me-1"></i> حاضر
                                        </button>
                                        <button class="btn btn-attendance" 
                                                [class.active-absent]="!student.isPresent"
                                                (click)="student.isPresent = false">
                                            <i class="fas fa-times-circle me-1"></i> غائب
                                        </button>
                                    </div>
                                </td>
                                <td class="text-center px-4">
                                    <input type="text" class="form-control form-control-sm border-0 bg-light rounded-pill px-3" 
                                           placeholder="أضف ملاحظة..." [(ngModel)]="student.notes">
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>

                <div *ngIf="!loading && students.length === 0" class="py-10 text-center animate-fade-in">
                    <div class="mb-4 d-inline-block p-4 bg-light rounded-circle scale-up">
                        <i class="fas fa-users-slash fs-1 text-primary opacity-25"></i>
                    </div>
                    <h4 class="fw-800 text-dark-blue mb-2">لا يوجد طلاب</h4>
                    <p class="text-secondary opacity-75">لا توجد بيانات طلاب مسجلة لهذه الحصة في قاعدة البيانات.</p>
        </div>
        
        <!-- Success Overlay -->
        <div class="success-overlay" *ngIf="showSuccess">
            <div class="success-content shadow-premium animate-scale-in">
                <div class="success-icon mb-3">
                    <i class="fas fa-check-circle fs-1 text-success"></i>
                </div>
                <h4 class="fw-800 text-dark-blue mb-2">تم الحفظ بنجاح!</h4>
                <p class="text-secondary small mb-0">تم تحديث سجل حضور الطلاب في قاعدة البيانات بنجاح</p>
            </div>
        </div>
    </div>
  `,
  styles: [`
    .page-container {
        animation: slideInUp 0.6s cubic-bezier(0.16, 1, 0.3, 1);
        min-height: 100vh;
    }
    
    @keyframes slideInUp { from { opacity: 0; transform: translateY(30px); } to { opacity: 1; transform: translateY(0); } }

    .page-title {
        background: linear-gradient(135deg, #0f172a 0%, #334155 100%);
        -webkit-background-clip: text;
        background-clip: text;
        -webkit-text-fill-color: transparent;
        letter-spacing: -1px;
    }

    .exams-table-card { border-radius: 25px !important; overflow: hidden; border: none !important; }
    
    .custom-table thead th {
        background: #f8fafc;
        color: #64748b;
        text-transform: uppercase;
        font-size: 0.75rem;
        font-weight: 800;
        padding: 1.25rem 1rem;
        border-bottom: 2px solid #f1f5f9;
    }

    .btn-attendance {
        border-radius: 12px;
        padding: 8px 20px;
        font-weight: 800;
        font-size: 0.85rem;
        border: 2px solid transparent;
        background: #f1f5f9;
        color: #64748b;
        transition: all 0.3s;
    }

    .active-present {
        background: #f0fdf4;
        color: #15803d;
        border-color: #bbf7d0;
        box-shadow: 0 4px 12px rgba(21, 128, 61, 0.1);
    }

    .active-absent {
        background: #fef2f2;
        color: #dc2626;
        border-color: #fecaca;
        box-shadow: 0 4px 12px rgba(220, 38, 38, 0.1);
    }

    .avatar-circle-sm {
        width: 38px;
        height: 38px;
        border-radius: 50%;
        display: flex;
        align-items: center;
        justify-content: center;
    }

    .bg-primary-soft { background: rgba(13, 110, 253, 0.1); }
    
    .rounded-15 { border-radius: 15px !important; }
    .rounded-25 { border-radius: 25px !important; }
    
    .shadow-premium {
        box-shadow: 0 10px 30px -5px rgba(0,0,0,0.08) !important;
    }

    /* Success Overlay Styles */
    .success-overlay {
        position: fixed;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        background: rgba(255, 255, 255, 0.8);
        backdrop-filter: blur(8px);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 9999;
        animation: fadeIn 0.4s ease;
    }

    .success-content {
        background: white;
        padding: 3rem;
        border-radius: 30px;
        text-align: center;
        max-width: 400px;
        border: 1px solid #f1f5f9;
        animation: slideInUp 0.5s cubic-bezier(0.16, 1, 0.3, 1);
    }

    .success-icon {
        animation: bounceIn 0.8s cubic-bezier(0.36, 0, 0.66, -0.56) both;
    }

    @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
    @keyframes bounceIn {
        0% { transform: scale(0.3); opacity: 0; }
        50% { transform: scale(1.05); opacity: 1; }
        70% { transform: scale(0.9); }
        100% { transform: scale(1); }
    }
  `]
})
export class ManualAttendanceComponent implements OnInit {
  selectedSessionId: number | null = null;
  selectedSessionLabel = '';
  sessions: any[] = [];

  displayedColumns: string[] = ['name', 'status'];
  students: StudentAttendance[] = [];
  loading = false;
  saving = false;
  showSuccess = false;

  constructor(
    private snackBar: MatSnackBar,
    private api: ApiService,
    private sessionService: SessionService,
    private authService: AuthService,
    private route: ActivatedRoute
  ) { }

  async ngOnInit() {
    // Load sessions from DB first
    await this.loadSessions();

    // Check if sessionId was passed via query params
    this.route.queryParams.subscribe(async params => {
      if (params['sessionId']) {
        this.selectedSessionId = Number(params['sessionId']);
        this.updateSessionLabel();
        await this.loadStudentsForSession();
      }
    });
  }

  async loadSessions() {
    try {
      const user = this.authService.getCurrentUser();
      const teacherId = user?.id;
      const res = await this.sessionService.getTeacherSessions(teacherId);
      this.sessions = Array.isArray(res) ? res : (res as any)?.data || [];
      console.log(`[Manual] Loaded ${this.sessions.length} sessions from DB`);
    } catch (err) {
      console.error('[Manual] Failed to load sessions:', err);
      this.sessions = [];
    }
  }

  updateSessionLabel() {
    const session = this.sessions.find(s => s.id === this.selectedSessionId);
    this.selectedSessionLabel = session ? `${session.subjectName} - ${session.classRoomName}` : '';
  }

  async onSessionSelected() {
    this.updateSessionLabel();
    await this.loadStudentsForSession();
  }

  async loadStudentsForSession() {
    if (!this.selectedSessionId) return;
    this.loading = true;
    
    try {
      // Get students enrolled in the session's classroom
      const res: any = await this.api.get(`/api/Attendance/session/${this.selectedSessionId}`);
      const attendanceRecords = Array.isArray(res) ? res : res?.data || [];
      
      // If there are existing records, show them
      if (attendanceRecords.length > 0) {
        this.students = attendanceRecords.map((r: any) => ({
          id: String(r.studentId),
          name: r.studentName || r.name,
          isPresent: r.status === 'Present',
          notes: ''
        }));
      } else {
        // No attendance yet — try to get students from the classroom
        try {
          const session = this.sessions.find(s => s.id === this.selectedSessionId);
          if (session?.classRoomId) {
            const students: any = await this.api.get(`/api/Students`, { classRoomId: session.classRoomId });
            const data = Array.isArray(students) ? students : students?.data || [];
            this.students = data.map((s: any) => ({
              id: String(s.id),
              name: s.fullName || s.name,
              isPresent: true,
              notes: ''
            }));
          }
        } catch {
          this.students = [];
        }
      }
      
      console.log(`[Manual] Loaded ${this.students.length} students from DB`);
    } catch (err) {
      console.error('[Manual] Failed to load students:', err);
      this.students = [];
      this.snackBar.open('فشل تحميل الطلاب من قاعدة البيانات', 'إغلاق', { duration: 3000 });
    } finally {
      this.loading = false;
    }
  }

  async saveAttendance() {
    if (!this.selectedSessionId) {
      this.snackBar.open('يرجى اختيار الحصة أولاً', 'إغلاق', { duration: 3000 });
      return;
    }

    this.saving = true;
    try {
      await this.api.post('/api/Attendance/manual', {
        classId: String(this.selectedSessionId),
        subjectId: '0',
        records: this.students
      });
      
      this.showSuccess = true;
      setTimeout(() => this.showSuccess = false, 3000);
      
    } catch (err) {
      this.snackBar.open('حدث خطأ أثناء محاولة الحفظ', 'إغلاق', { duration: 3000 });
    } finally {
      this.saving = false;
    }
  }
}

