export interface Teacher {
    id: number;
    fullName: string;
    email: string;
    phone?: string;
    address?: string;
    specialization?: string;
    bio?: string;
    isActive: boolean;
    avatar?: string;
    subjectId?: number | null;
    subjectName?: string;
    subjects?: string[];
    classes?: string[];
}
