export interface Student {
    id: number;
    fullName: string;
    email: string;
    phone?: string;
    address?: string;
    dateOfBirth?: string;
    gender?: string;
    classRoomId?: number;
    classRoomName?: string;
    gradeId?: number;
    gradeLevelId?: number;
    gradeName?: string;
    gradeLevelName?: string;
    parentName?: string;
    parentId?: number;
    isActive: boolean;
    avatar?: string;
}

export interface StudentFilter {
    pageIndex?: number;
    pageSize?: number;
    classRoomId?: number;
    search?: string;
    sort?: string;
    activeOnly?: boolean;
}

export interface PaginatedResponse<T> {
    items: T[];
    data?: T[]; // Support both item naming conventions
    totalCount: number;
    pageIndex: number;
    pageSize: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
}

export interface StudentSearchResult {
    id: number;
    fullName: string;
    email?: string;
    phone?: string;
    classRoomName?: string;
    gradeLevelName?: string;
    code?: string;
    isActive: boolean;
}

export interface StudentDashboard {
    id: number;
    name: string;
    email?: string;
    phone?: string;
    parentName?: string;
    classRoomName?: string;
    gradeLevelName?: string;
    academicYear?: string;
    code?: string;
    avg: number;
    attendance: number;
    activity: number;
    gradesCompleted: boolean;
    totalSubjects: number;
    approvedSubjects: number;
    gradesStatus: 'COMPLETED' | 'IN_PROGRESS' | string;
    gradesStatusLabel: string;
    lastAttendance?: StudentAttendanceSummary | null;
    grades: StudentGradeSummary[];
    attendanceRecords: StudentAttendanceSummary[];
    assignments: StudentAssignmentSummary[];
    alerts: StudentAlert[];
}

export interface StudentGradeSummary {
    id: number;
    subjectId: number;
    subjectName: string;
    teacherName?: string;
    gradeType: string;
    score: number;
    date: string;
    notes?: string;
    isApproved: boolean;
    approvalStatus: 'COMPLETED' | 'IN_PROGRESS' | string;
}

export interface StudentAttendanceSummary {
    id: number;
    date: string;
    status: string;
    isPresent: boolean;
    method?: string;
    sessionTitle?: string;
    subjectName?: string;
    recordedAt: string;
}

export interface StudentAssignmentSummary {
    id: number;
    title: string;
    subjectName?: string;
    teacherName?: string;
    dueDate: string;
    isSubmitted: boolean;
    isLate: boolean;
    status: 'SUBMITTED' | 'LATE' | 'PENDING' | string;
    statusLabel: string;
    submittedAt?: string | null;
    grade?: number | null;
    teacherFeedback?: string;
}

export interface StudentAlert {
    type: 'info' | 'warning' | 'danger' | string;
    title: string;
    message: string;
}
