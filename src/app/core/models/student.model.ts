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
