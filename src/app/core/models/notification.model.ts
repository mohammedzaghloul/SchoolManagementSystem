export interface Notification {
    id: number;
    title: string;
    message: string;
    type: 'attendance' | 'grade' | 'exam' | 'payment' | 'event' | 'message' | 'warning';
    isRead: boolean;
    createdAt: Date;
    data?: any;
}
