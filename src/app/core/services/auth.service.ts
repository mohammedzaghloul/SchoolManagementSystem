import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';

import { User } from '../models/user.model';
import { ApiService } from './api.service';
import { StorageService } from './storage.service';

export interface LoginOtpChallenge {
  requiresOtp: true;
  challengeId: string;
  message?: string;
  devOtp?: string;
  emailSent?: boolean;
  expiresInSeconds?: number;
}

interface LoginResponse {
  token?: string;
  requiresOtp?: boolean;
  challengeId?: string;
  message?: string;
  devOtp?: string;
  emailSent?: boolean;
  expiresInSeconds?: number;
}

export interface ForgotPasswordOtpResponse {
  success: boolean;
  emailSent: boolean;
  emailAccepted?: boolean;
  emailDeliveryStatus?: 'accepted' | 'cooldown' | 'unavailable' | 'not_applicable' | string;
  message?: string;
  expiresAtUtc?: string;
  devOtp?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly systemOwnerAdminEmail = 'mohammedzaghloul0123@gmail.com';
  private readonly loginDeviceKeyStorage = 'trusted_login_device_key';
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(
    private api: ApiService,
    private storage: StorageService,
    private router: Router
  ) {
    this.loadStoredUser();
  }

  async login(credentials: { email: string; password: string }): Promise<User | LoginOtpChallenge> {
    const response = await this.api.post<LoginResponse>('/api/Account/login', {
      email: credentials.email,
      password: credentials.password,
      deviceKey: this.getOrCreateLoginDeviceKey(),
      deviceName: this.getDeviceName()
    });

    if (response.requiresOtp && response.challengeId) {
      return {
        requiresOtp: true,
        challengeId: response.challengeId,
        message: response.message,
        devOtp: response.devOtp,
        emailSent: response.emailSent,
        expiresInSeconds: response.expiresInSeconds
      };
    }

    if (!response.token) {
      throw new Error('No token received from server');
    }

    return this.completeTokenLogin(response.token);
  }

  async verifyLoginOtp(challengeId: string, otp: string): Promise<User> {
    const response = await this.api.post<LoginResponse>('/api/Account/login/verify-otp', {
      challengeId,
      otp,
      deviceKey: this.getOrCreateLoginDeviceKey()
    });

    if (!response.token) {
      throw new Error('No token received from server');
    }

    return this.completeTokenLogin(response.token);
  }

  logout(): void {
    this.storage.clear();
    this.currentUserSubject.next(null);
    this.router.navigate(['/auth/login']);
  }

  getCurrentUser(): User | null {
    return this.currentUserSubject.value;
  }

  async refreshCurrentUserProfile(): Promise<User | null> {
    const current = this.currentUserSubject.value;
    if (!current || !this.getToken()) {
      return current;
    }

    const updated = await this.loadRemoteProfile(current);
    this.storage.setUser(updated);
    this.currentUserSubject.next(updated);
    return updated;
  }

  isAuthenticated(): boolean {
    const token = this.storage.getToken();
    return !!token && !this.isTokenExpired(token);
  }

  hasRole(role: string | string[]): boolean {
    const user = this.currentUserSubject.value;
    if (!user) {
      return false;
    }

    return Array.isArray(role) ? role.includes(user.role) : user.role === role;
  }

  isAdmin(): boolean {
    return this.hasRole('Admin');
  }

  canCreateAdmins(): boolean {
    const user = this.currentUserSubject.value;
    return user?.role === 'Admin'
      && (user.email || '').toLowerCase() === this.systemOwnerAdminEmail;
  }

  isTeacher(): boolean {
    return this.hasRole('Teacher');
  }

  isStudent(): boolean {
    return this.hasRole('Student');
  }

  isParent(): boolean {
    return this.hasRole('Parent');
  }

  getToken(): string | null {
    return this.storage.getToken();
  }

  updateLocalUser(data: Partial<User>): void {
    const current = this.currentUserSubject.value;
    if (!current) {
      return;
    }

    const updated = this.normalizeUser({ ...current, ...data });
    this.storage.setUser(updated);
    this.currentUserSubject.next(updated);
  }

  async changePassword(data: any): Promise<any> {
    return this.api.post<any>('/api/Account/change-password', data);
  }

  async sendForgotPasswordOtp(email: string): Promise<ForgotPasswordOtpResponse> {
    return this.api.post<ForgotPasswordOtpResponse>('/api/auth/forgot-password', { email });
  }

  async verifyResetOtp(email: string, otp: string): Promise<any> {
    return this.api.post<any>('/api/auth/verify-otp', { email, otp });
  }

  async verifyForgotPasswordOtp(email: string, otp: string, newPassword: string): Promise<any> {
    return this.api.post<any>('/api/auth/reset-password', {
      email,
      otp,
      newPassword,
      confirmPassword: newPassword
    });
  }

  private async completeTokenLogin(token: string): Promise<User> {
    this.storage.setToken(token);

    const baseUser = this.decodeUserFromToken(token);
    const user = await this.loadRemoteProfile(baseUser);
    this.storage.setUser(user);
    this.currentUserSubject.next(user);
    return user;
  }

  private loadStoredUser(): void {
    const token = this.storage.getToken();
    if (!token || this.isTokenExpired(token)) {
      this.storage.clear();
      return;
    }

    const storedUser = this.storage.getUser();
    if (storedUser) {
      this.currentUserSubject.next(this.normalizeUser(storedUser));
      return;
    }

    const user = this.normalizeUser(this.decodeUserFromToken(token));
    this.currentUserSubject.next(user);
  }

  private isTokenExpired(token: string): boolean {
    try {
      const payload = this.parseJwt(token);
      if (!payload.exp) {
        return false;
      }

      return Date.now() >= payload.exp * 1000;
    } catch {
      return true;
    }
  }

  private decodeUserFromToken(token: string): User {
    const payload = this.parseJwt(token);

    return this.normalizeUser({
      id:
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ||
        payload.nameid ||
        payload.sub ||
        '',
      email:
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
        payload.email ||
        '',
      role:
        payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
        payload.role ||
        'User',
      fullName:
        payload.given_name ||
        payload.unique_name ||
        payload.name ||
        payload.FullName ||
        ''
    });
  }

  private async loadRemoteProfile(baseUser: User): Promise<User> {
    try {
      const profile = await this.api.get<Partial<User>>('/api/Account/profile');
      const user = this.normalizeUser({ ...baseUser, ...profile });
      if (baseUser.role === 'Admin') {
        user.role = 'Admin';
      }

      return user;
    } catch {
      return this.normalizeUser(baseUser);
    }
  }

  private normalizeUser(user: Partial<User>): User {
    return {
      id: user.id,
      fullName: user.fullName || '',
      email: user.email || '',
      phone: user.phone,
      avatar: this.resolveUrl(user.avatar),
      role: user.role || 'User',
      createdAt: user.createdAt,
      lastLogin: user.lastLogin,
      sessionCount: user.sessionCount,
      address: user.address,
      permissions: user.permissions
    };
  }

  private resolveUrl(url?: string): string | undefined {
    if (!url) {
      return undefined;
    }

    if (url.startsWith('http://') || url.startsWith('https://') || url.startsWith('data:')) {
      return url;
    }

    return `${this.api.getBaseUrl()}${url}`;
  }

  private getOrCreateLoginDeviceKey(): string {
    let deviceKey = localStorage.getItem(this.loginDeviceKeyStorage);
    if (deviceKey) {
      return deviceKey;
    }

    deviceKey = typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(36).slice(2)}-${Math.random().toString(36).slice(2)}`;

    localStorage.setItem(this.loginDeviceKeyStorage, deviceKey);
    return deviceKey;
  }

  private getDeviceName(): string {
    const platform = navigator.platform || 'Browser';
    const userAgent = navigator.userAgent || '';
    const browser = userAgent.includes('Edg')
      ? 'Edge'
      : userAgent.includes('Chrome')
        ? 'Chrome'
        : userAgent.includes('Firefox')
          ? 'Firefox'
          : userAgent.includes('Safari')
            ? 'Safari'
            : 'Browser';

    return `${browser} - ${platform}`;
  }

  private parseJwt(token: string): any {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((char) => `%${(`00${char.charCodeAt(0).toString(16)}`).slice(-2)}`)
        .join('')
    );

    return JSON.parse(jsonPayload);
  }
}
