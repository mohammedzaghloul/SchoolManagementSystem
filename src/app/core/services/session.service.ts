// core/services/session.service.ts
import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { Session } from '../models/session.model';

interface LegacyAttendanceRecord {
  sessionId?: number;
  status?: string;
  isPresent?: boolean;
  method?: string;
  recordedAt?: string;
  classRoomName?: string;
}

interface LegacyStudentDashboard {
  className?: string;
  gradeLevel?: string;
  todaySessions?: any[];
  nextSession?: any;
}

export interface AdminScheduleSessionItem {
  id: number;
  sessionDate: string;
  term?: string;
  subjectName: string;
  teacherName: string;
  classRoomName: string;
  gradeName: string;
  startTime: string;
  endTime: string;
  attendanceType: string;
  studentCount: number;
  attendanceCount: number;
}

export interface AdminScheduleOverview {
  startDate: string;
  endDate: string;
  term: string;
  totalSessions: number;
  totalTeachers: number;
  totalClasses: number;
  scheduledToday: number;
  items: AdminScheduleSessionItem[];
}

export interface GenerateTermSchedulePayload {
  startDate: string;
  endDate: string;
  term: string;
}

export interface GenerateTermScheduleResult {
  startDate: string;
  endDate: string;
  term: string;
  plannedSlots: number;
  createdCount: number;
  existingCount: number;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class SessionService {
  constructor(private api: ApiService) { }

  async getActiveSessions(): Promise<Session[]> {
    return this.api.get<Session[]>('/api/Sessions/active');
  }

  async getMyAttendanceContext(): Promise<any> {
    try {
      return await this.api.get('/api/Sessions/me/attendance-context');
    } catch (error: any) {
      if (Number(error?.status) !== 404) {
        throw error;
      }

      return this.getLegacyAttendanceContext();
    }
  }

  async getAllSessions(): Promise<Session[]> {
    return this.api.get<Session[]>('/api/Sessions');
  }

  async getTeacherSessions(teacherId?: string | number, date?: string): Promise<Session[]> {
    const url = teacherId ? `/api/Sessions/teacher/${teacherId}` : '/api/Sessions/active';
    const params = date && teacherId ? { date } : undefined;
    return this.api.get<Session[]>(url, params);
  }

  async getSessionById(id: number): Promise<Session> {
    return this.api.get<Session>(`/api/Sessions/${id}`);
  }

  async createSession(data: any): Promise<Session> {
    return this.api.post<Session>('/api/Sessions', data);
  }

  async getAdminScheduleOverview(startDate: string, endDate: string, term: string = 'all'): Promise<AdminScheduleOverview> {
    return this.api.get<AdminScheduleOverview>('/api/Sessions/admin/schedule-overview', {
      startDate,
      endDate,
      term
    });
  }

  async generateTermSchedule(payload: GenerateTermSchedulePayload): Promise<GenerateTermScheduleResult> {
    return this.api.post<GenerateTermScheduleResult>('/api/Sessions/admin/generate-term-schedule', payload);
  }

  async getQRCode(sessionId: number): Promise<{ qrCode: string }> {
    return this.api.get<{ qrCode: string }>(`/api/Sessions/${sessionId}/qr`);
  }

  async refreshQRCode(sessionId: number): Promise<{ qrCode: string }> {
    return this.api.post<{ qrCode: string }>(`/api/Sessions/${sessionId}/refresh-qr`, {});
  }

  async activateSession(sessionId: number): Promise<void> {
    return this.api.post(`/api/Sessions/${sessionId}/activate`, {});
  }

  async deactivateSession(sessionId: number): Promise<void> {
    return this.api.post(`/api/Sessions/${sessionId}/deactivate`, {});
  }

  private async getLegacyAttendanceContext(): Promise<any> {
    const [dashboard, attendanceResponse] = await Promise.all([
      this.api.get<LegacyStudentDashboard>('/api/Dashboards/student'),
      this.api.get('/api/Attendance/me').catch(() => ({ records: [] }))
    ]);

    const records = this.extractAttendanceRecords(attendanceResponse);
    const attendanceBySessionId = new Map<number, LegacyAttendanceRecord>();

    records.forEach(record => {
      const sessionId = Number(record?.sessionId ?? 0);
      if (sessionId && !attendanceBySessionId.has(sessionId)) {
        attendanceBySessionId.set(sessionId, record);
      }
    });

    const fallbackClassName =
      dashboard?.className ||
      records.find(record => !!record?.classRoomName)?.classRoomName ||
      '';

    const todayReference = new Date();
    const todaySessions = Array.isArray(dashboard?.todaySessions)
      ? dashboard.todaySessions.map((session: any) =>
          this.normalizeLegacySession(session, attendanceBySessionId, fallbackClassName, todayReference)
        )
      : [];

    const activeSession = todaySessions.find((session: any) => session.isActive) || null;

    const normalizedNextSession = dashboard?.nextSession
      ? this.normalizeLegacySession(dashboard.nextSession, attendanceBySessionId, fallbackClassName, todayReference)
      : null;

    const nextSession = normalizedNextSession || this.findNextSession(todaySessions);

    return {
      className: fallbackClassName,
      gradeLevel: dashboard?.gradeLevel || '',
      activeSession,
      nextSession,
      todaySessions
    };
  }

  private extractAttendanceRecords(response: any): LegacyAttendanceRecord[] {
    if (Array.isArray(response)) {
      return response;
    }

    if (Array.isArray(response?.records)) {
      return response.records;
    }

    return [];
  }

  private normalizeLegacySession(
    session: any,
    attendanceBySessionId: Map<number, LegacyAttendanceRecord>,
    fallbackClassName: string,
    referenceDate: Date
  ): any {
    const sessionId = Number(session?.id ?? session?.Id ?? 0);
    const attendance = sessionId ? attendanceBySessionId.get(sessionId) : undefined;
    const startTime = this.normalizeDateTime(session?.startTime ?? session?.StartTime, referenceDate);
    const endTime = this.normalizeDateTime(session?.endTime ?? session?.EndTime, referenceDate);
    const now = new Date();
    const startDate = startTime ? new Date(startTime) : null;
    const endDate = endTime ? new Date(endTime) : null;
    const attendanceType = String(
      session?.attendanceType ??
      session?.AttendanceType ??
      session?.type ??
      session?.Type ??
      'QR'
    );
    const attendanceRecorded = !!attendance;
    const status = attendance?.status || (attendance?.isPresent ? 'Present' : undefined);

    return {
      id: sessionId,
      title: session?.title ?? session?.Title ?? session?.subjectName ?? session?.SubjectName ?? session?.subject ?? session?.Subject ?? 'Session',
      subjectName: session?.subjectName ?? session?.SubjectName ?? session?.subject ?? session?.Subject ?? session?.title ?? session?.Title ?? 'Session',
      teacherName: session?.teacherName ?? session?.TeacherName ?? '',
      classRoomName: session?.classRoomName ?? session?.ClassRoomName ?? fallbackClassName,
      startTime,
      endTime,
      attendanceType,
      isLive: !!(session?.isLive ?? session?.IsLive),
      isActive: !!(startDate && endDate && startDate <= now && endDate >= now),
      isCompleted: !!(endDate && endDate < now),
      attendanceRecorded,
      attendanceStatus: status,
      attendanceMethod: attendance?.method,
      canMarkWithQr: !!(startDate && endDate && startDate <= now && endDate >= now && !attendanceRecorded && attendanceType.toLowerCase() === 'qr')
    };
  }

  private findNextSession(todaySessions: any[]): any | null {
    const now = new Date().getTime();

    return todaySessions
      .filter(session => !!session?.startTime && new Date(session.startTime).getTime() > now)
      .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime())[0] || null;
  }

  private normalizeDateTime(value: unknown, referenceDate: Date): string | undefined {
    if (!value) {
      return undefined;
    }

    if (value instanceof Date) {
      return value.toISOString();
    }

    const text = String(value).trim();
    if (!text) {
      return undefined;
    }

    const timeOnlyMatch = text.match(/^(\d{1,2}):(\d{2})(?::(\d{2}))?$/);
    if (timeOnlyMatch) {
      const [, hours, minutes, seconds] = timeOnlyMatch;
      const parsed = new Date(referenceDate);
      parsed.setHours(Number(hours), Number(minutes), Number(seconds || 0), 0);
      return parsed.toISOString();
    }

    const parsed = new Date(text);
    if (!Number.isNaN(parsed.getTime())) {
      return parsed.toISOString();
    }

    return undefined;
  }
}
