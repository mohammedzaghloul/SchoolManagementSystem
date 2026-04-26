import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  AdminGradeUploadStatusResponse,
  AdminGradeUploadSubjectStatus,
  AdminGradeUploadTeacherStatus,
  GradeService
} from '../../../core/services/grade.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-grade-upload-status',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './grade-upload-status.component.html',
  styleUrls: ['./grade-upload-status.component.css']
})
export class GradeUploadStatusComponent implements OnInit {
  readonly gradeTypes = ['واجب', 'اختبار', 'مشاركة', 'شفوي', 'تقييم'];

  selectedGradeType = 'واجب';
  selectedDate = this.getDateInputValue(new Date());
  status: AdminGradeUploadStatusResponse | null = null;
  loading = false;
  errorMessage = '';

  constructor(
    private gradeService: GradeService,
    private notify: NotificationService
  ) {}

  async ngOnInit(): Promise<void> {
    await this.loadStatus();
  }

  get teachers(): AdminGradeUploadTeacherStatus[] {
    return this.status?.teachers || [];
  }

  get isCompleted(): boolean {
    return this.status?.status === 'COMPLETED';
  }

  async loadStatus(): Promise<void> {
    this.loading = true;
    this.errorMessage = '';

    try {
      this.status = await this.gradeService.getAdminGradeUploadStatus(
        this.selectedGradeType,
        this.selectedDate
      );
    } catch (error: any) {
      this.status = null;
      this.errorMessage = error?.message || 'تعذر تحميل حالة رفع الدرجات.';
      this.notify.error(this.errorMessage);
    } finally {
      this.loading = false;
    }
  }

  async onFilterChanged(): Promise<void> {
    await this.loadStatus();
  }

  trackByTeacher(_: number, teacher: AdminGradeUploadTeacherStatus): number {
    return teacher.teacherId;
  }

  trackBySubject(_: number, subject: AdminGradeUploadSubjectStatus): number {
    return subject.subjectId;
  }

  private getDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
