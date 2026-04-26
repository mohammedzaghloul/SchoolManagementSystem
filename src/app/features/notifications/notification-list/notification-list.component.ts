import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';

import { Notification as AppNotification } from '../../../core/models/notification.model';
import { NotificationCenterService } from '../../../core/services/notification-center.service';

@Component({
  selector: 'app-notification-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './notification-list.component.html',
  styleUrls: ['./notification-list.component.css']
})
export class NotificationListComponent implements OnInit, OnDestroy {
  notifications: AppNotification[] = [];
  unreadCount = 0;
  loading = false;
  page = 1;
  pageSize = 20;
  hasMore = true;

  private readonly subscription = new Subscription();

  constructor(private notificationCenter: NotificationCenterService) {}

  ngOnInit(): void {
    this.subscription.add(
      this.notificationCenter.notifications$.subscribe(items => {
        this.applyNotifications(items);
      })
    );
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  async loadNotifications(): Promise<void> {
    if (this.loading) {
      return;
    }

    this.loading = true;
    try {
      this.applyNotifications(this.notificationCenter.snapshot);
    } catch (error) {
      console.error('Error loading notifications:', error);
    } finally {
      this.loading = false;
    }
  }

  async markAsRead(notificationId: number): Promise<void> {
    this.notificationCenter.markAsRead(notificationId);
  }

  async markAllAsRead(): Promise<void> {
    this.notificationCenter.markAllAsRead();
  }

  async deleteNotification(notificationId: number): Promise<void> {
    if (confirm('هل أنت متأكد من حذف هذا الإشعار؟')) {
      this.notificationCenter.removeNotification(notificationId);
    }
  }

  onScroll(): void {
    if (this.hasMore && !this.loading) {
      this.page++;
      this.loadNotifications();
    }
  }

  getNotificationIcon(type: string): string {
    const icons = {
      attendance: 'fa-user-graduate',
      grade: 'fa-star',
      exam: 'fa-pencil-alt',
      payment: 'fa-credit-card',
      event: 'fa-calendar',
      message: 'fa-envelope',
      warning: 'fa-exclamation-triangle'
    };

    return icons[type as keyof typeof icons] || 'fa-bell';
  }

  getNotificationColor(type: string): string {
    const colors = {
      attendance: '#3B82F6',
      grade: '#10B981',
      exam: '#8B5CF6',
      payment: '#F59E0B',
      event: '#EC4899',
      message: '#6366F1',
      warning: '#EF4444'
    };

    return colors[type as keyof typeof colors] || '#6B7280';
  }

  getNotificationLabel(type: string): string {
    const labels = {
      attendance: 'حضور',
      grade: 'درجات',
      exam: 'اختبار',
      payment: 'مدفوعات',
      event: 'تنبيه',
      message: 'رسالة',
      warning: 'تحذير'
    };

    return labels[type as keyof typeof labels] || 'تنبيه';
  }

  private applyNotifications(items: AppNotification[]): void {
    const end = this.page * this.pageSize;
    const visibleItems = items.slice(0, end);

    this.notifications = visibleItems;
    this.hasMore = items.length > visibleItems.length;
    this.unreadCount = items.filter(item => !item.isRead).length;
  }
}
