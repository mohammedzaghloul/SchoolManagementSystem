import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { SessionService } from '../../../../core/services/session.service';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
    selector: 'app-attendance-sessions',
    standalone: true,
    imports: [CommonModule, RouterModule],
    templateUrl: './attendance-sessions.component.html',
    styleUrls: ['./attendance-sessions.component.css']
})
export class AttendanceSessionsComponent implements OnInit {
    sessions: any[] = [];
    loading = false;
    showSuccess = false;
    errorMessage = '';

    constructor(
        private sessionService: SessionService,
        private authService: AuthService
    ) { }

    async ngOnInit() {
        this.loading = true;
        this.errorMessage = '';
        
        try {
            const user = this.authService.getCurrentUser();
            const teacherId = user ? user.id : undefined;
            const res: any = await this.sessionService.getTeacherSessions(teacherId);
            this.sessions = Array.isArray(res) ? res : res?.data || [];
            
            console.log(`[Attendance] Loaded ${this.sessions.length} sessions from DB`);
        } catch (error: any) {
            console.error('[Attendance] DB Connection failed:', error);
            this.errorMessage = error?.message || 'فشل الاتصال بقاعدة البيانات';
            this.sessions = [];
        }
        
        this.loading = false;
    }

    formatTime(time: any): string {
        if (!time) return '—';
        // If it's an ISO date string like "2026-04-05T08:00:00"
        if (typeof time === 'string' && time.includes('T')) {
            const d = new Date(time);
            return d.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
        }
        // If it's a plain time string like "08:00:00" or "08:00"
        if (typeof time === 'string') {
            const parts = time.split(':');
            let hours = parseInt(parts[0], 10);
            const minutes = parts[1] || '00';
            const period = hours >= 12 ? 'م' : 'ص';
            if (hours > 12) hours -= 12;
            if (hours === 0) hours = 12;
            return `${hours}:${minutes} ${period}`;
        }
        // If it's a Date object
        if (time instanceof Date) {
            return time.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
        }
        return String(time);
    }
}
