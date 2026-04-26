import { Injectable } from '@angular/core';
import { ApiService } from './api.service';

export interface VideoInfo {
    id?: number;
    title: string;
    description: string;
    url: string;
    thumbnailUrl: string;
    duration: string;
    subjectId: number;
}

@Injectable({
    providedIn: 'root'
})
export class VideoService {
    constructor(private api: ApiService) { }

    async getVideos(subjectId?: number): Promise<any[]> {
        const params = subjectId ? { subjectId } : {};
        return this.api.get<any[]>('/api/Videos', params);
    }

    async addVideo(video: VideoInfo): Promise<any> {
        return this.api.post('/api/Videos', video);
    }

    async incrementViews(id: number): Promise<{ views: number }> {
        return this.api.post<{ views: number }>(`/api/Videos/${id}/view`, {});
    }
}
