import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { SessionService } from '../../../../core/services/session.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Session } from '../../../../core/models/session.model';

interface WeekDay {
  label: string;
  dateKey: string;
  dateLabel: string;
  isToday: boolean;
  sessionCount: number;
}

interface TimetableSession extends Session {
  normalizedStart: Date;
  normalizedEnd: Date | null;
  dateKey: string;
  slotKey: string;
  startLabel: string;
  endLabel: string;
}

@Component({
  selector: 'app-teacher-timetable',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './teacher-timetable.component.html',
  styleUrl: './teacher-timetable.component.css'
})
export class TeacherTimetableComponent implements OnInit {
  sessions: Session[] = [];
  normalizedSessions: TimetableSession[] = [];
  weekSessions: TimetableSession[] = [];
  weekDays: WeekDay[] = [];
  timeSlots: string[] = [];
  timetableMatrix: Record<string, Record<string, TimetableSession[]>> = {};

  loading = false;
  error = '';
  teacherName = '';
  selectedWeekStart = this.getWeekStart(new Date());

  private readonly dayNames = ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];
  private readonly schoolWeekDayOffsets = [0, 1, 2, 3, 4, 5]; // السبت -> الخميس
  private readonly defaultTimeSlots = ['08:00', '09:00', '10:00', '11:00', '12:00', '13:00'];

  constructor(
    private sessionService: SessionService,
    private auth: AuthService
  ) {}

  async ngOnInit(): Promise<void> {
    const user = this.auth.getCurrentUser();
    this.teacherName = user?.fullName || 'المعلم';
    await this.loadTimetable();
  }

  get totalWeekSessions(): number {
    return this.weekSessions.length;
  }

  get totalClasses(): number {
    return new Set(this.weekSessions.map(session => session.classRoomName || session.className).filter(Boolean)).size;
  }

  get maxDailySessions(): number {
    return Math.max(...this.weekDays.map(day => day.sessionCount), 0);
  }

  get nextSession(): TimetableSession | null {
    const now = new Date();
    return this.weekSessions
      .filter(session => session.normalizedStart.getTime() >= now.getTime())
      .sort((first, second) => first.normalizedStart.getTime() - second.normalizedStart.getTime())[0] || null;
  }

  get weekRangeLabel(): string {
    const end = this.addDays(this.selectedWeekStart, 5);
    return `${this.formatShortDate(this.selectedWeekStart)} - ${this.formatShortDate(end)}`;
  }

  async loadTimetable(): Promise<void> {
    this.loading = true;
    this.error = '';

    try {
      const user = this.auth.getCurrentUser();
      const response = user?.id
        ? await this.loadTeacherWeekSessions(user.id)
        : await this.sessionService.getActiveSessions();

      this.sessions = Array.isArray(response) ? response : [];
      this.normalizedSessions = this.sessions
        .map(session => this.normalizeSession(session))
        .filter((session): session is TimetableSession => !!session)
        .sort((first, second) => first.normalizedStart.getTime() - second.normalizedStart.getTime());

      this.buildMatrix();
    } catch (err) {
      this.sessions = [];
      this.normalizedSessions = [];
      this.weekSessions = [];
      this.error = 'تعذر تحميل جدول الحصص للمدرس حالياً.';
      console.error(err);
      this.buildMatrix();
    } finally {
      this.loading = false;
    }
  }

  shiftWeek(amount: number): void {
    this.selectedWeekStart = this.addDays(this.selectedWeekStart, amount * 7);
    void this.loadTimetable();
  }

  goToCurrentWeek(): void {
    this.selectedWeekStart = this.getWeekStart(new Date());
    void this.loadTimetable();
  }

  getSessionsForSlot(day: WeekDay, time: string): TimetableSession[] {
    return this.timetableMatrix[day.dateKey]?.[time] || [];
  }

  getSubjectClass(subjectName: string | undefined): string {
    if (!subjectName) {
      return 'subject-card--blue';
    }

    if (subjectName.includes('رياضيات')) return 'subject-card--blue';
    if (subjectName.includes('علوم')) return 'subject-card--green';
    if (subjectName.includes('عربية')) return 'subject-card--violet';
    if (subjectName.includes('إنجليزية') || subjectName.includes('English')) return 'subject-card--amber';

    return 'subject-card--rose';
  }

  trackByDay(_: number, day: WeekDay): string {
    return day.dateKey;
  }

  trackBySlot(_: number, slot: string): string {
    return slot;
  }

  trackBySession(_: number, session: TimetableSession): number {
    return session.id;
  }

  private buildMatrix(): void {
    this.weekDays = this.schoolWeekDayOffsets.map(offset => {
      const date = this.addDays(this.selectedWeekStart, offset);
      return {
        label: this.dayNames[date.getDay()],
        dateKey: this.getDateKey(date),
        dateLabel: date.toLocaleDateString('ar-EG', { day: 'numeric', month: 'short' }),
        isToday: this.getDateKey(date) === this.getDateKey(new Date()),
        sessionCount: 0
      };
    });

    const weekDateKeys = new Set(this.weekDays.map(day => day.dateKey));
    this.weekSessions = this.normalizedSessions.filter(session => weekDateKeys.has(session.dateKey));

    this.weekDays = this.weekDays.map(day => ({
      ...day,
      sessionCount: this.weekSessions.filter(session => session.dateKey === day.dateKey).length
    }));

    this.timeSlots = Array.from(new Set([
      ...this.defaultTimeSlots,
      ...this.weekSessions.map(session => session.slotKey)
    ])).sort();

    this.timetableMatrix = this.weekDays.reduce<Record<string, Record<string, TimetableSession[]>>>((matrix, day) => {
      matrix[day.dateKey] = this.timeSlots.reduce<Record<string, TimetableSession[]>>((slots, slot) => {
        slots[slot] = [];
        return slots;
      }, {});
      return matrix;
    }, {});

    this.weekSessions.forEach(session => {
      if (!this.timetableMatrix[session.dateKey]) {
        return;
      }

      if (!this.timetableMatrix[session.dateKey][session.slotKey]) {
        this.timetableMatrix[session.dateKey][session.slotKey] = [];
      }

      this.timetableMatrix[session.dateKey][session.slotKey].push(session);
    });
  }

  private async loadTeacherWeekSessions(userId: string | number): Promise<Session[]> {
    const dateKeys = this.schoolWeekDayOffsets.map(offset =>
      this.getDateKey(this.addDays(this.selectedWeekStart, offset))
    );

    const dailySessions = await Promise.all(
      dateKeys.map(dateKey =>
        this.sessionService.getTeacherSessions(userId, dateKey).catch((): Session[] => [])
      )
    );

    const sessionsByKey = new Map<string, Session>();
    dailySessions.flat().forEach(session => {
      const key = `${session.id}-${session.sessionDate || session.date || session.startTime}`;
      sessionsByKey.set(key, session);
    });

    return Array.from(sessionsByKey.values());
  }

  private normalizeSession(session: Session): TimetableSession | null {
    const normalizedStart = this.resolveDateTime(session, 'startTime');
    if (!normalizedStart) {
      return null;
    }

    const normalizedEnd = this.resolveDateTime(session, 'endTime', normalizedStart);
    const slotKey = this.formatSlot(normalizedStart);

    return {
      ...session,
      normalizedStart,
      normalizedEnd,
      dateKey: this.getDateKey(normalizedStart),
      slotKey,
      startLabel: this.formatTime(normalizedStart),
      endLabel: normalizedEnd ? this.formatTime(normalizedEnd) : ''
    };
  }

  private resolveDateTime(session: Session, field: 'startTime' | 'endTime', fallbackDate?: Date): Date | null {
    const value = session[field];
    if (!value) {
      return null;
    }

    if (value instanceof Date) {
      return value;
    }

    const text = String(value).trim();
    if (!text) {
      return null;
    }

    const timeOnlyMatch = text.match(/^(\d{1,2}):(\d{2})(?::(\d{2}))?$/);
    if (timeOnlyMatch) {
      const baseDate = this.resolveBaseDate(session, fallbackDate);
      if (!baseDate) {
        return null;
      }

      const [, hours, minutes, seconds] = timeOnlyMatch;
      const parsed = new Date(baseDate);
      parsed.setHours(Number(hours), Number(minutes), Number(seconds || 0), 0);
      return parsed;
    }

    const parsed = new Date(text);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }

  private resolveBaseDate(session: Session, fallbackDate?: Date): Date | null {
    const rawDate = session.sessionDate || session.date;

    if (rawDate) {
      const text = String(rawDate);
      const datePart = text.includes('T') ? text.split('T')[0] : text;
      const parsed = new Date(`${datePart}T00:00:00`);
      if (!Number.isNaN(parsed.getTime())) {
        return parsed;
      }
    }

    return fallbackDate ? new Date(fallbackDate) : null;
  }

  private getWeekStart(date: Date): Date {
    const start = new Date(date);
    const daysSinceSaturday = (start.getDay() - 6 + 7) % 7;
    start.setDate(start.getDate() - daysSinceSaturday);
    start.setHours(0, 0, 0, 0);
    return start;
  }

  private addDays(date: Date, days: number): Date {
    const result = new Date(date);
    result.setDate(result.getDate() + days);
    return result;
  }

  private getDateKey(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private formatSlot(date: Date): string {
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    return `${hours}:${minutes}`;
  }

  private formatTime(date: Date): string {
    return date.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', hour12: true });
  }

  private formatShortDate(date: Date): string {
    return date.toLocaleDateString('ar-EG', { day: 'numeric', month: 'short' });
  }
}
