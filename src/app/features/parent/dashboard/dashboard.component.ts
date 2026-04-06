import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import {
  ParentDashboardChild,
  ParentDashboardData,
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
    } catch (error) {
      console.error('Failed to load parent dashboard:', error);
      this.notify.error('تعذر تحميل بيانات اللوحة حاليًا.');
    } finally {
      this.loading = false;
    }
  }

  selectChild(child: ParentDashboardChild): void {
    this.selectedChild = child;
  }

  closeDetails(): void {
    this.selectedChild = null;
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
      case 'Present': return 'حاضر الآن';
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
}
