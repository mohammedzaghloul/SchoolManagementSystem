import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';

import {
  StudentAssignmentSummary,
  StudentAttendanceSummary,
  StudentDashboard,
  StudentGradeSummary,
  StudentSearchResult
} from '../../../../core/models/student.model';
import { StudentService } from '../../../../core/services/student.service';

type DashboardTab = 'grades' | 'attendance' | 'assignments';
type GradeSort = 'date-desc' | 'score-desc' | 'score-asc';

@Component({
  selector: 'app-admin-student-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './student-dashboard.component.html',
  styleUrls: ['./student-dashboard.component.css']
})
export class StudentDashboardComponent implements OnInit, OnDestroy {
  query = '';
  results: StudentSearchResult[] = [];
  dashboard: StudentDashboard | null = null;
  activeTab: DashboardTab = 'grades';
  gradeFilter = 'all';
  gradeSort: GradeSort = 'date-desc';
  searching = false;
  loadingDashboard = false;
  errorMessage = '';

  private searchTimer: number | undefined;
  private searchRequestId = 0;

  constructor(
    private studentService: StudentService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    const studentId = Number(this.route.snapshot.queryParamMap.get('studentId'));
    if (studentId > 0) {
      void this.loadDashboard(studentId);
    }
  }

  ngOnDestroy(): void {
    if (this.searchTimer) {
      window.clearTimeout(this.searchTimer);
    }
  }

  onSearchInput(): void {
    this.errorMessage = '';

    if (this.searchTimer) {
      window.clearTimeout(this.searchTimer);
    }

    this.searchTimer = window.setTimeout(() => {
      void this.performSearch();
    }, 300);
  }

  onSearchFocus(): void {
    if (this.query.trim().length >= 2 && this.results.length === 0) {
      void this.performSearch();
    }
  }

  async performSearch(): Promise<void> {
    const term = this.query.trim();
    const requestId = ++this.searchRequestId;

    if (term.length < 2) {
      this.results = [];
      this.searching = false;
      return;
    }

    this.searching = true;
    try {
      const results = await this.studentService.searchStudents(term, 10);
      if (requestId === this.searchRequestId) {
        this.results = results;
      }
    } catch (error: any) {
      if (requestId === this.searchRequestId) {
        this.results = [];
        this.errorMessage = error?.message || 'تعذر البحث عن الطلاب.';
      }
    } finally {
      if (requestId === this.searchRequestId) {
        this.searching = false;
      }
    }
  }

  async selectStudent(student: StudentSearchResult): Promise<void> {
    this.query = student.fullName;
    this.results = [];
    await this.loadDashboard(student.id);
  }

  async loadDashboard(studentId: number): Promise<void> {
    this.loadingDashboard = true;
    this.errorMessage = '';
    this.activeTab = 'grades';
    this.gradeFilter = 'all';
    this.gradeSort = 'date-desc';

    try {
      this.dashboard = await this.studentService.getStudentDashboard(studentId);
    } catch (error: any) {
      this.dashboard = null;
      this.errorMessage = error?.message || 'تعذر تحميل لوحة الطالب.';
    } finally {
      this.loadingDashboard = false;
    }
  }

  get subjectOptions(): string[] {
    const subjects = this.dashboard?.grades.map(grade => grade.subjectName).filter(Boolean) || [];
    return Array.from(new Set(subjects)).sort((a, b) => a.localeCompare(b, 'ar'));
  }

  get filteredGrades(): StudentGradeSummary[] {
    const grades = this.dashboard?.grades || [];
    const list = this.gradeFilter === 'all'
      ? [...grades]
      : grades.filter(grade => grade.subjectName === this.gradeFilter);

    return list.sort((left, right) => {
      if (this.gradeSort === 'score-desc') {
        return right.score - left.score;
      }

      if (this.gradeSort === 'score-asc') {
        return left.score - right.score;
      }

      return new Date(right.date).getTime() - new Date(left.date).getTime();
    });
  }

  get attendanceRows(): StudentAttendanceSummary[] {
    return this.dashboard?.attendanceRecords || [];
  }

  get assignmentRows(): StudentAssignmentSummary[] {
    return this.dashboard?.assignments || [];
  }

  get gradeCompletionPercent(): number {
    if (!this.dashboard || this.dashboard.totalSubjects <= 0) {
      return this.dashboard?.gradesCompleted ? 100 : 0;
    }

    return Math.round((this.dashboard.approvedSubjects / this.dashboard.totalSubjects) * 100);
  }

  setTab(tab: DashboardTab): void {
    this.activeTab = tab;
  }

  trackBySearchResult(_: number, student: StudentSearchResult): number {
    return student.id;
  }

  trackByGrade(_: number, grade: StudentGradeSummary): number {
    return grade.id;
  }

  trackByAttendance(_: number, attendance: StudentAttendanceSummary): number {
    return attendance.id;
  }

  trackByAssignment(_: number, assignment: StudentAssignmentSummary): number {
    return assignment.id;
  }

  scoreClass(score: number): string {
    if (score >= 85) return 'excellent';
    if (score >= 70) return 'good';
    if (score >= 50) return 'mid';
    return 'low';
  }

  gradeDisplay(grade: StudentGradeSummary): string {
    if (!grade.isGraded) {
      return 'Not Graded';
    }

    if (grade.rawScore !== null && typeof grade.rawScore !== 'undefined' && grade.maxScore) {
      return `${grade.rawScore} / ${grade.maxScore}`;
    }

    return this.formatPercent(grade.score);
  }

  formatPercent(value: number | null | undefined): string {
    const safeValue = Number.isFinite(value) ? Number(value) : 0;
    return `${safeValue.toFixed(1)}%`;
  }

  attendanceStatusLabel(record: StudentAttendanceSummary | null | undefined): string {
    if (!record) {
      return 'غير مسجل';
    }

    const status = (record.status || '').toLowerCase();
    if (record.isPresent || status.includes('present')) {
      return 'حاضر';
    }

    if (status.includes('late')) {
      return 'متأخر';
    }

    return 'غائب';
  }

  attendanceClass(record: StudentAttendanceSummary): string {
    const status = (record.status || '').toLowerCase();
    if (record.isPresent || status.includes('present')) return 'present';
    if (status.includes('late')) return 'late';
    return 'absent';
  }

  assignmentClass(assignment: StudentAssignmentSummary): string {
    if (assignment.isSubmitted) return 'submitted';
    if (assignment.isLate) return 'late';
    return 'pending';
  }
}
