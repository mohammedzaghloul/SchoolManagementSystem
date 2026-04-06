import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ParentService } from '../../../core/services/parent.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-parent-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './parent-dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class ParentDashboardComponent implements OnInit {
  children: ChildSummary[] = [];
  recentNotifications: Notification[] = [];
  upcomingEvents: Event[] = [];
  pendingPayments: number = 0;
  parentName: string = '';
  parentEmail: string = '';
  parentAddress: string = '';

  constructor(
    private parentService: ParentService,
    private notificationService: NotificationService
  ) { }

  async ngOnInit() {
    await this.loadDashboardData();
    await Promise.all([
      this.loadNotifications(),
      this.loadEvents(),
      this.loadPayments()
    ]);
  }

  async loadDashboardData(): Promise<void> {
    try {
      const data = await this.parentService.getDashboardData();
      this.children = data.children;
      this.parentName = data.parentName;
      this.parentEmail = data.parentEmail;
      this.parentAddress = data.parentAddress;
    } catch (error) {
      console.error('Error loading dashboard data', error);
    }
  }

  async loadNotifications(): Promise<void> {
    this.recentNotifications = await this.notificationService.getRecent();
  }

  async loadEvents(): Promise<void> {
    this.upcomingEvents = await this.parentService.getUpcomingEvents();
  }

  async loadPayments(): Promise<void> {
    this.pendingPayments = await this.parentService.getPendingPayments();
  }

  getAttendanceColor(rate: number): string {
    if (rate >= 90) return '#10B981';
    if (rate >= 75) return '#F59E0B';
    return '#EF4444';
  }
}