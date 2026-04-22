import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  AdminScheduleOverview,
  AdminScheduleSessionItem,
  GenerateTermScheduleResult,
  SessionService
} from '../../../../core/services/session.service';
import { NotificationService } from '../../../../core/services/notification.service';

@Component({
  selector: 'app-session-schedule-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './session-schedule-management.component.html',
  styleUrls: ['./session-schedule-management.component.css']
})
export class SessionScheduleManagementComponent implements OnInit {
  overview: AdminScheduleOverview | null = null;
  sessions: AdminScheduleSessionItem[] = [];
  loading = false;
  generating = false;
  error = '';
  lastGeneration: GenerateTermScheduleResult | null = null;
  selectedTerm = 'all';
  readonly termOptions = [
    { value: 'all', label: 'كل الترمات' },
    { value: 'الترم الأول', label: 'الترم الأول' },
    { value: 'الترم الثاني', label: 'الترم الثاني' }
  ];

  periodStart = this.toDateInput(new Date());
  periodEnd = this.toDateInput(this.addDays(new Date(), 42));

  constructor(
    private sessionService: SessionService,
    private notificationService: NotificationService
  ) {}

  async ngOnInit(): Promise<void> {
    await this.loadOverview();
  }

  async loadOverview(): Promise<void> {
    this.loading = true;
    this.error = '';

    try {
      this.overview = await this.sessionService.getAdminScheduleOverview(
        this.periodStart,
        this.periodEnd,
        this.selectedTerm
      );
      this.sessions = this.overview.items || [];
    } catch (error: any) {
      console.error('Failed to load schedule overview', error);
      this.error = error?.message || 'تعذر تحميل جدول الحصص الحالي.';
      this.overview = null;
      this.sessions = [];
    } finally {
      this.loading = false;
    }
  }

  async generateSchedule(): Promise<void> {
    this.generating = true;
    this.error = '';

    try {
      this.lastGeneration = await this.sessionService.generateTermSchedule({
        startDate: this.periodStart,
        endDate: this.periodEnd,
        term: this.selectedTerm
      });

      const successMessage = this.lastGeneration.createdCount > 0
        ? `تم تجهيز ${this.lastGeneration.createdCount} حصة ضمن ${this.lastGeneratedTermLabel}.`
        : `${this.lastGeneratedTermLabel} مجهز بالفعل خلال الفترة المختارة.`;

      this.notificationService.success(successMessage);
      await this.loadOverview();
    } catch (error: any) {
      console.error('Failed to generate schedule', error);
      this.error = error?.message || 'تعذر تجهيز جدول الحصص.';
      this.notificationService.error(this.error);
    } finally {
      this.generating = false;
    }
  }

  formatDate(value: string): string {
    return new Date(value).toLocaleDateString('ar-EG', {
      weekday: 'long',
      day: 'numeric',
      month: 'long'
    });
  }

  formatTime(value: string): string {
    return new Date(value).toLocaleTimeString('ar-EG', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: true
    });
  }

  formatTerm(term?: string): string {
    if (!term) {
      return 'غير محدد';
    }

    return this.getTermLabel(term);
  }

  get selectedTermLabel(): string {
    return this.getTermLabel(this.selectedTerm);
  }

  get lastGeneratedTermLabel(): string {
    return this.getTermLabel(this.lastGeneration?.term);
  }

  trackBySession(_: number, session: AdminScheduleSessionItem): number {
    return session.id;
  }

  private toDateInput(value: Date): string {
    const year = value.getFullYear();
    const month = `${value.getMonth() + 1}`.padStart(2, '0');
    const day = `${value.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private addDays(value: Date, days: number): Date {
    const next = new Date(value);
    next.setDate(next.getDate() + days);
    return next;
  }

  private getTermLabel(term?: string): string {
    if (!term || term === 'all' || term === 'الكل') {
      return 'كل الترمات';
    }

    return this.termOptions.find(option => option.value === term)?.label || term;
  }
}
