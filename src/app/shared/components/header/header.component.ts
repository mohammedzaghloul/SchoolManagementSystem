import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, OnDestroy, OnInit } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';

import { Notification } from '../../../core/models/notification.model';
import { User } from '../../../core/models/user.model';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationCenterService } from '../../../core/services/notification-center.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SearchService } from '../../../core/services/search.service';
import { SignalRService } from '../../../core/services/signalr.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './header.component.html',
  styleUrl: './header.component.css'
})
export class HeaderComponent implements OnInit, OnDestroy {
  currentUser: User | null = null;
  showNotifications = false;
  showProfileMenu = false;
  unreadCount = 0;
  isDarkMode = false;
  notifications: Notification[] = [];
  notificationPage = 1;
  readonly notificationPageSize = 15;
  browserNotificationsEnabled = false;

  private authSub?: Subscription;
  private unreadCountSub?: Subscription;
  private notificationStoreSub?: Subscription;
  private signalRNotificationSub?: Subscription;
  private notificationAudio?: HTMLAudioElement;

  constructor(
    private authService: AuthService,
    private signalR: SignalRService,
    private router: Router,
    private searchService: SearchService,
    private eRef: ElementRef,
    private notificationCenter: NotificationCenterService,
    private notify: NotificationService
  ) {}

  get isProfilePage(): boolean {
    return this.router.url === '/profile';
  }

  get visibleNotifications(): Notification[] {
    return this.notifications.slice(0, this.notificationPage * this.notificationPageSize);
  }

  get hasMoreNotifications(): boolean {
    return this.visibleNotifications.length < this.notifications.length;
  }

  get canEnableBrowserNotifications(): boolean {
    return this.getBrowserNotificationPermission() === 'default';
  }

  @HostListener('document:click', ['$event'])
  clickout(event: Event): void {
    if (this.eRef.nativeElement.contains(event.target as Node | null)) {
      return;
    }

    this.showNotifications = false;
    this.showProfileMenu = false;
  }

  async ngOnInit(): Promise<void> {
    this.currentUser = this.authService.getCurrentUser();
    this.syncTheme(this.currentUser);
    this.browserNotificationsEnabled = this.getBrowserNotificationPermission() === 'granted';
    this.notificationAudio = new Audio('/assets/sounds/new-message-sound-in-chat.mp3');
    this.notificationAudio.preload = 'auto';
    this.ensureWelcomeNotification(this.currentUser);

    this.authSub = this.authService.currentUser$.subscribe(user => {
      this.currentUser = user;
      this.syncTheme(user);
      this.ensureWelcomeNotification(user);
    });

    this.notificationStoreSub = this.notificationCenter.notifications$.subscribe(items => {
      this.notifications = items;
      if (this.visibleNotifications.length === 0) {
        this.notificationPage = 1;
      }
    });

    this.unreadCountSub = this.notificationCenter.unreadCount$.subscribe(count => {
      this.unreadCount = count;
    });

    this.signalRNotificationSub = this.signalR.notification$.subscribe(notif => {
      if (!notif) {
        return;
      }

      if (notif.type === 'message') {
        if (this.router.url.includes('/chat')) {
          return;
        }

        const message = notif.data?.content || 'تم استلام رسالة أو ملف جديد.';
        this.notificationCenter.addNotification({
          title: 'رسالة جديدة',
          message,
          type: 'message',
          data: notif.data
        });
        this.notify.info(message, 'التواصل');
        this.playNotificationSound();
        this.showBrowserNotification('رسالة جديدة', message);
        return;
      }

      const title = notif.data?.title || 'تنبيه';
      const message = notif.data?.content || notif.data?.message || 'لديك تحديث جديد داخل النظام.';

      this.notificationCenter.addNotification({
        title,
        message,
        type: 'event',
        data: notif.data
      });
      this.notify.info(message, title);
      this.playNotificationSound();
      this.showBrowserNotification(title, message);
    });
  }

  getRoleText(role: string): string {
    const roles: { [key: string]: string } = {
      Admin: 'مدير النظام',
      Teacher: 'مدرس',
      Student: 'طالب',
      Parent: 'ولي أمر'
    };

    return roles[role] || role;
  }

  getNotificationIcon(type: Notification['type']): string {
    const icons: Record<Notification['type'], string> = {
      attendance: 'fas fa-user-check',
      grade: 'fas fa-star',
      exam: 'fas fa-file-pen',
      payment: 'fas fa-credit-card',
      event: 'fas fa-bell',
      message: 'fas fa-envelope',
      warning: 'fas fa-triangle-exclamation'
    };

    return icons[type] || 'fas fa-bell';
  }

  getNotificationLabel(type: Notification['type']): string {
    const labels: Record<Notification['type'], string> = {
      attendance: 'حضور',
      grade: 'درجات',
      exam: 'اختبار',
      payment: 'مدفوعات',
      event: 'تنبيه',
      message: 'رسالة',
      warning: 'تحذير'
    };

    return labels[type] || 'تنبيه';
  }

  toggleNotifications(event: Event): void {
    event.stopPropagation();
    this.showNotifications = !this.showNotifications;
    this.showProfileMenu = false;

    if (this.showNotifications) {
      // Mark as read after 2s so the badge stays visible while user reads
      this.notificationPage = 1;
      setTimeout(() => {
        if (this.showNotifications) {
          this.notificationCenter.markAllAsRead();
        }
      }, 2000);
    }
  }

  async enableNotifications(event?: Event): Promise<void> {
    event?.stopPropagation();

    if (!('Notification' in window)) {
      this.notify.warning('المتصفح الحالي لا يدعم إشعارات سطح المكتب.');
      return;
    }

    const permission = await window.Notification.requestPermission();
    this.browserNotificationsEnabled = permission === 'granted';

    if (this.browserNotificationsEnabled) {
      this.notify.success('تم تفعيل إشعارات النظام بنجاح.');
      this.playNotificationSound();
      return;
    }

    this.notify.warning('لم يتم تفعيل إشعارات المتصفح. يمكنك السماح بها من إعدادات الموقع.');
  }

  loadMoreNotifications(event?: Event): void {
    event?.stopPropagation();
    this.notificationPage += 1;
  }

  toggleProfileMenu(event: Event): void {
    event.stopPropagation();
    this.showProfileMenu = !this.showProfileMenu;
    this.showNotifications = false;
  }

  toggleTheme(): void {
    if (this.currentUser?.role === 'Admin') {
      this.isDarkMode = false;
      document.body.classList.remove('dark-theme');
      localStorage.setItem('theme', 'light');
      return;
    }

    this.isDarkMode = !this.isDarkMode;
    if (this.isDarkMode) {
      document.body.classList.add('dark-theme');
      localStorage.setItem('theme', 'dark');
    } else {
      document.body.classList.remove('dark-theme');
      localStorage.setItem('theme', 'light');
    }
  }

  goToChat(notification?: Notification): void {
    this.showNotifications = false;
    this.notificationCenter.markAllAsRead('message');
    const contactId = notification?.data?.senderId || notification?.data?.receiverId;
    this.router.navigate(['/chat'], {
      queryParams: contactId ? { contactId } : undefined
    });
  }

  clearNotifications(event?: Event): void {
    event?.stopPropagation();
    this.notificationCenter.clearAll();
  }

  logout(): void {
    this.authService.logout();
    this.showProfileMenu = false;
  }

  onSearchChange(event: Event): void {
    const term = (event.target as HTMLInputElement)?.value || '';
    this.searchService.updateSearchTerm(term);
  }

  ngOnDestroy(): void {
    this.authSub?.unsubscribe();
    this.unreadCountSub?.unsubscribe();
    this.notificationStoreSub?.unsubscribe();
    this.signalRNotificationSub?.unsubscribe();
  }

  private playNotificationSound(): void {
    if (this.notificationAudio) {
      this.notificationAudio.currentTime = 0;
      this.notificationAudio.volume = 0.55;
      this.notificationAudio.play().catch(() => this.playFallbackTone());
      return;
    }

    this.playFallbackTone();
  }

  private playFallbackTone(): void {
    try {
      const audioContext = new (window.AudioContext || (window as any).webkitAudioContext)();
      const oscillator = audioContext.createOscillator();
      const gainNode = audioContext.createGain();

      oscillator.connect(gainNode);
      gainNode.connect(audioContext.destination);

      oscillator.frequency.setValueAtTime(880, audioContext.currentTime);
      oscillator.frequency.setValueAtTime(1100, audioContext.currentTime + 0.1);

      gainNode.gain.setValueAtTime(0.2, audioContext.currentTime);
      gainNode.gain.exponentialRampToValueAtTime(0.01, audioContext.currentTime + 0.3);

      oscillator.start(audioContext.currentTime);
      oscillator.stop(audioContext.currentTime + 0.3);
    } catch {
      // Ignore sound failures and keep notifications working visually.
    }
  }

  private showBrowserNotification(title: string, message: string): void {
    if (!this.browserNotificationsEnabled || !('Notification' in window)) {
      return;
    }

    try {
      new window.Notification(title, {
        body: message,
        icon: '/assets/images/default-avatar.png'
      });
    } catch {
      // Browser notification failures should not block in-app notifications.
    }
  }

  private getBrowserNotificationPermission(): NotificationPermission | 'unsupported' {
    return 'Notification' in window ? window.Notification.permission : 'unsupported';
  }

  private ensureWelcomeNotification(user: User | null): void {
    if (!user?.id) {
      return;
    }

    const storageKey = `school_welcome_notification_seen_${user.id}`;
    if (localStorage.getItem(storageKey) === '1') {
      return;
    }

    this.notificationCenter.addNotification({
      title: 'مرحباً بك في جولتنا!',
      message: 'اضغط على الجرس في أي وقت لمتابعة الرسائل والتنبيهات المهمة داخل النظام.',
      type: 'event'
    });

    localStorage.setItem(storageKey, '1');
    this.playNotificationSound();
  }

  private syncTheme(user: User | null): void {
    const savedTheme = localStorage.getItem('theme');
    const forceLightMode = user?.role === 'Admin';

    this.isDarkMode = !forceLightMode && savedTheme === 'dark';
    document.body.classList.toggle('dark-theme', this.isDarkMode);

    if (forceLightMode && savedTheme !== 'light') {
      localStorage.setItem('theme', 'light');
    }
  }
}
