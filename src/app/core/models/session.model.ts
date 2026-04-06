export interface Session {
    id: number;
    subjectName?: string;
    subjectId?: number;
    gradeName?: string;
    classRoomName?: string;
    className?: string; // Legacy
    teacherName?: string;
    startTime: string | Date;
    endTime: string | Date;
    date?: string;
    isActive?: boolean;
    isRecorded?: boolean;
    studentCount?: number;
    attendanceCount?: number;
    classRoomId?: number;
}
