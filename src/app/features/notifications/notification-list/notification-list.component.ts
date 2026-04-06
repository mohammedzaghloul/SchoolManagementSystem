// features/notifications/notification-list/notification-list.component.ts
@Component({
  selector: 'app-notification-list',
  templateUrl: './notification-list.component.html',
  styleUrls: ['./notification-list.component.css']
})
export class NotificationListComponent implements OnInit {
  notifications: Notification[] = [];
  unreadCount = 0;
  loading = false;
  page = 1;
  pageSize = 20;
  hasMore = true;

  constructor(
    private notificationService: NotificationService,
    private signalR: SignalRService
  ) {}

  ngOnInit() {
    this.loadNotifications();
    this.listenForNewNotifications();
  }

  async loadNotifications(): Promise<void> {
    if (this.loading) return;
    
    this.loading = true;
    try {
      const response = await this.notificationService.getNotifications(this.page, this.pageSize);
      
      if (this.page === 1) {
        this.notifications = response.items;
      } else {
        this.notifications = [...this.notifications, ...response.items];
      }
      
      this.hasMore = response.hasMore;
      this.unreadCount = response.unreadCount;
      
    } catch (error) {
      console.error('Error loading notifications:', error);
    } finally {
      this.loading = false;
    }
  }

  listenForNewNotifications(): void {
    this.signalR.notification$.subscribe(notification => {
      this.notifications.unshift(notification);
      this.unreadCount++;
      this.playNotificationSound();
      this.showBrowserNotification(notification);
    });
  }

  playNotificationSound(): void {
    const audio = new Audio('/assets/sounds/notification.mp3');
    audio.play().catch(() => {});
  }

  showBrowserNotification(notification: Notification): void {
    if (Notification.permission === 'granted') {
      new Notification(notification.title, {
        body: notification.message,
        icon: '/assets/icons/notification-icon.png'
      });
    }
  }

  async markAsRead(notificationId: number): Promise<void> {
    await this.notificationService.markAsRead(notificationId);
    
    const notification = this.notifications.find(n => n.id === notificationId);
    if (notification && !notification.isRead) {
      notification.isRead = true;
      this.unreadCount--;
    }
  }

  async markAllAsRead(): Promise<void> {
    await this.notificationService.markAllAsRead();
    this.notifications.forEach(n => n.isRead = true);
    this.unreadCount = 0;
  }

  async deleteNotification(notificationId: number): Promise<void> {
    if (confirm('هل أنت متأكد من حذف هذا الإشعار؟')) {
      await this.notificationService.deleteNotification(notificationId);
      this.notifications = this.notifications.filter(n => n.id !== notificationId);
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
      'attendance': 'fa-user-graduate',
      'grade': 'fa-star',
      'exam': 'fa-pencil-alt',
      'payment': 'fa-credit-card',
      'event': 'fa-calendar',
      'message': 'fa-envelope',
      'warning': 'fa-exclamation-triangle'
    };
    return icons[type as keyof typeof icons] || 'fa-bell';
  }

  getNotificationColor(type: string): string {
    const colors = {
      'attendance': '#3B82F6',
      'grade': '#10B981',
      'exam': '#8B5CF6',
      'payment': '#F59E0B',
      'event': '#EC4899',
      'message': '#6366F1',
      'warning': '#EF4444'
    };
    return colors[type as keyof typeof colors] || '#6B7280';
  }
}