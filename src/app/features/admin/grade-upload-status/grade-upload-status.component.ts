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
    { value: 'GradeLevel', label: 'مرحلة دراسية' },
    { value: 'Class', label: 'فصل محدد' },
    { value: 'All', label: 'كل الفصول' }
  ];

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
    return this.dashboard?.sessions || [];
  }

  get isCompleted(): boolean {
    return this.dashboard?.globalStatus === 'Completed';
  }

  get availableSubjects(): Subject[] {
    const scopedSubjects = this.publishForm.scope === 'Class' && this.publishForm.classId
      ? this.subjects.filter(subject => subject.classRoomId === Number(this.publishForm.classId))
      : this.subjects;

    return this.getUniqueSubjects(scopedSubjects.length ? scopedSubjects : this.subjects);
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
      this.publishForm.gradeLevelId = this.gradeLevels[0]?.id || null;
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
      this.notify.warning('اختر المادة أولًا.');
      return;
    }

    if (this.publishForm.scope === 'Class' && !this.publishForm.classId) {
      this.notify.warning('اختر الفصل أولًا.');
      return;
    }

    if (this.publishForm.scope === 'GradeLevel' && !this.publishForm.gradeLevelId) {
      this.notify.warning('اختر المرحلة الدراسية أولًا.');
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
    if (status === 'Approved') return 'Approved';
    if (status === 'InProgress') return 'In Progress';
    return 'Not Started';
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
      const key = this.normalizeSubjectName(subject.name);
      if (!uniqueSubjects.has(key)) {
        uniqueSubjects.set(key, subject);
      }
    }

    return Array.from(uniqueSubjects.values()).sort((first, second) => first.name.localeCompare(second.name, 'ar'));
  }

  private normalizeSubjectName(name: string): string {
    return name.trim().replace(/\s+/g, ' ').toLocaleLowerCase('ar');
  }

  private getDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
