import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { Notification } from '../models/notification.model';

type NotificationInput = Omit<Notification, 'id' | 'isRead' | 'createdAt'> & {
  createdAt?: Date | string;
};

@Injectable({
  providedIn: 'root'
})
export class NotificationCenterService {
  private readonly storageKey = 'school_notification_center';
  private readonly maxItems = 100;

  private notificationsSubject = new BehaviorSubject<Notification[]>(this.loadNotifications());
  readonly notifications$ = this.notificationsSubject.asObservable();

  private unreadCountSubject = new BehaviorSubject<number>(0);
  readonly unreadCount$ = this.unreadCountSubject.asObservable();

  private unreadMessageCountSubject = new BehaviorSubject<number>(0);
  readonly unreadMessageCount$ = this.unreadMessageCountSubject.asObservable();

  constructor() {
    this.updateCounters(this.notificationsSubject.value);
  }

  get snapshot(): Notification[] {
    return this.notificationsSubject.value;
  }

  addNotification(input: NotificationInput): void {
    const nextItem: Notification = {
      id: Date.now() + Math.floor(Math.random() * 1000),
      title: input.title,
      message: input.message,
      type: input.type,
      data: input.data,
      isRead: false,
      createdAt: input.createdAt ? new Date(input.createdAt) : new Date()
    };

    const nextState = [nextItem, ...this.notificationsSubject.value].slice(0, this.maxItems);
    this.setNotifications(nextState);
  }

  markAllAsRead(type?: Notification['type']): void {
    const nextState = this.notificationsSubject.value.map(notification => {
      if (type && notification.type !== type) {
        return notification;
      }

      return {
        ...notification,
        isRead: true
      };
    });

    this.setNotifications(nextState);
  }

  markAsRead(notificationId: number): void {
    const nextState = this.notificationsSubject.value.map(notification => {
      if (notification.id !== notificationId || notification.isRead) {
        return notification;
      }

      return {
        ...notification,
        isRead: true
      };
    });

    this.setNotifications(nextState);
  }

  removeNotification(notificationId: number): void {
    this.setNotifications(
      this.notificationsSubject.value.filter(notification => notification.id !== notificationId)
    );
  }

  clearAll(): void {
    this.setNotifications([]);
  }

  private setNotifications(notifications: Notification[]): void {
    this.notificationsSubject.next(notifications);
    this.persistNotifications(notifications);
    this.updateCounters(notifications);
  }

  private updateCounters(notifications: Notification[]): void {
    this.unreadCountSubject.next(notifications.filter(item => !item.isRead).length);
    this.unreadMessageCountSubject.next(
      notifications.filter(item => !item.isRead && item.type === 'message').length
    );
  }

  private loadNotifications(): Notification[] {
    try {
      const raw = localStorage.getItem(this.storageKey);
      if (!raw) {
        return [];
      }

      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) {
        return [];
      }

      return parsed.map(item => ({
        ...item,
        createdAt: new Date(item.createdAt)
      }));
    } catch {
      return [];
    }
  }

  private persistNotifications(notifications: Notification[]): void {
    try {
      localStorage.setItem(this.storageKey, JSON.stringify(notifications));
    } catch {
      // Ignore storage failures and keep in-memory notifications working.
    }
  }
}
