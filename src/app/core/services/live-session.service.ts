import { Injectable } from '@angular/core';
import { ApiService } from './api.service';

export interface AgoraConfig {
    appId: string;
    channelName: string;
    token: string;
    uid: number;
}

@Injectable({
    providedIn: 'root'
})
export class LiveSessionService {
    constructor(private api: ApiService) { }

    async startSession(sessionId: number): Promise<AgoraConfig> {
        return this.api.post<AgoraConfig>(`/api/LiveSessions/start/${sessionId}`, {});
    }

    async joinSession(sessionId: number): Promise<AgoraConfig> {
        return this.api.get<AgoraConfig>(`/api/LiveSessions/join/${sessionId}`);
    }

    async endSession(sessionId: number): Promise<any> {
        return this.api.post(`/api/LiveSessions/end/${sessionId}`, {});
    }
}
