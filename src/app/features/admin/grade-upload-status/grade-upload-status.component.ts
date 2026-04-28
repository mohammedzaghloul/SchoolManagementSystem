import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ClassRoom } from '../../../core/models/class.model';
import { GradeLevel } from '../../../core/models/grade.model';
import { Subject } from '../../../core/models/subject.model';
import { ClassRoomService } from '../../../core/services/classroom.service';
import {
  AdminGradeSessionsDashboard,
  GradeService,
  GradeSessionMonitor,
  PublishGradeSessionsRequest
} from '../../../core/services/grade.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SubjectService } from '../../../core/services/subject.service';

@Component({
  selector: 'app-grade-upload-status',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './grade-upload-status.component.html',
  styleUrls: ['./grade-upload-status.component.css']
})
export class GradeUploadStatusComponent implements OnInit {
  readonly gradeTypes = ['Exam', 'Homework', 'Activity'];
  readonly scopes = [
    { value: 'GradeLevel', label: 'مرحلة دراسية كاملة' },
    { value: 'Class', label: 'فصل محدد' },
    { value: 'All', label: 'كل الفصول' }
  ];

  private readonly subjectDisplayNames: Record<string, string> = {
    math: 'الرياضيات',
    arabic: 'اللغة العربية',
    english: 'اللغة الإنجليزية',
    science: 'العلوم'
  };
  private readonly gradeLevelDisplayNames: Record<string, string> = {
    'first grade': 'الصف الأول',
    'second grade': 'الصف الثاني',
    'third grade': 'الصف الثالث'
  };
  private readonly gradeTypeDisplayNames: Record<string, string> = {
    Exam: 'اختبار',
    Homework: 'واجب',
    Activity: 'نشاط'
  };

  classRooms: ClassRoom[] = [];
  gradeLevels: GradeLevel[] = [];
  subjects: Subject[] = [];
  dashboard: AdminGradeSessionsDashboard | null = null;

  publishForm: PublishGradeSessionsRequest = {
    scope: 'GradeLevel',
    classId: null,
    gradeLevelId: null,
    subjectId: 0,
    type: 'Homework',
    date: this.getDateInputValue(new Date()),
    deadline: null
  };

  selectedMonitorType = '';
  selectedMonitorDate = '';
  loadingMeta = false;
  publishing = false;
  loadingDashboard = false;
  errorMessage = '';

  constructor(
    private gradeService: GradeService,
    private classRoomService: ClassRoomService,
    private subjectService: SubjectService,
    private notify: NotificationService
  ) {}

  async ngOnInit(): Promise<void> {
    await Promise.all([
      this.loadMeta(),
      this.loadDashboard()
    ]);
  }

  get sessions(): GradeSessionMonitor[] {
    return [...(this.dashboard?.sessions || [])].sort((first, second) => {
      const statusDifference = this.statusPriority(first.status) - this.statusPriority(second.status);
      if (statusDifference !== 0) return statusDifference;
      return first.teacherName.localeCompare(second.teacherName, 'ar');
    });
  }

  get isCompleted(): boolean {
    return this.dashboard?.globalStatus === 'Completed';
  }

  get completionPercent(): number {
    if (!this.dashboard?.totalSessions) return 0;
    return Math.round((this.dashboard.approvedSessions / this.dashboard.totalSessions) * 100);
  }

  get availableSubjects(): Subject[] {
    const scopedSubjects = this.publishForm.scope === 'Class' && this.publishForm.classId
      ? this.subjects.filter(subject => subject.classRoomId === Number(this.publishForm.classId))
      : this.subjects;

    return this.getUniqueSubjects(scopedSubjects.length ? scopedSubjects : this.subjects);
  }

  get availableGradeLevels(): GradeLevel[] {
    return this.getUniqueGradeLevels(this.gradeLevels);
  }

  async loadMeta(): Promise<void> {
    this.loadingMeta = true;
    try {
      const [classRooms, gradeLevels, subjects] = await Promise.all([
        this.classRoomService.getClassRooms(),
        this.gradeService.getGrades(),
        this.subjectService.getSubjects()
      ]);

      this.classRooms = classRooms || [];
      this.gradeLevels = gradeLevels || [];
      this.subjects = (subjects || []).filter(subject => subject.isActive !== false);
      this.publishForm.gradeLevelId = this.availableGradeLevels[0]?.id || null;
      this.publishForm.classId = this.classRooms[0]?.id || null;
      this.syncSelectedSubject();
    } catch (error: any) {
      this.errorMessage = error?.message || 'تعذر تحميل بيانات النشر.';
      this.notify.error(this.errorMessage);
    } finally {
      this.loadingMeta = false;
    }
  }

  async publishSessions(): Promise<void> {
    if (!this.publishForm.subjectId) {
      this.notify.warning('اختر المادة أولاً.');
      return;
    }

    if (this.publishForm.scope === 'Class' && !this.publishForm.classId) {
      this.notify.warning('اختر الفصل أولاً.');
      return;
    }

    if (this.publishForm.scope === 'GradeLevel' && !this.publishForm.gradeLevelId) {
      this.notify.warning('اختر المرحلة الدراسية أولاً.');
      return;
    }

    this.publishing = true;
    try {
      const result = await this.gradeService.publishGradeSessions({
        ...this.publishForm,
        deadline: this.publishForm.deadline || null
      });
      this.notify.success(`تم نشر ${result.createdCount} جلسة. المكرر: ${result.duplicateCount}.`);
      await this.loadDashboard();
    } catch (error: any) {
      this.notify.error(error?.message || 'تعذر نشر جلسات الدرجات.');
    } finally {
      this.publishing = false;
    }
  }

  async loadDashboard(): Promise<void> {
    this.loadingDashboard = true;
    this.errorMessage = '';
    try {
      this.dashboard = await this.gradeService.getAdminGradeSessionsDashboard(
        this.selectedMonitorType || undefined,
        this.selectedMonitorDate || undefined
      );
    } catch (error: any) {
      this.dashboard = null;
      this.errorMessage = error?.message || 'تعذر تحميل متابعة الدرجات.';
      this.notify.error(this.errorMessage);
    } finally {
      this.loadingDashboard = false;
    }
  }

  async onMonitorFilterChanged(): Promise<void> {
    await this.loadDashboard();
  }

  onPublishScopeChanged(): void {
    this.syncSelectedSubject();
  }

  onPublishClassChanged(): void {
    this.syncSelectedSubject();
  }

  trackBySession(_: number, session: GradeSessionMonitor): number {
    return session.sessionId;
  }

  statusLabel(status: string): string {
    if (status === 'Approved') return 'تم رفع الدرجات';
    if (status === 'InProgress') return 'جاري رفع الدرجات';
    return 'لم يبدأ';
  }

  globalStatusLabel(status?: string): string {
    return status === 'Completed' ? 'تم رفع الدرجات' : 'جاري رفع الدرجات';
  }

  typeLabel(type: string): string {
    return this.gradeTypeDisplayNames[type] || type;
  }

  subjectLabel(subjectName: string): string {
    return this.subjectDisplayNames[this.normalizeKey(subjectName)] || subjectName;
  }

  gradeLevelLabel(gradeLevelName?: string | null): string {
    if (!gradeLevelName) return 'مرحلة غير محددة';
    return this.gradeLevelDisplayNames[this.normalizeKey(gradeLevelName)] || gradeLevelName;
  }

  statusIcon(status: string): string {
    if (status === 'Approved') return 'fa-circle-check';
    if (status === 'InProgress') return 'fa-clock';
    return 'fa-circle-minus';
  }

  sessionHint(session: GradeSessionMonitor): string {
    if (session.status === 'Approved') return 'كل الطلاب لهم درجات والمدرس اعتمد الرفع.';
    if (session.missingGradesCount > 0) return `ناقص إدخال درجات ${session.missingGradesCount} طالب.`;
    return 'الدرجات مكتملة وتحتاج اعتماد المدرس.';
  }

  private syncSelectedSubject(): void {
    const selectedSubjectExists = this.availableSubjects.some(subject => subject.id === Number(this.publishForm.subjectId));
    if (!selectedSubjectExists) {
      this.publishForm.subjectId = this.availableSubjects[0]?.id || 0;
    }
  }

  private getUniqueSubjects(subjects: Subject[]): Subject[] {
    const uniqueSubjects = new Map<string, Subject>();

    for (const subject of subjects) {
      const key = this.normalizeSubjectName(this.subjectLabel(subject.name));
      const existingSubject = uniqueSubjects.get(key);
      if (!existingSubject || this.isCanonicalName(subject.name, this.subjectDisplayNames)) {
        uniqueSubjects.set(key, subject);
      }
    }

    return Array.from(uniqueSubjects.values()).sort((first, second) =>
      this.subjectLabel(first.name).localeCompare(this.subjectLabel(second.name), 'ar')
    );
  }

  private getUniqueGradeLevels(gradeLevels: GradeLevel[]): GradeLevel[] {
    const uniqueGradeLevels = new Map<string, GradeLevel>();

    for (const gradeLevel of gradeLevels) {
      const key = this.normalizeSubjectName(this.gradeLevelLabel(gradeLevel.name));
      const existingGradeLevel = uniqueGradeLevels.get(key);
      if (!existingGradeLevel || this.isCanonicalName(gradeLevel.name, this.gradeLevelDisplayNames)) {
        uniqueGradeLevels.set(key, gradeLevel);
      }
    }

    return Array.from(uniqueGradeLevels.values()).sort((first, second) =>
      this.gradeLevelLabel(first.name).localeCompare(this.gradeLevelLabel(second.name), 'ar')
    );
  }

  private normalizeSubjectName(name: string): string {
    return name.trim().replace(/\s+/g, ' ').toLocaleLowerCase('ar');
  }

  private normalizeKey(name: string): string {
    return name.trim().replace(/\s+/g, ' ').toLocaleLowerCase('en');
  }

  private statusPriority(status: string): number {
    if (status === 'NotStarted') return 0;
    if (status === 'InProgress') return 1;
    if (status === 'Approved') return 2;
    return 3;
  }

  private isCanonicalName(name: string, labels: Record<string, string>): boolean {
    return Object.prototype.hasOwnProperty.call(labels, this.normalizeKey(name));
  }

  private getDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
