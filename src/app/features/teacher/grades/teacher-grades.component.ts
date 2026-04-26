import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  GradeService,
  TeacherGradeSessionOption,
  TeacherSessionGradeStudent,
  TeacherSessionGradebook
} from '../../../core/services/grade.service';
import { NotificationService } from '../../../core/services/notification.service';

interface EditableSessionGradeStudent extends TeacherSessionGradeStudent {
  score: number | null;
  maxScore: number;
}

@Component({
  selector: 'app-teacher-grades',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './teacher-grades.component.html',
  styleUrls: ['./teacher-grades.component.css']
})
export class TeacherGradesComponent implements OnInit {
  sessions: TeacherGradeSessionOption[] = [];
  gradebook: TeacherSessionGradebook | null = null;
  students: EditableSessionGradeStudent[] = [];
  selectedSessionId: number | null = null;
  searchTerm = '';

  loadingSessions = false;
  loadingGradebook = false;
  saving = false;
  approving = false;
  loadErrorMessage = '';
  hasUnsavedChanges = false;

  constructor(
    private gradeService: GradeService,
    private notify: NotificationService
  ) {}

  async ngOnInit(): Promise<void> {
    await this.loadSessions();
  }

  get selectedSession(): TeacherGradeSessionOption | undefined {
    return this.sessions.find(session => session.sessionId === this.selectedSessionId);
  }

  get filteredStudents(): EditableSessionGradeStudent[] {
    const term = this.searchTerm.trim().toLowerCase();
    return this.students.filter(student =>
      !term || student.studentName.toLowerCase().includes(term)
    );
  }

  get totalStudents(): number {
    return this.students.length;
  }

  get enteredGradesCount(): number {
    return this.students.filter(student => student.score !== null).length;
  }

  get pendingGradesCount(): number {
    return Math.max(this.totalStudents - this.enteredGradesCount, 0);
  }

  get entryProgressPercent(): number {
    if (this.totalStudents === 0) {
      return 0;
    }

    return Math.round((this.enteredGradesCount / this.totalStudents) * 100);
  }

  get canSave(): boolean {
    return Boolean(this.gradebook)
      && !this.gradebook!.isLocked
      && !this.saving
      && !this.loadingGradebook
      && this.students.some(student => student.score !== null);
  }

  get canApproveUpload(): boolean {
    return Boolean(this.gradebook)
      && !this.gradebook!.isLocked
      && !this.approving
      && !this.saving
      && this.totalStudents > 0
      && this.pendingGradesCount === 0
      && !this.hasUnsavedChanges;
  }

  get statusLabel(): string {
    if (!this.gradebook) return 'Not Started';
    if (this.gradebook.status === 'Approved') return 'Approved';
    if (this.gradebook.status === 'InProgress') return 'In Progress';
    return 'Not Started';
  }

  async loadSessions(): Promise<void> {
    this.loadingSessions = true;
    this.loadErrorMessage = '';

    try {
      this.sessions = await this.gradeService.getTeacherGradeSessions();
      this.selectedSessionId = this.sessions[0]?.sessionId || null;
      if (this.selectedSessionId) {
        await this.loadGradebook();
      }
    } catch (error: any) {
      this.loadErrorMessage = error?.message || 'تعذر تحميل جلسات الدرجات.';
      this.notify.error(this.loadErrorMessage);
    } finally {
      this.loadingSessions = false;
    }
  }

  async loadGradebook(): Promise<void> {
    if (!this.selectedSessionId) {
      this.gradebook = null;
      this.students = [];
      return;
    }

    this.loadingGradebook = true;
    this.loadErrorMessage = '';

    try {
      this.gradebook = await this.gradeService.getTeacherSessionGradebook(this.selectedSessionId);
      this.students = (this.gradebook.students || []).map(student => ({
        ...student,
        score: student.score ?? null,
        maxScore: student.maxScore || 100
      }));
      this.hasUnsavedChanges = false;
    } catch (error: any) {
      this.gradebook = null;
      this.students = [];
      this.hasUnsavedChanges = false;
      this.loadErrorMessage = error?.message || 'تعذر تحميل كشف الدرجات.';
      this.notify.error(this.loadErrorMessage);
    } finally {
      this.loadingGradebook = false;
    }
  }

  async onSessionChanged(): Promise<void> {
    await this.loadGradebook();
  }

  updateScore(student: EditableSessionGradeStudent, value: string | number | null): void {
    if (value === null || value === '' || typeof value === 'undefined') {
      student.score = null;
      this.hasUnsavedChanges = true;
      return;
    }

    const parsed = Number(value);
    if (!Number.isFinite(parsed)) {
      student.score = null;
      this.hasUnsavedChanges = true;
      return;
    }

    student.score = Math.min(student.maxScore, Math.max(0, parsed));
    this.hasUnsavedChanges = true;
  }

  updateMaxScore(student: EditableSessionGradeStudent, value: string | number | null): void {
    const parsed = Number(value);
    student.maxScore = Number.isFinite(parsed) && parsed > 0 ? parsed : 100;
    if (student.score !== null && student.score > student.maxScore) {
      student.score = student.maxScore;
    }
    this.hasUnsavedChanges = true;
  }

  studentGradeLabel(student: EditableSessionGradeStudent): string {
    if (student.score === null) {
      return 'لم يتم الرصد';
    }

    const maxScore = student.maxScore || 100;
    const percentage = maxScore > 0
      ? Math.round((Number(student.score) / maxScore) * 1000) / 10
      : 0;

    return `${percentage}%`;
  }

  async saveGrades(): Promise<void> {
    if (!this.selectedSessionId || !this.canSave) {
      return;
    }

    this.saving = true;
    try {
      const result = await this.gradeService.saveTeacherSessionGrades({
        sessionId: this.selectedSessionId,
        grades: this.students
          .filter(student => student.score !== null)
          .map(student => ({
            studentId: student.studentId,
            score: Number(student.score),
            maxScore: student.maxScore
          }))
      });

      this.notify.success(result.message || 'تم حفظ الدرجات.');
      await this.loadGradebook();
      await this.loadSessions();
    } catch (error: any) {
      this.notify.error(error?.message || 'تعذر حفظ الدرجات.');
    } finally {
      this.saving = false;
    }
  }

  async approveUpload(): Promise<void> {
    if (!this.selectedSessionId || !this.canApproveUpload) {
      return;
    }

    this.approving = true;
    try {
      const result = await this.gradeService.approveTeacherSession(this.selectedSessionId);
      this.notify.success(result.message || 'تم اعتماد رفع الدرجات.');
      await this.loadGradebook();
      await this.loadSessions();
    } catch (error: any) {
      this.notify.error(error?.message || 'تعذر اعتماد رفع الدرجات.');
    } finally {
      this.approving = false;
    }
  }

  trackByStudent(_: number, student: EditableSessionGradeStudent): number {
    return student.studentId;
  }
}
