export interface Grade {
    id: number;
    studentId: number;
    studentName?: string;
    subjectId: number;
    subjectName?: string;
    value: number;
    date: string;
    remarks?: string;
    term?: string;
    academicYear?: string;
}

export interface GradeLevel {
    id: number;
    name: string;
    description?: string;
}
