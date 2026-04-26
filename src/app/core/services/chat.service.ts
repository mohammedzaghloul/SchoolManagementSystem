import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { Message, Contact } from '../models/message.model';
import { BehaviorSubject } from 'rxjs';
import { AuthService } from './auth.service';

export interface ChatHistoryPage {
  items: Message[];
  page: number;
  size: number;
  total: number;
  hasMore: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ChatService {

  constructor(private api: ApiService, private authService: AuthService) { }

  async getContacts(): Promise<Contact[]> {
    return this.api.get<Contact[]>('/api/Chat/contacts');
  }

  async getMessages(userId: string, page: number = 1, size: number = 50): Promise<ChatHistoryPage | Message[]> {
    return this.api.get<ChatHistoryPage | Message[]>(`/api/Chat/history/${userId}`, { page, size });
  }

  async sendMessage(receiverId: string, content: string, fileUrl?: string,
    fileName?: string, fileType?: string, fileSize?: number, messageType: string = 'text'): Promise<any> {
    return this.api.post<any>('/api/Chat/send', {
      receiverId, content, fileUrl, fileName, fileType, fileSize, messageType
    });
  }

  async uploadFile(file: File): Promise<{ fileUrl: string; fileName: string; fileType: string; fileSize: number }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.api.upload<any>('/api/Chat/upload', formData);
  }

  async markAsRead(contactId: string): Promise<void> {
    return this.api.post(`/api/Chat/mark-read/${contactId}`, {});
  }

  async deleteMessage(messageId: string): Promise<void> {
    return this.api.delete(`/api/Chat/message/${messageId}`);
  }

  async searchMessages(query: string, contactId?: string): Promise<any[]> {
    const params: any = { query };
    if (contactId) params.contactId = contactId;
    return this.api.get<any[]>('/api/Chat/search', params);
  }
}
