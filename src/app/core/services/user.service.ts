import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { User } from '../models/user.model';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  constructor(private api: ApiService, private auth: AuthService) {}

  async getCurrentUser(): Promise<User | null> {
    try {
      return await this.auth.refreshCurrentUserProfile();
    } catch {
      return this.auth.getCurrentUser();
    }
  }

  async updateProfile(data: Partial<User>): Promise<User> {
    const response = await this.api.put<User>('/api/Account/profile', data);
    const normalized = this.normalizeUser(response);
    this.auth.updateLocalUser(normalized);
    return normalized;
  }

  async uploadProfileImage(file: File): Promise<{ avatar?: string; url?: string }> {
    const response = await this.api.upload<{ avatar?: string; url?: string }>('/api/Account/avatar', file);
    const avatar = this.resolveUrl(response.avatar || response.url);
    if (avatar) {
      this.auth.updateLocalUser({ avatar });
    }

    return {
      ...response,
      avatar,
      url: avatar
    };
  }

  private normalizeUser(user: User): User {
    return {
      ...user,
      avatar: this.resolveUrl(user.avatar)
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
