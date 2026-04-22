import { Component, OnInit, OnDestroy, HostListener, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { SignalRService } from '../../../core/services/signalr.service';
import { SearchService } from '../../../core/services/search.service';
import { NotificationCenterService } from '../../../core/services/notification-center.service';
import { NotificationService } from '../../../core/services/notification.service';
import { User } from '../../../core/models/user.model';
import { Notification } from '../../../core/models/notification.model';

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

  get isProfilePage(): boolean {
    return this.router.url === '/profile';
  }

  private authSub?: Subscription;
  private unreadCountSub?: Subscription;
  private notificationStoreSub?: Subscription;
  private signalRNotificationSub?: Subscription;

  constructor(
    private authService: AuthService,
    private signalR: SignalRService,
    private router: Router,
    private searchService: SearchService,
    private eRef: ElementRef,
    private notificationCenter: NotificationCenterService,
    private notify: NotificationService
  ) { }

  @HostListener('document:click', ['$event'])
  clickout(event: Event) {
    if (this.eRef.nativeElement.contains(event.target as Node | null)) {
      return;
    }

    this.showNotifications = false;
    this.showProfileMenu = false;
  }

  async ngOnInit() {
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme === 'dark') {
      this.isDarkMode = true;
      document.body.classList.add('dark-theme');
    }

    this.authSub = this.authService.currentUser$.subscribe(user => {
      this.currentUser = user;
    });

    this.notificationStoreSub = this.notificationCenter.notifications$.subscribe(items => {
      this.notifications = items;
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
        return;
      }

      const title = notif.data?.title || 'تنبيه';
      const message = notif.data?.content || 'لديك تحديث جديد داخل النظام.';

      this.notificationCenter.addNotification({
        title,
        message,
        type: 'event',
        data: notif.data
      });
      this.notify.info(message, title);
    });
  }

  private playNotificationSound() {
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

  getRoleText(role: string): string {
    const roles: { [key: string]: string } = {
      Admin: 'مدير النظام',
      Teacher: 'مدرس',
      Student: 'طالب',
      Parent: 'ولي أمر'
    };

    return roles[role] || role;
  }

  toggleNotifications(event: Event) {
    event.stopPropagation();
    this.showNotifications = !this.showNotifications;
    this.showProfileMenu = false;

    if (this.showNotifications) {
      this.notificationCenter.markAllAsRead();
    }
  }

  toggleProfileMenu(event: Event) {
    event.stopPropagation();
    this.showProfileMenu = !this.showProfileMenu;
    this.showNotifications = false;
  }

  toggleTheme() {
    this.isDarkMode = !this.isDarkMode;
    if (this.isDarkMode) {
      document.body.classList.add('dark-theme');
      localStorage.setItem('theme', 'dark');
    } else {
      document.body.classList.remove('dark-theme');
      localStorage.setItem('theme', 'light');
    }
  }

  goToChat() {
    this.showNotifications = false;
    this.notificationCenter.markAllAsRead('message');
    this.router.navigate(['/chat']);
  }

  clearNotifications(event?: Event) {
    event?.stopPropagation();
    this.notificationCenter.clearAll();
  }

  logout() {
    this.authService.logout();
    this.showProfileMenu = false;
  }

  onSearchChange(event: Event) {
    const term = (event.target as HTMLInputElement)?.value || '';
    this.searchService.updateSearchTerm(term);
  }

  ngOnDestroy(): void {
    this.authSub?.unsubscribe();
    this.unreadCountSub?.unsubscribe();
    this.notificationStoreSub?.unsubscribe();
    this.signalRNotificationSub?.unsubscribe();
  }
}
