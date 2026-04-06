export interface Subject {
    id: number;
    name: string;
    code?: string;
    description?: string;
    term?: string;
    isActive?: boolean;
    creditHours?: number;
    classRoomId?: number;
}
