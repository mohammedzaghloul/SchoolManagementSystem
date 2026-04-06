export interface Exam {
    id: number;
    title: string;
    description?: string;
    subjectName?: string;
    subjectId?: number;
    subject?: string;
    classRoomId?: number;
    classRoomName?: string;
    classRoom?: string;
    date: string;
    startTime: string;
    endTime?: string;
    duration: number; // in minutes
    totalMarks: number;
    maxScore?: number;
    passingMarks: number;
    status?: string;
    isCompleted?: boolean;
    score?: number;
    notes?: string;
    questions?: any[];
    timeUntil?: string;
}
