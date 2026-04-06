export interface Video {
    id: number;
    url: string;
    title: string;
    description: string;
    subject: string;
    grade: string;
    thumbnail: string;
    thumbnailUrl?: string;
    duration: string;
    views: number;
    isHidden?: boolean;
    subjectId?: number;
    gradeLevelId?: number;
    gradeName?: string;
}
