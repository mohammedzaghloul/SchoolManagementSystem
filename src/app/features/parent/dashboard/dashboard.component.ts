import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import {
  ParentDashboardChild,
  ParentDashboardData,
  ParentGradeHistoryItem,
  ParentService
} from '../../../core/services/parent.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-parent-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  loading = true;
  dashboard: ParentDashboardData | null = null;
  selectedChild: ParentDashboardChild | null = null;
  selectedChildGrades: ParentGradeHistoryItem[] = [];
  gradesLoading = false;

  constructor(
    private parentService: ParentService,
    private notify: NotificationService
  ) { }

  async ngOnInit(): Promise<void> {
    await this.loadDashboard();
  }

  async loadDashboard(): Promise<void> {
    this.loading = true;
    try {
      this.dashboard = await this.parentService.getDashboardData();
      const activeChild = this.selectedChild
        ? this.children.find(child => child.id === this.selectedChild?.id)
        : this.children[0];

      if (activeChild) {
        await this.selectChild(activeChild);
      }
    } catch (error) {
      console.error('Failed to load parent dashboard:', error);
      this.notify.error('تعذر تحميل بيانات اللوحة حاليًا.');
    } finally {
      this.loading = false;
    }
  }

  async selectChild(child: ParentDashboardChild): Promise<void> {
    const isSameChild = this.selectedChild?.id === child.id;
    this.selectedChild = child;

    if (isSameChild && this.selectedChildGrades.length > 0) {
      return;
    }

    this.selectedChildGrades = [];
    this.gradesLoading = true;

    try {
      this.selectedChildGrades = await this.parentService.getGradeHistory(child.id, 40);
    } catch (error) {
      console.error('Failed to load child grade history:', error);
      this.notify.error('تعذر تحميل سجل درجات الطالب.');
    } finally {
      this.gradesLoading = false;
    }
  }

  closeDetails(): void {
    this.selectedChild = null;
    this.selectedChildGrades = [];
    this.gradesLoading = false;
  }

  get summary() {
    return this.dashboard?.summary ?? {
      totalChildren: 0,
      averageAttendanceRate: 0,
      averageScore: 0,
      totalAbsences: 0,
      pendingPaymentsAmount: 0,
      pendingInvoicesCount: 0
    };
  }

  get children(): ParentDashboardChild[] {
    return this.dashboard?.children ?? [];
  }

  isSelectedChild(child: ParentDashboardChild): boolean {
    return this.selectedChild?.id === child.id;
  }

  getOpenAssignmentsCount(child: ParentDashboardChild | null): number {
    return (child?.assignments ?? []).filter(assignment => assignment.status !== 'Graded').length;
  }

  getPendingPaymentsCount(child: ParentDashboardChild | null): number {
    return (child?.payments ?? []).filter(payment => payment.status !== 'Paid' || payment.remainingAmount > 0).length;
  }

  getTodayLabel(): string {
    return new Intl.DateTimeFormat('ar-EG', {
      weekday: 'long',
      day: 'numeric',
      month: 'long'
    }).format(new Date());
  }

  getOpenAssignmentsTotal(): number {
    return this.children.reduce((total, child) => total + this.getOpenAssignmentsCount(child), 0);
  }

  getNextAssignmentTitle(): string {
    const assignments = this.children
      .flatMap(child => (child.assignments ?? []).map(assignment => ({ ...assignment, childName: child.fullName })))
      .filter(assignment => assignment.status !== 'Graded')
      .sort((a, b) => new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime());

    if (!assignments.length) {
      return 'لا توجد واجبات مفتوحة حالياً';
    }

    return `${assignments[0].title} - ${assignments[0].childName}`;
  }

  getAttendanceConcernCount(): number {
    return this.children.filter(child => child.latestAttendanceStatus === 'Absent' || child.latestAttendanceStatus === 'Late').length;
  }

  getPresentChildrenCount(): number {
    return this.children.filter(child => child.latestAttendanceStatus === 'Present').length;
  }

  getDailyAttendanceText(): string {
    if (!this.children.length) {
      return 'لا توجد بيانات حضور بعد';
    }

    const concerns = this.getAttendanceConcernCount();
    if (concerns > 0) {
      return `${concerns} طالب يحتاج مراجعة في الحضور`;
    }

    return `${this.getPresentChildrenCount()} من ${this.children.length} حضورهم مطمئن`;
  }

  getNotificationCount(): number {
    return this.summary.pendingInvoicesCount + this.getOpenAssignmentsTotal() + this.getAttendanceConcernCount();
  }

  getOpenAssignmentsHeadline(): string {
    const count = this.getOpenAssignmentsTotal();
    if (count === 0) {
      return '0 واجبات';
    }

    return `${count} واجبات`;
  }

  getOpenAssignmentsStatusText(): string {
    return this.getOpenAssignmentsTotal() === 0 ? 'لا يوجد واجبات' : this.getNextAssignmentTitle();
  }

  getChildClassLabel(child: ParentDashboardChild | null): string {
    if (!child) {
      return 'لا توجد بيانات فصل';
    }

    return `${child.gradeLevel || 'بدون صف'} · ${child.classRoomName || 'بدون فصل'}`;
  }

  getChildPaymentDueTotal(child: ParentDashboardChild | null): number {
    return (child?.payments ?? []).reduce((total, payment) => total + (payment.remainingAmount || 0), 0);
  }

  getProgressValue(value: number | null | undefined): number {
    if (!Number.isFinite(value ?? NaN)) {
      return 0;
    }

    return Math.max(0, Math.min(100, Math.round((value || 0) * 10) / 10));
  }

  getPaymentStatusClass(status: string): string {
    switch (status) {
      case 'Paid': return 'status-good';
      case 'Overdue': return 'status-danger';
      case 'Partial': return 'status-warn';
      default: return 'status-neutral';
    }
  }

  getGradeDisplayValue(grade: ParentGradeHistoryItem): number {
    return this.getProgressValue(grade.percentage || grade.score);
  }

  getParticipationAverage(child: ParentDashboardChild | null): number {
    return this.averageScores((child?.recentGrades ?? [])
      .filter(grade => this.isParticipationType(grade.type || ''))
      .map(grade => grade.score));
  }

  getAssessmentAverage(child: ParentDashboardChild | null): number {
    const assessmentScores = (child?.recentGrades ?? [])
      .filter(grade => !this.isParticipationType(grade.type || ''))
      .map(grade => grade.score);

    return this.averageScores(assessmentScores.length ? assessmentScores : (child?.recentGrades ?? []).map(grade => grade.score));
  }

  getSelectedParticipationAverage(): number {
    return this.averageScores(this.selectedChildGrades
      .filter(grade => this.isParticipationType(grade.gradeType))
      .map(grade => grade.percentage || grade.score));
  }

  getSelectedAssessmentAverage(): number {
    const assessmentScores = this.selectedChildGrades
      .filter(grade => !this.isParticipationType(grade.gradeType))
      .map(grade => grade.percentage || grade.score);

    return this.averageScores(assessmentScores.length
      ? assessmentScores
      : this.selectedChildGrades.map(grade => grade.percentage || grade.score));
  }

  getAttendanceSummaryText(child: ParentDashboardChild | null): string {
    const summary = child?.attendanceSummary;
    if (!summary) {
      return 'لا يوجد رصد';
    }

    return `حضر ${summary.present} مرة · تأخر ${summary.late} · غاب ${summary.absent}`;
  }

  getLatestAttendanceLabel(child: ParentDashboardChild | null): string {
    if (!child?.latestAttendanceAt) {
      return 'لا يوجد رصد حديث';
    }

    return new Intl.DateTimeFormat('ar-EG', {
      day: 'numeric',
      month: 'short',
      hour: 'numeric',
      minute: '2-digit'
    }).format(new Date(child.latestAttendanceAt));
  }

  getShortDate(value?: string | Date | null): string {
    if (!value) {
      return 'لا يوجد تاريخ';
    }

    return new Intl.DateTimeFormat('ar-EG', {
      day: 'numeric',
      month: 'short'
    }).format(new Date(value));
  }

  get selectedAssignments() {
    return this.selectedChild?.assignments ?? [];
  }

  get selectedPayments() {
    return this.selectedChild?.payments ?? [];
  }

  get selectedAttendance() {
    return this.selectedChild?.recentAttendance ?? [];
  }

  get selectedGradeBreakdown() {
    return this.selectedChild?.gradeBreakdown ?? [];
  }

  get parentName(): string {
    return this.dashboard?.parentName || 'ولي الأمر';
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('ar-EG', {
      style: 'currency',
      currency: 'EGP',
      maximumFractionDigits: 0
    }).format(value || 0);
  }

  getAttendanceColor(rate: number): string {
    if (rate >= 90) return '#10b981';
    if (rate >= 75) return '#f59e0b';
    return '#ef4444';
  }

  getChildStatusLabel(status: string): string {
    switch (status) {
      case 'Present': return 'حاضر';
      case 'Late': return 'متأخر';
      case 'Absent': return 'غائب';
      default: return 'لا توجد بيانات';
    }
  }

  getChildStatusClass(status: string): string {
    switch (status) {
      case 'Present': return 'status-good';
      case 'Late': return 'status-warn';
      case 'Absent': return 'status-danger';
      default: return 'status-neutral';
    }
  }

  getAssignmentStatusLabel(status: string): string {
    switch (status) {
      case 'Graded': return 'مقيّم';
      case 'Submitted': return 'تم التسليم';
      case 'Late': return 'متأخر';
      default: return 'مفتوح';
    }
  }

  getAssignmentStatusClass(status: string): string {
    switch (status) {
      case 'Graded': return 'status-good';
      case 'Submitted': return 'status-warn';
      case 'Late': return 'status-danger';
      default: return 'status-neutral';
    }
  }

  getPaymentStatusLabel(status: string): string {
    switch (status) {
      case 'Paid': return 'مسددة';
      case 'Partial': return 'جزئية';
      case 'Overdue': return 'متأخرة';
      default: return 'مستحقة';
    }
  }

  getGradeTypeLabel(type: string): string {
    const value = (type || '').toLowerCase();
    if (value.includes('homework') || value.includes('واجب')) return 'واجب';
    if (value.includes('quiz') || value.includes('اختبار')) return 'اختبار';
    if (value.includes('participation') || value.includes('مشاركة')) return 'نشاط';
    if (value.includes('exam') || value.includes('امتحان')) return 'امتحان';
    return type || 'درجة';
  }

  getGradeCategoryLabel(type: string): string {
    return this.isParticipationType(type) ? 'نشاط' : 'درجة';
  }

  getGradeCategoryClass(type: string): string {
    return this.isParticipationType(type) ? 'category-participation' : 'category-assessment';
  }

  private isParticipationType(type: string): boolean {
    const value = (type || '').toLowerCase();
    return value.includes('participation') || value.includes('مشاركة');
  }

  private averageScores(scores: number[]): number {
    const validScores = scores.filter(score => Number.isFinite(score));
    if (!validScores.length) {
      return 0;
    }

    const total = validScores.reduce((sum, score) => sum + score, 0);
    return Math.round((total / validScores.length) * 10) / 10;
  }
}
