import { Injectable } from '@angular/core';
import { ApiService } from './api.service';

@Injectable({
    providedIn: 'root'
})
export class DashboardService {
    constructor(private api: ApiService) { }

    async getAdminStats(): Promise<any> {
        return this.api.get('/api/Dashboards/admin');
    }

    async getTeacherStats(): Promise<any> {
        return this.api.get('/api/Dashboards/teacher');
    }

    async getStudentStats(): Promise<any> {
        return this.api.get('/api/Dashboards/student');
    }

    async getParentStats(): Promise<any> {
        return this.api.get('/api/Dashboards/parent');
    }
}
