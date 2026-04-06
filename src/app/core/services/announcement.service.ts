import { Injectable } from '@angular/core';
import { ApiService } from './api.service';

export interface Announcement {
    id?: number;
    title: string;
    content: string;
    createdAt: Date;
    audience: 'All' | 'Teachers' | 'Students' | 'Parents';
    createdBy?: string;
}

@Injectable({
    providedIn: 'root'
})
export class AnnouncementService {
    constructor(private api: ApiService) { }

    async getAnnouncements(): Promise<Announcement[]> {
        return this.api.get<Announcement[]>('/api/Announcement');
    }

    async createAnnouncement(announcement: Announcement): Promise<Announcement> {
        return this.api.post<Announcement>('/api/Announcement', announcement);
    }
}
