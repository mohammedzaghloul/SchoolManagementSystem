import { Component, OnInit, OnDestroy, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import WaveSurfer from 'wavesurfer.js';
import { SignalRService } from '../../../core/services/signalr.service';
import { AuthService } from '../../../core/services/auth.service';
import { ChatService } from '../../../core/services/chat.service';
import { NotificationService } from '../../../core/services/notification.service';
import { Message, Contact } from '../../../core/models/message.model';
import { environment } from '../../../../environments/environment';

declare var MediaRecorder: any;

@Component({
  selector: 'app-chat-room',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat-room.component.html',
  styleUrls: ['./chat-room.component.css']
})
export class ChatRoomComponent implements OnInit, OnDestroy {
  contacts: Contact[] = [];
  filteredContacts: Contact[] = [];
  visibleContactsCount = 15;
  readonly contactsPageSize = 15;
  selectedContact: Contact | null = null;
  messages: Message[] = [];
  newMessage = '';
  searchQuery = '';
  messageSearchQuery = '';
  loading = false;
  loadingMessages = false;
  loadingOlderMessages = false;
  messagesPage = 1;
  messagesPageSize = 30;
  hasMoreMessages = false;
  currentUserRole = '';
  currentUserId = '';
  showEmojiPicker = false;
  isRecording = false;
  recordingDuration = 0;
  recordingInterval: any;
  mediaRecorder: any;
  audioChunks: Blob[] = [];
  showMessageSearch = false;
  searchResults: any[] = [];
  onlineUsers: Set<string> = new Set();
  showAttachMenu = false;
  imagePreview: string | null = null;
  confirmDeleteId: string | null = null;

  private audioUnlocked = false;
  private recordingStream: MediaStream | null = null;
  private shouldSendRecording = false;
  private messageSub?: Subscription;
  private typingSub?: Subscription;
  private onlineSub?: Subscription;
  private readSub?: Subscription;
  private routeSub?: Subscription;
  private typingTimeout: any;
  private wavesurfers: Map<string, WaveSurfer> = new Map();
  private mutationObserver?: MutationObserver;
  private mutationObserverAttached = false;
  private isUserScrollingManually = false;

  get visibleContacts(): Contact[] {
    return this.filteredContacts.slice(0, this.visibleContactsCount);
  }

  get hasMoreContacts(): boolean {
    return this.visibleContactsCount < this.filteredContacts.length;
  }

  @ViewChild('sentAudio') sentAudio!: ElementRef<HTMLAudioElement>;
  @ViewChild('receivedAudio') receivedAudio!: ElementRef<HTMLAudioElement>;
  @ViewChild('notifyAudio') notifyAudio!: ElementRef<HTMLAudioElement>;
  @ViewChild('chatMessagesContainer') chatMessagesContainer!: ElementRef;
  @ViewChild('messageInput') messageInput!: ElementRef;

  emojis = [
    '😊', '😂', '❤️', '👍', '👋', '✨', '🙏', '🔥',
    '😍', '🥰', '😘', '🤔', '😅', '🤣', '😎', '🥳',
    '👏', '💪', '✅', '❌', '⭐', '🎉', '📚', '✏️',
    '🏫', '👨‍🏫', '👩‍🎓', '📖', '💡', '🎯', '🌟', '💯'
  ];

  constructor(
    private signalR: SignalRService,
    private auth: AuthService,
    private chatService: ChatService,
    private notify: NotificationService,
    private route: ActivatedRoute
  ) { }

  unlockAudio() {
    if (this.audioUnlocked) {
      return;
    }

    [this.sentAudio, this.receivedAudio, this.notifyAudio].forEach(elementRef => {
      const audio = elementRef?.nativeElement;
      if (audio) {
        audio.play().then(() => {
          audio.pause();
          audio.currentTime = 0;
        }).catch(() => { });
      }
    });

    this.audioUnlocked = true;
  }

  async ngOnInit() {
    const user = this.auth.getCurrentUser();
    this.currentUserRole = user?.role || '';
    this.currentUserId = user?.id?.toString() || '';

    this.loading = true;
    await this.loadContacts();
    this.loading = false;
    this.selectContactFromRoute();

    this.routeSub = this.route.queryParamMap.subscribe(() => {
      this.selectContactFromRoute();
    });

    await this.signalR.startConnection();
    this.listenForMessages();
    this.listenForTyping();
    this.listenForOnlineStatus();
    this.listenForReadReceipts();
    this.setupScrollObserver();
  }

  ngAfterViewChecked() {
    if (this.chatMessagesContainer && !this.mutationObserverAttached) {
      this.attachObserver();
    }
  }

  private setupScrollObserver() {
    this.mutationObserver = new MutationObserver(() => {
      if (!this.isUserScrollingManually) {
        this.scrollToBottom();
      }
    });
  }

  private attachObserver() {
    if (!this.chatMessagesContainer) {
      return;
    }

    this.mutationObserver?.observe(this.chatMessagesContainer.nativeElement, {
      childList: true,
      subtree: true
    });
    this.mutationObserverAttached = true;

    this.chatMessagesContainer.nativeElement.addEventListener('scroll', () => {
      const element = this.chatMessagesContainer.nativeElement;
      const atBottom = element.scrollHeight - element.scrollTop <= element.clientHeight + 100;
      this.isUserScrollingManually = !atBottom;
    });
  }

  async loadContacts(): Promise<void> {
    try {
      this.contacts = await this.chatService.getContacts() || [];
      this.contacts.sort((a, b) => {
        const timeA = a.lastMessageTime ? new Date(a.lastMessageTime).getTime() : 0;
        const timeB = b.lastMessageTime ? new Date(b.lastMessageTime).getTime() : 0;
        return timeB - timeA;
      });
      this.filteredContacts = [...this.contacts];
      this.visibleContactsCount = this.contactsPageSize;
    } catch {
      this.contacts = [];
      this.filteredContacts = [];
      this.visibleContactsCount = this.contactsPageSize;
    }
  }

  filterContacts() {
    const query = (this.searchQuery || '').trim().toLowerCase();
    
    if (!query) {
      this.filteredContacts = [...this.contacts];
      this.visibleContactsCount = this.contactsPageSize;
      return;
    }

    this.filteredContacts = this.contacts.filter(contact => {
      const nameMatch = contact.name?.toLowerCase().includes(query);
      const studentMatch = contact.studentName?.toLowerCase().includes(query);
      const roleMatch = this.getContactRoleLabel(contact).toLowerCase().includes(query);
      const subjectMatch = contact.subjectName?.toLowerCase().includes(query);
      
      return nameMatch || studentMatch || roleMatch || subjectMatch;
    });
    this.visibleContactsCount = this.contactsPageSize;
  }

  loadMoreContacts(): void {
    this.visibleContactsCount += this.contactsPageSize;
  }

  async selectContact(contact: Contact): Promise<void> {
    this.selectedContact = contact;
    this.messages = [];
    this.messagesPage = 1;
    this.hasMoreMessages = false;
    this.loadingMessages = true;
    this.showMessageSearch = false;
    this.showEmojiPicker = false;
    this.showAttachMenu = false;
    this.messageSearchQuery = '';
    this.isUserScrollingManually = false;

    await this.loadMessages(contact.id, 1, false);
    this.loadingMessages = false;

    if (contact.unreadCount > 0) {
      contact.unreadCount = 0;
      try {
        await this.chatService.markAsRead(contact.id);
        await this.signalR.markAsRead(contact.id);
      } catch {
        // Ignore read receipt failures and keep chat usable.
      }
    }

    setTimeout(() => this.scrollToBottom(), 50);
    setTimeout(() => this.scrollToBottom(), 400);
  }

  async loadMessages(contactId: string, page: number = 1, appendOlder: boolean = false): Promise<void> {
    try {
      const response: any = await this.chatService.getMessages(contactId, page, this.messagesPageSize);
      const rawMessages = Array.isArray(response) ? response : (response.items || []);

      const mappedMessages = rawMessages.map((message: any) => ({
        ...message,
        id: message.id?.toString() || Date.now().toString(),
        timestamp: message.sentAt || message.timestamp || new Date(),
        isIncoming: message.senderId !== this.currentUserId,
        messageType: message.messageType || 'text'
      }));

      this.messages = appendOlder
        ? [...mappedMessages, ...this.messages]
        : mappedMessages;

      this.messages.sort((a, b) =>
        new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()
      );

      this.messagesPage = Array.isArray(response) ? page : (response.page || page);
      this.hasMoreMessages = Array.isArray(response)
        ? rawMessages.length >= this.messagesPageSize
        : !!response.hasMore;
    } catch (error) {
      console.error('Error loading messages:', error);
      if (!appendOlder) {
        this.messages = [];
        this.hasMoreMessages = false;
      }
    }
  }

  private selectContactFromRoute(): void {
    const contactId = this.route.snapshot.queryParamMap.get('contactId');
    if (!contactId || this.selectedContact?.id === contactId) {
      return;
    }

    const contact = this.contacts.find(item => item.id === contactId);
    if (contact) {
      this.selectContact(contact);
    }
  }

  async loadOlderMessages(): Promise<void> {
    if (!this.selectedContact || this.loadingOlderMessages || !this.hasMoreMessages) {
      return;
    }

    const container = this.chatMessagesContainer?.nativeElement;
    const previousScrollHeight = container?.scrollHeight || 0;
    this.loadingOlderMessages = true;

    try {
      await this.loadMessages(this.selectedContact.id, this.messagesPage + 1, true);

      setTimeout(() => {
        const element = this.chatMessagesContainer?.nativeElement;
        if (element) {
          element.scrollTop = element.scrollHeight - previousScrollHeight;
        }
      }, 50);
    } finally {
      this.loadingOlderMessages = false;
    }
  }

  listenForMessages(): void {
    this.messageSub = this.signalR.message$.subscribe(message => {
      if (!message) {
        return;
      }

      if (
        this.selectedContact &&
        (message.senderId === this.selectedContact.id || message.receiverId === this.selectedContact.id)
      ) {
        const exists = this.messages.some(existing =>
          existing.id.toString() === message.id.toString() ||
          (
            existing.isSending &&
            existing.receiverId === message.receiverId &&
            existing.messageType === message.messageType &&
            (existing.content === message.content || !!existing.fileUrl)
          )
        );

        if (!exists) {
          if (!message.isIncoming) {
            const optimisticIndex = this.messages.findIndex(existing =>
              existing.isSending &&
              existing.receiverId === message.receiverId &&
              existing.messageType === message.messageType
            );

            if (optimisticIndex !== -1) {
              this.messages.splice(optimisticIndex, 1);
            }
          }

          message.isIncoming = message.senderId !== this.currentUserId;
          this.messages.push(message);
          this.scrollToBottom();

          if (message.isIncoming) {
            this.chatService.markAsRead(this.selectedContact.id).catch(() => { });
            this.playReceivedSound();
          }
        }
      } else if (message.senderId !== this.currentUserId) {
        this.playNotificationSound();
      }

      this.updateContactPreview(message);
    });
  }

  listenForTyping(): void {
    this.typingSub = this.signalR.typing$.subscribe(({ userId, isTyping }) => {
      const contact = this.contacts.find(item => item.id === userId);
      if (!contact) {
        return;
      }

      contact.isTyping = isTyping;
      if (isTyping) {
        setTimeout(() => {
          contact.isTyping = false;
        }, 3000);
      }
    });
  }

  listenForOnlineStatus(): void {
    this.onlineSub = this.signalR.onlineUsers$.subscribe(users => {
      this.onlineUsers = users;
      this.contacts.forEach(contact => {
        contact.isOnline = users.has(contact.id);
      });
    });
  }

  listenForReadReceipts(): void {
    this.readSub = this.signalR.messagesRead$.subscribe(readByUserId => {
      if (this.selectedContact?.id !== readByUserId) {
        return;
      }

      this.messages.forEach(message => {
        if (!message.isIncoming) {
          message.isRead = true;
        }
      });
    });
  }

  private updateContactPreview(message: Message) {
    const contactId = message.senderId === this.currentUserId ? message.receiverId : message.senderId;
    const contact = this.contacts.find(item => item.id === contactId);

    if (!contact) {
      return;
    }

    if (message.messageType === 'audio') {
      contact.lastMessage = 'رسالة صوتية';
    } else if (message.messageType === 'image') {
      contact.lastMessage = 'صورة';
    } else if (message.messageType === 'file') {
      contact.lastMessage = 'ملف مرفق';
    } else {
      contact.lastMessage = message.content;
    }

    contact.lastMessageTime = message.timestamp;
    contact.isTyping = false;

    if (message.senderId !== this.currentUserId && this.selectedContact?.id !== contactId) {
      contact.unreadCount = (contact.unreadCount || 0) + 1;
    }

    this.contacts.sort((a, b) => {
      const timeA = a.lastMessageTime ? new Date(a.lastMessageTime).getTime() : 0;
      const timeB = b.lastMessageTime ? new Date(b.lastMessageTime).getTime() : 0;
      return timeB - timeA;
    });
    this.filterContacts();
  }

  async sendMessage(): Promise<void> {
    if (!this.newMessage.trim() || !this.selectedContact) {
      return;
    }

    const messageContent = this.newMessage;
    this.newMessage = '';
    this.showEmojiPicker = false;
    this.showAttachMenu = false;

    const optimisticMessage: Message = {
      id: 'temp_' + Date.now(),
      senderId: this.currentUserId,
      receiverId: this.selectedContact.id,
      content: messageContent,
      timestamp: new Date(),
      isIncoming: false,
      isSending: true,
      messageType: 'text'
    };

    this.messages.push(optimisticMessage);
    this.scrollToBottom();

    try {
      let sentMessage: any;
      if (this.signalR.isConnected()) {
        sentMessage = await this.signalR.sendMessage(this.selectedContact.id, messageContent);
      } else {
        sentMessage = await this.chatService.sendMessage(this.selectedContact.id, messageContent);
      }

      this.playSentSound();
      optimisticMessage.isSending = false;

      if (sentMessage && sentMessage.id) {
        optimisticMessage.id = sentMessage.id.toString();
        if (sentMessage.sentAt) optimisticMessage.timestamp = sentMessage.sentAt;
      }

      this.notify.success('تم إرسال الرسالة بنجاح.', 'تم الإرسال');
    } catch (error) {
      console.error('Failed to send message:', error);
      optimisticMessage.isSending = false;
      this.messages = this.messages.filter(m => m.id !== optimisticMessage.id);
      this.notify.error('تعذر إرسال الرسالة. حاول مرة أخرى.');
    }

    if (this.selectedContact) {
      this.signalR.stopTyping(this.selectedContact.id);
    }
  }

  async copyMessage(content: string, event: Event): Promise<void> {
    event.stopPropagation();
    try {
      await navigator.clipboard.writeText(content);
      this.notify.success('تم نسخ الرسالة.', 'تم النسخ');
    } catch {
      this.notify.error('تعذر النسخ.');
    }
  }

  async deleteMessage(messageId: string, event: Event) {
    event.stopPropagation();

    // Don't allow deleting temp/optimistic messages
    if (messageId.startsWith('temp_')) {
      this.notify.info('الرسالة لا تزال قيد الإرسال.');
      return;
    }

    // First click = show confirm toast
    if (this.confirmDeleteId !== messageId) {
      this.confirmDeleteId = messageId;
      this.notify.warning('اضغط مرة أخرى للتأكيد وحذف الرسالة.', 'تأكيد الحذف');
      setTimeout(() => { this.confirmDeleteId = null; }, 3000);
      return;
    }

    // Second click = actually delete
    this.confirmDeleteId = null;
    try {
      await this.chatService.deleteMessage(messageId);
      this.messages = this.messages.filter(message => message.id !== messageId);
      this.notify.success('تم حذف الرسالة بنجاح.', 'تم الحذف');
    } catch (error) {
      console.error('Failed to delete message:', error);
      this.notify.error('تعذر حذف الرسالة.');
    }
  }

  toggleEmojiPicker() {
    this.showEmojiPicker = !this.showEmojiPicker;
    this.showAttachMenu = false;
  }

  addEmoji(emoji: string) {
    this.newMessage += emoji;
    this.showEmojiPicker = false;
    this.messageInput?.nativeElement?.focus();
  }

  toggleAttachMenu() {
    this.showAttachMenu = !this.showAttachMenu;
    this.showEmojiPicker = false;
  }

  async startRecording() {
    if (!this.selectedContact) {
      this.notify.info('اختر جهة اتصال أولًا قبل إرسال رسالة صوتية.');
      return;
    }

    if (!this.canUseVoiceRecording) {
      this.notify.warning('التسجيل الصوتي غير مدعوم في هذا المتصفح أو الجهاز.');
      return;
    }

    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const mimeType = this.getSupportedRecordingMimeType();
      const recorderOptions = mimeType ? { mimeType } : undefined;

      this.recordingStream = stream;
      this.mediaRecorder = recorderOptions ? new MediaRecorder(stream, recorderOptions) : new MediaRecorder(stream);
      this.audioChunks = [];
      this.shouldSendRecording = true;

      this.mediaRecorder.ondataavailable = (event: BlobEvent) => {
        if (event.data?.size > 0) {
          this.audioChunks.push(event.data);
        }
      };

      this.mediaRecorder.onstop = async () => {
        const activeContact = this.selectedContact;
        const mimeTypeToUse = this.mediaRecorder?.mimeType || mimeType || 'audio/webm';

        this.stopRecordingTracks();

        if (!this.shouldSendRecording || !activeContact || this.audioChunks.length === 0) {
          this.audioChunks = [];
          return;
        }

        const audioBlob = new Blob(this.audioChunks, { type: mimeTypeToUse });
        const extension = this.getAudioFileExtension(mimeTypeToUse);
        const audioFile = new File([audioBlob], `voice_${Date.now()}.${extension}`, { type: mimeTypeToUse });
        const tempUrl = URL.createObjectURL(audioBlob);

        const optimisticMessage: Message = {
          id: 'temp_audio_' + Date.now(),
          senderId: this.currentUserId,
          receiverId: activeContact.id,
          content: '',
          fileUrl: tempUrl,
          timestamp: new Date(),
          isIncoming: false,
          isSending: true,
          messageType: 'audio'
        };

        this.messages.push(optimisticMessage);
        this.scrollToBottom();

        try {
          const uploadResult = await this.chatService.uploadFile(audioFile);
          if (this.signalR.isConnected()) {
            await this.signalR.sendMessage(
              activeContact.id,
              '',
              uploadResult.fileUrl,
              uploadResult.fileName,
              'audio',
              uploadResult.fileSize,
              'audio'
            );
          } else {
            await this.chatService.sendMessage(
              activeContact.id,
              '',
              uploadResult.fileUrl,
              uploadResult.fileName,
              'audio',
              uploadResult.fileSize,
              'audio'
            );
          }

          optimisticMessage.isSending = false;
        } catch (error) {
          console.error('Failed to upload voice message:', error);
          optimisticMessage.isSending = false;
          this.notify.error('تعذر إرسال الرسالة الصوتية. حاول مرة أخرى.');
        } finally {
          URL.revokeObjectURL(tempUrl);
          this.audioChunks = [];
        }
      };

      this.mediaRecorder.start(250);
      this.isRecording = true;
      this.recordingDuration = 0;
      this.recordingInterval = setInterval(() => {
        this.recordingDuration++;
      }, 1000);
    } catch {
      this.stopRecordingTracks();
      this.notify.warning('لا يمكن الوصول إلى الميكروفون. تأكد من إعطاء الإذن للمتصفح.');
    }
  }

  stopRecording() {
    if (this.mediaRecorder && this.isRecording) {
      this.shouldSendRecording = true;
      this.mediaRecorder.stop();
      this.isRecording = false;
      clearInterval(this.recordingInterval);
    }
  }

  cancelRecording() {
    if (this.mediaRecorder && this.isRecording) {
      this.shouldSendRecording = false;
      this.mediaRecorder.stop();
      this.isRecording = false;
      clearInterval(this.recordingInterval);
      this.audioChunks = [];
      this.notify.info('تم إلغاء التسجيل.', 'إلغاء');
    }
  }

  formatRecordingTime(seconds: number): string {
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = seconds % 60;
    return `${minutes}:${remainingSeconds.toString().padStart(2, '0')}`;
  }

  async onFileSelected(event: any, type: string = 'file') {
    const file = event.target.files[0];
    if (!file || !this.selectedContact) {
      return;
    }

    this.showAttachMenu = false;

    const isImage = file.type.startsWith('image/');
    const messageType = isImage ? 'image' : 'file';
    const tempUrl = isImage ? URL.createObjectURL(file) : '';

    const optimisticMessage: Message = {
      id: 'temp_file_' + Date.now(),
      senderId: this.currentUserId,
      receiverId: this.selectedContact.id,
      content: '',
      imageUrl: isImage ? tempUrl : undefined,
      fileName: file.name,
      fileType: messageType,
      fileSize: file.size,
      timestamp: new Date(),
      isIncoming: false,
      isSending: true,
      messageType
    };

    this.messages.push(optimisticMessage);
    this.scrollToBottom();

    try {
      const uploadResult = await this.chatService.uploadFile(file);
      if (this.signalR.isConnected()) {
        await this.signalR.sendMessage(
          this.selectedContact.id,
          file.name,
          uploadResult.fileUrl,
          uploadResult.fileName,
          uploadResult.fileType,
          uploadResult.fileSize,
          messageType
        );
      } else {
        await this.chatService.sendMessage(
          this.selectedContact.id,
          file.name,
          uploadResult.fileUrl,
          uploadResult.fileName,
          uploadResult.fileType,
          uploadResult.fileSize,
          messageType
        );
      }

      optimisticMessage.isSending = false;
      optimisticMessage.fileUrl = uploadResult.fileUrl;
      optimisticMessage.fileName = uploadResult.fileName;
      optimisticMessage.fileType = uploadResult.fileType;
      optimisticMessage.fileSize = uploadResult.fileSize;
      if (isImage) {
        optimisticMessage.imageUrl = uploadResult.fileUrl;
      }
      this.notify.success('تم إرسال الملف بنجاح.', 'تم الإرسال');
    } catch (error) {
      console.error('Failed to upload file:', error);
      optimisticMessage.isSending = false;
      this.messages = this.messages.filter(m => m.id !== optimisticMessage.id);
      this.notify.error('تعذر رفع الملف. حاول مرة أخرى.');
    } finally {
      if (tempUrl) {
        URL.revokeObjectURL(tempUrl);
      }
    }

    event.target.value = '';
  }

  openImage(url?: string) {
    if (url) {
      this.imagePreview = url;
    }
  }

  closeImagePreview() {
    this.imagePreview = null;
  }

  getFileIcon(fileType?: string): string {
    switch (fileType) {
      case 'pdf':
        return 'fas fa-file-pdf';
      case 'document':
        return 'fas fa-file-word';
      case 'image':
        return 'fas fa-file-image';
      default:
        return 'fas fa-file';
    }
  }

  formatFileSize(bytes?: number): string {
    if (!bytes) {
      return '';
    }

    if (bytes < 1024) {
      return `${bytes} B`;
    }

    if (bytes < 1048576) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }

    return `${(bytes / 1048576).toFixed(1)} MB`;
  }

  getFullFileUrl(url?: string): string {
    if (!url) {
      return '';
    }

    if (url.startsWith('http') || url.startsWith('blob:')) {
      return url;
    }

    return `${environment.apiUrl}${url}`;
  }

  get canUseVoiceRecording(): boolean {
    return typeof window !== 'undefined'
      && 'MediaRecorder' in window
      && !!navigator.mediaDevices
      && typeof navigator.mediaDevices.getUserMedia === 'function';
  }

  toggleMessageSearch() {
    this.showMessageSearch = !this.showMessageSearch;
    this.messageSearchQuery = '';
    this.searchResults = [];
  }

  async searchInMessages() {
    if (!this.messageSearchQuery.trim()) {
      this.searchResults = [];
      return;
    }

    try {
      this.searchResults = await this.chatService.searchMessages(
        this.messageSearchQuery,
        this.selectedContact?.id
      );
    } catch {
      this.searchResults = [];
    }
  }

  onTyping(): void {
    if (!this.selectedContact) {
      return;
    }

    try {
      this.signalR.typing(this.selectedContact.id);
      clearTimeout(this.typingTimeout);
      this.typingTimeout = setTimeout(() => {
        if (this.selectedContact) {
          this.signalR.stopTyping(this.selectedContact.id);
        }
      }, 2000);
    } catch {
      // Ignore typing failures.
    }
  }

  getContactRoleLabel(contact: Contact): string {
    if (contact.role === 'Teacher') return 'معلم';
    if (contact.role === 'Student') return 'طالب';
    if (contact.role === 'Parent') return 'ولي أمر';
    if (contact.role === 'Admin') return 'إدارة';
    return '';
  }

  getContactRoleIcon(contact: Contact): string {
    if (contact.role === 'Teacher') return 'fas fa-chalkboard-teacher';
    if (contact.role === 'Student') return 'fas fa-user-graduate';
    if (contact.role === 'Parent') return 'fas fa-user-friends';
    if (contact.role === 'Admin') return 'fas fa-user-shield';
    return 'fas fa-user';
  }

  getSearchPlaceholder(): string {
    if (this.currentUserRole === 'Student') return 'بحث عن مدرس...';
    if (this.currentUserRole === 'Parent') return 'بحث عن مدرس...';
    if (this.currentUserRole === 'Teacher') return 'بحث عن طالب أو ولي أمر...';
    return 'بحث...';
  }

  isFirstMessageOfDay(index: number): boolean {
    if (index === 0) {
      return true;
    }

    const current = new Date(this.messages[index].timestamp);
    const previous = new Date(this.messages[index - 1].timestamp);
    return current.toDateString() !== previous.toDateString();
  }

  formatDate(date: Date): string {
    const value = new Date(date);
    const today = new Date();
    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);

    if (value.toDateString() === today.toDateString()) return 'اليوم';
    if (value.toDateString() === yesterday.toDateString()) return 'أمس';

    return value.toLocaleDateString('ar-EG', {
      weekday: 'long',
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  }

  formatTimeAgo(date: any): string {
    if (!date) {
      return '';
    }

    const value = new Date(date);
    const now = new Date();
    const diff = now.getTime() - value.getTime();
    const minutes = Math.floor(diff / 60000);

    if (minutes < 1) return 'الآن';
    if (minutes < 60) return `منذ ${minutes} دقيقة`;

    const today = new Date();
    if (value.toDateString() === today.toDateString()) {
      return value.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit' });
    }

    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);
    if (value.toDateString() === yesterday.toDateString()) {
      return 'أمس';
    }

    return value.toLocaleDateString('ar-EG', { month: 'short', day: 'numeric' });
  }

  getTotalUnread(): number {
    return this.contacts.reduce((sum, contact) => sum + (contact.unreadCount || 0), 0);
  }

  goBack() {
    this.selectedContact = null;
    this.showMessageSearch = false;
    this.showEmojiPicker = false;
    this.showAttachMenu = false;
  }

  private playSentSound() {
    this.unlockAudio();
    const audio = this.sentAudio?.nativeElement;
    if (audio) {
      audio.currentTime = 0;
      audio.volume = 0.42;
      audio.play().catch(() => this.playUiTone(740, 120, 920));
      return;
    }

    this.playUiTone(740, 120, 920);
  }

  private playReceivedSound() {
    this.unlockAudio();
    const audio = this.receivedAudio?.nativeElement;
    if (audio) {
      audio.currentTime = 0;
      audio.volume = 0.55;
      audio.play().catch(() => this.playUiTone(580, 180, 720));

      if ('vibrate' in navigator) {
        navigator.vibrate([100, 30, 100]);
      }
      return;
    }

    this.playUiTone(580, 180, 720);
  }

  private playNotificationSound() {
    this.unlockAudio();
    const audio = this.notifyAudio?.nativeElement;
    if (audio) {
      audio.currentTime = 0;
      audio.volume = 0.5;
      audio.play().catch(() => this.playUiTone(880, 200, 1080));

      if ('vibrate' in navigator) {
        navigator.vibrate(200);
      }
      return;
    }

    this.playUiTone(880, 200, 1080);
  }

  private playMessageSound() {
    this.playReceivedSound();
  }

  initWaveSurfer(message: Message) {
    if (this.wavesurfers.has(message.id) || (!message.fileUrl && !message.imageUrl)) {
      return;
    }

    setTimeout(() => {
      const containerId = '#waveform-' + message.id;
      const container = document.querySelector(containerId);
      if (!container) {
        return;
      }

      const waveSurfer = WaveSurfer.create({
        container: containerId,
        waveColor: message.isIncoming ? '#94a3b8' : '#cbd5e1',
        progressColor: message.isIncoming ? '#128C7E' : '#ffffff',
        cursorColor: 'transparent',
        barWidth: 2,
        barGap: 3,
        barRadius: 2,
        height: 30,
        url: this.getFullFileUrl(message.fileUrl || message.imageUrl)
      });

      waveSurfer.on('finish', () => {
        message.isPlaying = false;
      });

      this.wavesurfers.set(message.id, waveSurfer);
    }, 50);
  }

  toggleAudio(message: Message) {
    const waveSurfer = this.wavesurfers.get(message.id);
    if (!waveSurfer) {
      this.initWaveSurfer(message);
      setTimeout(() => this.toggleAudio(message), 100);
      return;
    }

    if (message.isPlaying) {
      waveSurfer.pause();
      message.isPlaying = false;
      return;
    }

    this.messages.forEach(item => {
      if (item.id !== message.id && item.isPlaying) {
        this.wavesurfers.get(item.id)?.pause();
        item.isPlaying = false;
      }
    });

    waveSurfer.play();
    message.isPlaying = true;
  }

  setPlaybackRate(message: Message, rate: number) {
    const waveSurfer = this.wavesurfers.get(message.id);
    if (!waveSurfer) {
      return;
    }

    waveSurfer.setPlaybackRate(rate);
    message.playbackRate = rate;
  }

  scrollToBottom(): void {
    setTimeout(() => {
      const element = this.chatMessagesContainer?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    }, 100);
  }

  ngOnDestroy(): void {
    this.messageSub?.unsubscribe();
    this.typingSub?.unsubscribe();
    this.onlineSub?.unsubscribe();
    this.readSub?.unsubscribe();
    this.routeSub?.unsubscribe();
    clearTimeout(this.typingTimeout);
    clearInterval(this.recordingInterval);
    this.stopRecordingTracks();
    this.mutationObserver?.disconnect();
    this.wavesurfers.forEach(waveSurfer => waveSurfer.destroy());
  }

  private getSupportedRecordingMimeType(): string | undefined {
    if (typeof window === 'undefined' || !('MediaRecorder' in window)) {
      return undefined;
    }

    const supportedTypes = [
      'audio/webm;codecs=opus',
      'audio/webm',
      'audio/ogg;codecs=opus',
      'audio/mp4'
    ];

    return supportedTypes.find(type => MediaRecorder.isTypeSupported(type));
  }

  private getAudioFileExtension(mimeType: string): string {
    if (mimeType.includes('ogg')) {
      return 'ogg';
    }

    if (mimeType.includes('mp4')) {
      return 'm4a';
    }

    return 'webm';
  }

  private stopRecordingTracks(): void {
    this.recordingStream?.getTracks().forEach(track => track.stop());
    this.recordingStream = null;
  }

  private playUiTone(primaryFrequency: number, durationMs: number, secondaryFrequency?: number): void {
    try {
      const audioContext = new (window.AudioContext || (window as any).webkitAudioContext)();
      const oscillator = audioContext.createOscillator();
      const gainNode = audioContext.createGain();

      oscillator.connect(gainNode);
      gainNode.connect(audioContext.destination);

      oscillator.frequency.setValueAtTime(primaryFrequency, audioContext.currentTime);
      if (secondaryFrequency) {
        oscillator.frequency.linearRampToValueAtTime(
          secondaryFrequency,
          audioContext.currentTime + durationMs / 1000
        );
      }

      gainNode.gain.setValueAtTime(0.12, audioContext.currentTime);
      gainNode.gain.exponentialRampToValueAtTime(0.01, audioContext.currentTime + durationMs / 1000);

      oscillator.start(audioContext.currentTime);
      oscillator.stop(audioContext.currentTime + durationMs / 1000);
    } catch {
      // Ignore sound failures and keep chat working.
    }
  }
}
