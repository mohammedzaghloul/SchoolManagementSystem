import { Injectable } from '@angular/core';
import { Subject, BehaviorSubject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';
import { Message } from '../models/message.model';

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private hubConnection?: signalR.HubConnection;
  private connectionPromise?: Promise<void>;
  private reconnectTimer?: ReturnType<typeof setTimeout>;
  private manuallyStopped = false;

  private messageSubject = new Subject<Message>();
  message$ = this.messageSubject.asObservable();

  private notificationSubject = new Subject<any>();
  notification$ = this.notificationSubject.asObservable();

  private typingSubject = new Subject<{ userId: string; isTyping: boolean }>();
  typing$ = this.typingSubject.asObservable();

  private onlineUsersSubject = new BehaviorSubject<Set<string>>(new Set());
  onlineUsers$ = this.onlineUsersSubject.asObservable();

  private messagesReadSubject = new Subject<string>();
  messagesRead$ = this.messagesReadSubject.asObservable();

  private connectionStateSubject = new BehaviorSubject<signalR.HubConnectionState>(
    signalR.HubConnectionState.Disconnected
  );
  connectionState$ = this.connectionStateSubject.asObservable();

  constructor(private auth: AuthService) { }

  async startConnection(): Promise<void> {
    if (!this.auth.getToken()) {
      return;
    }

    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      this.connectionStateSubject.next(signalR.HubConnectionState.Connected);
      return;
    }

    if (
      this.hubConnection?.state === signalR.HubConnectionState.Connecting
      || this.hubConnection?.state === signalR.HubConnectionState.Reconnecting
    ) {
      return this.connectionPromise ?? Promise.resolve();
    }

    this.manuallyStopped = false;
    this.clearReconnectTimer();

    if (!this.hubConnection || this.hubConnection.state === signalR.HubConnectionState.Disconnected) {
      this.createConnection();
    }

    this.connectionStateSubject.next(signalR.HubConnectionState.Connecting);
    this.connectionPromise = this.hubConnection!.start()
      .then(async () => {
        this.connectionStateSubject.next(signalR.HubConnectionState.Connected);
        console.log('SignalR connected');
        await this.refreshOnlineUsers();
      })
      .catch(err => {
        this.connectionStateSubject.next(signalR.HubConnectionState.Disconnected);
        console.error('SignalR connection error:', err);
        this.scheduleReconnect();
        throw err;
      })
      .finally(() => {
        this.connectionPromise = undefined;
      });

    return this.connectionPromise;
  }

  private registerEvents(): void {
    const connection = this.hubConnection;
    if (!connection) {
      return;
    }

    // Receive full message object from hub
    connection.on('ReceiveMessage', (messageData: any) => {
      const msg: Message = {
        id: messageData.id?.toString() || Date.now().toString(),
        senderId: messageData.senderId,
        receiverId: messageData.receiverId,
        content: messageData.content || '',
        timestamp: messageData.sentAt || new Date(),
        isRead: messageData.isRead || false,
        isIncoming: messageData.senderId !== (this.auth.getCurrentUser()?.id?.toString() || ''),
        fileUrl: messageData.fileUrl,
        fileName: messageData.fileName,
        fileType: messageData.fileType,
        fileSize: messageData.fileSize,
        messageType: messageData.messageType || 'text'
      };
      this.messageSubject.next(msg);
      // For global notification badge
      if (msg.isIncoming) {
        this.notificationSubject.next({ type: 'message', data: msg });
      }
    });

    connection.on('ReceiveNotification', (notificationOrTitle: any, content?: string, audience?: string) => {
      const notification = typeof notificationOrTitle === 'string'
        ? {
          title: notificationOrTitle,
          content: content || '',
          type: audience || 'General'
        }
        : notificationOrTitle;

      this.notificationSubject.next({ type: 'general', data: notification });
    });

    connection.on('UserTyping', (userId: string) => {
      this.typingSubject.next({ userId, isTyping: true });
    });

    connection.on('UserStopTyping', (userId: string) => {
      this.typingSubject.next({ userId, isTyping: false });
    });

    connection.on('UserOnline', (userId: string) => {
      const current = this.onlineUsersSubject.value;
      current.add(userId);
      this.onlineUsersSubject.next(new Set(current));
    });

    connection.on('UserOffline', (userId: string) => {
      const current = this.onlineUsersSubject.value;
      current.delete(userId);
      this.onlineUsersSubject.next(new Set(current));
    });

    connection.on('MessagesRead', (readByUserId: string) => {
      this.messagesReadSubject.next(readByUserId);
    });
  }

  async stopConnection(): Promise<void> {
    this.manuallyStopped = true;
    this.clearReconnectTimer();
    this.connectionPromise = undefined;
    this.onlineUsersSubject.next(new Set());
    this.connectionStateSubject.next(signalR.HubConnectionState.Disconnected);

    if (this.hubConnection) {
      const connection = this.hubConnection;
      this.hubConnection = undefined;

      try {
        await connection.stop();
      } catch {
        // Ignore stop errors during logout/navigation cleanup.
      }
    }
  }

  async sendMessage(receiverId: string, content: string, fileUrl?: string,
    fileName?: string, fileType?: string, fileSize?: number, messageType: string = 'text'): Promise<void> {
    if (!await this.ensureConnected()) {
      throw new Error('SignalR connection is not available.');
    }

    await this.hubConnection!.invoke('SendMessage', receiverId, content,
      fileUrl || null, fileName || null, fileType || null, fileSize || null, messageType);
  }

  async typing(receiverId: string): Promise<void> {
    try {
      if (!await this.ensureConnected()) {
        return;
      }

      await this.hubConnection!.invoke('Typing', receiverId);
    } catch { }
  }

  async stopTyping(receiverId: string): Promise<void> {
    try {
      if (!await this.ensureConnected()) {
        return;
      }

      await this.hubConnection!.invoke('StopTyping', receiverId);
    } catch { }
  }

  async markAsRead(senderId: string): Promise<void> {
    try {
      if (!await this.ensureConnected()) {
        return;
      }

      await this.hubConnection!.invoke('MarkAsRead', senderId);
    } catch { }
  }

  isConnected(): boolean {
    return this.hubConnection?.state === signalR.HubConnectionState.Connected;
  }

  private createConnection(): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.getHubUrl(), {
        accessTokenFactory: () => this.auth.getToken() || ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.hubConnection.keepAliveIntervalInMilliseconds = 15000;
    this.hubConnection.serverTimeoutInMilliseconds = 60000;

    this.registerEvents();
    this.registerLifecycleEvents();
  }

  private registerLifecycleEvents(): void {
    if (!this.hubConnection) {
      return;
    }

    this.hubConnection.onreconnecting(error => {
      this.connectionStateSubject.next(signalR.HubConnectionState.Reconnecting);
      console.warn('SignalR reconnecting:', error);
    });

    this.hubConnection.onreconnected(async () => {
      this.connectionStateSubject.next(signalR.HubConnectionState.Connected);
      console.log('SignalR reconnected');
      await this.refreshOnlineUsers();
    });

    this.hubConnection.onclose(error => {
      this.connectionStateSubject.next(signalR.HubConnectionState.Disconnected);
      this.onlineUsersSubject.next(new Set());

      if (this.manuallyStopped) {
        return;
      }

      console.error('SignalR connection closed:', error);
      this.scheduleReconnect();
    });
  }

  private async refreshOnlineUsers(): Promise<void> {
    if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      const users = await this.hubConnection.invoke<string[]>('GetOnlineUsers');
      this.onlineUsersSubject.next(new Set(users));
    } catch {
      // Ignore refresh failures and keep the live connection running.
    }
  }

  private async ensureConnected(): Promise<boolean> {
    if (!this.auth.getToken()) {
      return false;
    }

    if (this.isConnected()) {
      return true;
    }

    try {
      await this.startConnection();
    } catch {
      return false;
    }

    return this.isConnected();
  }

  private scheduleReconnect(): void {
    if (this.manuallyStopped || this.reconnectTimer || !this.auth.getToken()) {
      return;
    }

    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = undefined;
      this.startConnection().catch(() => {
        this.scheduleReconnect();
      });
    }, 5000);
  }

  private clearReconnectTimer(): void {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = undefined;
    }
  }

  private getHubUrl(): string {
    const configuredUrl = environment.signalRUrl || `${environment.apiUrl}/chathub`;
    return configuredUrl.replace(/\/chatHub$/i, '/chathub');
  }
}
