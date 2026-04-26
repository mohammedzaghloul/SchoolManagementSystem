import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationCenterService } from '../../../core/services/notification-center.service';
import { User } from '../../../core/models/user.model';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.css'
})
export class SidebarComponent implements OnInit, OnDestroy {
  currentUser: User | null = null;
  unreadChatCount = 0;

  private messageUnreadSub?: Subscription;

  constructor(
    private authService: AuthService,
    private notificationCenter: NotificationCenterService
  ) { }

  ngOnInit() {
    this.currentUser = this.authService.getCurrentUser();
    this.messageUnreadSub = this.notificationCenter.unreadMessageCount$.subscribe(count => {
      this.unreadChatCount = count;
    });
  }

  hasRole(role: string): boolean {
    return this.authService.hasRole(role);
  }

  canCreateAdmins(): boolean {
    return this.authService.canCreateAdmins();
  }

  resetChatBadge() {
    this.notificationCenter.markAllAsRead('message');
  }

  logout() {
    this.authService.logout();
  }

  ngOnDestroy() {
    this.messageUnreadSub?.unsubscribe();
  }
}
