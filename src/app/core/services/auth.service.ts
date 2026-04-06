// core/services/auth.service.ts
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { ApiService } from './api.service';
import { StorageService } from './storage.service';
import { User } from '../models/user.model';

interface LoginResponse {
  token: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(
    private api: ApiService,
    private storage: StorageService,
    private router: Router
  ) {
    this.loadStoredUser();
  }

  // ─── Private Helpers ─────────────────────────────────────────────────────────

  private loadStoredUser(): void {
    const token = this.storage.getToken();
    if (token && !this.isTokenExpired(token)) {
      // Try to get fully enriched user from storage first
      const storedUser = this.storage.getUser();
      if (storedUser) {
        this.currentUserSubject.next(this.normalizeUser(storedUser));
      } else {
        const user = this.normalizeUser(this.decodeUserFromToken(token));
        this.currentUserSubject.next(user);
      }
    } else {
      this.storage.clear();
    }
  }

  private isTokenExpired(token: string): boolean {
    try {
      const base64Url = token.split('.')[1];
      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      const jsonPayload = decodeURIComponent(atob(base64).split('').map((c) => {
        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
      }).join(''));
      const payload = JSON.parse(jsonPayload);
      if (!payload.exp) return false;
      return Date.now() >= payload.exp * 1000;
    } catch {
      return true;
    }
  }

  /**
   * Decodes a JWT and maps standard .NET Identity claims to a User object.
   * .NET uses full URN claim names for ClaimTypes.Role and ClaimTypes.Name.
   */
  private decodeUserFromToken(token: string): User {
    try {
      const base64Url = token.split('.')[1];
      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      const jsonPayload = decodeURIComponent(atob(base64).split('').map((c) => {
        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
      }).join(''));

      const payload = JSON.parse(jsonPayload);
      console.log('[Auth] Decoded payload:', payload);

      return {
        id:
          payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ||
          payload['nameid'] ||
          payload.sub ||
          '',
        email:
          payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
          payload['email'] ||
          '',
        role:
          payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
          payload['role'] ||
          'User',
        fullName:
          payload['given_name'] ||
          payload['unique_name'] ||
          payload['name'] ||
          payload['FullName'] ||
          ''
      };
    } catch (err) {
      console.error('[Auth] Failed to decode token parts:', err);
      throw err;
    }
  }

  // ─── Public API ───────────────────────────────────────────────────────────────

  async login(credentials: { email: string; password: string }): Promise<User> {
    console.log('[Auth] Attempting login for:', credentials.email);
    const response = await this.api.post<LoginResponse>('/api/Account/login', credentials);
    console.log('[Auth] Login response received:', !!response.token ? 'Token present' : 'Token missing');

    if (!response.token) {
      throw new Error('No token received from server');
    }

    this.storage.setToken(response.token);

    try {
      const baseUser = this.decodeUserFromToken(response.token);
      const user = await this.loadRemoteProfile(baseUser);
      console.log('[Auth] User decoded successfully:', user.role);
      this.storage.setUser(user);
      this.currentUserSubject.next(user);
      return user;
    } catch (decodeErr) {
      console.error('[Auth] Token decoding failed:', decodeErr);
      throw decodeErr;
    }
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
    if (!user) return false;
    if (Array.isArray(role)) return role.includes(user.role);
    return user.role === role;
  }

  isAdmin(): boolean {
    return this.hasRole('Admin');
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
    if (current) {
      const updated = this.normalizeUser({ ...current, ...data });
      this.storage.setUser(updated);
      this.currentUserSubject.next(updated);
    }
  }

  async changePassword(data: any): Promise<any> {
    return this.api.post<any>('/api/Account/change-password', data);
  }

  private async loadRemoteProfile(baseUser: User): Promise<User> {
    try {
      const profile = await this.api.get<Partial<User>>('/api/Account/profile');
      return this.normalizeUser({ ...baseUser, ...profile });
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
}
