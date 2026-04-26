export interface Message {
  id: string;
  senderId: string;
  receiverId: string;
  content: string;
  timestamp: Date;
  sentAt?: Date;
  isIncoming?: boolean;
  isRead?: boolean;
  
  // Media support
  imageUrl?: string;
  fileUrl?: string;
  fileName?: string;
  fileType?: string; // text, image, audio, pdf, document, file
  fileSize?: number;
  messageType?: string; // text, image, audio, file
  
  // UI state
  isDeleted?: boolean;
  isSending?: boolean;
  isPlaying?: boolean;
  playbackRate?: number;
}

export interface Contact {
  id: string;
  name: string;
  avatar?: string;
  lastMessage?: string;
  lastMessageTime?: Date;
  unreadCount: number;
  isOnline: boolean;
  studentName?: string;
  role?: string;      // Student | Teacher | Parent
  className?: string;
  subjectName?: string;
  isTyping?: boolean;
}
