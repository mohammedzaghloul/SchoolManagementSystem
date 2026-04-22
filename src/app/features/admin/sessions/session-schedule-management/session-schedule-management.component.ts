import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

import {
  AdminScheduleOverview,
  AdminScheduleSessionItem,
  CreateAdminSessionPayload,
  CreateAdminSessionResult,
  GenerateTermScheduleResult,
  SessionService
} from '../../../../core/services/session.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { TeacherService } from '../../../../core/services/teacher.service';
import { ClassRoomService } from '../../../../core/services/classroom.service';

interface TimeSlotOption {
  key: string;
  label: string;
  startTime: string;
  endTime: string;
}

interface CreateSessionFormState {
  teacherId: number | null;
  subjectId: number | null;
  classRoomId: number | null;
  sessionDate: string;
  slotKey: string;
  attendanceType: 'QR' | 'Face' | 'Manual';
  title: string;
}

@Component({
  selector: 'app-session-schedule-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './session-schedule-management.component.html',
  styleUrls: ['./session-schedule-management.component.css']
})
export class SessionScheduleManagementComponent implements OnInit {
  overview: AdminScheduleOverview | null = null;
  sessions: AdminScheduleSessionItem[] = [];
  teachers: any[] = [];
  classRooms: any[] = [];
  teacherSubjects: any[] = [];
  loading = false;
  generating = false;
  creatingSession = false;
  loadingCreateMeta = false;
  error = '';
  createError = '';
  lastGeneration: GenerateTermScheduleResult | null = null;
  lastCreatedSession: CreateAdminSessionResult | null = null;
  selectedTerm = 'all';
  readonly termOptions = [
    { value: 'all', label: 'كل الترمات' },
    { value: 'الترم الأول', label: 'الترم الأول' },
    { value: 'الترم الثاني', label: 'الترم الثاني' }
  ];

  readonly attendanceTypeOptions: Array<{ value: 'QR' | 'Face' | 'Manual'; label: string }> = [
    { value: 'QR', label: 'QR' },
    { value: 'Face', label: 'Face ID' },
    { value: 'Manual', label: 'يدوي' }
  ];

  readonly baseTimeSlots: TimeSlotOption[] = [
    { key: 'slot-1', label: 'الحصة الأولى 08:00 - 08:45', startTime: '08:00:00', endTime: '08:45:00' },
    { key: 'slot-2', label: 'الحصة الثانية 09:00 - 09:45', startTime: '09:00:00', endTime: '09:45:00' },
    { key: 'slot-3', label: 'الحصة الثالثة 10:00 - 10:45', startTime: '10:00:00', endTime: '10:45:00' },
    { key: 'slot-4', label: 'الحصة الرابعة 11:15 - 12:00', startTime: '11:15:00', endTime: '12:00:00' },
    { key: 'slot-5', label: 'الحصة الخامسة 12:15 - 13:00', startTime: '12:15:00', endTime: '13:00:00' },
    { key: 'slot-6', label: 'الحصة السادسة 13:15 - 14:00', startTime: '13:15:00', endTime: '14:00:00' },
    { key: 'slot-7', label: 'الحصة السابعة 14:15 - 15:00', startTime: '14:15:00', endTime: '15:00:00' },
    { key: 'slot-8', label: 'الحصة الثامنة 15:15 - 16:00', startTime: '15:15:00', endTime: '16:00:00' }
  ];

  periodStart = this.toDateInput(new Date());
  periodEnd = this.toDateInput(this.addDays(new Date(), 42));
  createForm: CreateSessionFormState = this.createDefaultForm();

  constructor(
    private sessionService: SessionService,
    private teacherService: TeacherService,
    private classRoomService: ClassRoomService,
    private notificationService: NotificationService
  ) {}

  async ngOnInit(): Promise<void> {
    await Promise.all([
      this.loadOverview(),
      this.loadCreateMeta()
    ]);
  }

  get selectedTermLabel(): string {
    return this.getTermLabel(this.selectedTerm);
  }

  get lastGeneratedTermLabel(): string {
    return this.getTermLabel(this.lastGeneration?.term);
  }

  get selectedTimeSlot(): TimeSlotOption | null {
    return this.timeSlotOptions.find(slot => slot.key === this.createForm.slotKey) || null;
  }

  get timeSlotOptions(): TimeSlotOption[] {
    const now = new Date();
    const start = this.toTimeInput(now);
    const endDate = new Date(now);
    endDate.setMinutes(endDate.getMinutes() + 45);

    return [
      {
        key: 'current',
        label: `الآن - ${start.slice(0, 5)} إلى ${this.toTimeInput(endDate).slice(0, 5)}`,
        startTime: `${start}:00`,
        endTime: `${this.toTimeInput(endDate)}:00`
      },
      ...this.baseTimeSlots
    ];
  }

  get filteredClassRooms(): any[] {
    if (!this.teacherSubjects.length) {
      return [];
    }

    const classIds = Array.from(new Set(
      this.teacherSubjects
        .map(subject => Number(subject.classRoomId || 0))
        .filter(classRoomId => classRoomId > 0)
    ));

    return this.classRooms
      .filter(classRoom => classIds.includes(Number(classRoom.id)))
      .sort((first, second) => String(first.name || '').localeCompare(String(second.name || ''), 'ar'));
  }

  get filteredSubjects(): any[] {
    let subjects = [...this.teacherSubjects];

    if (this.createForm.classRoomId) {
      subjects = subjects.filter(subject => Number(subject.classRoomId) === Number(this.createForm.classRoomId));
    }

    return subjects.sort((first, second) => String(first.name || '').localeCompare(String(second.name || ''), 'ar'));
  }

  get createPreviewText(): string {
    const selectedTeacher = this.teachers.find(teacher => Number(teacher.id) === Number(this.createForm.teacherId));
    const selectedSubject = this.filteredSubjects.find(subject => Number(subject.id) === Number(this.createForm.subjectId));
    const selectedClassRoom = this.filteredClassRooms.find(classRoom => Number(classRoom.id) === Number(this.createForm.classRoomId));
    const selectedSlot = this.selectedTimeSlot;

    if (!selectedTeacher || !selectedSubject || !selectedClassRoom || !selectedSlot) {
      return 'اختر المدرس والمادة والفصل وموعد الحصة من القوائم الجاهزة ليظهر لك ملخص الإنشاء هنا.';
    }

    return `سيتم إنشاء حصة ${selectedSubject.name} للمدرس ${selectedTeacher.fullName} على فصل ${selectedClassRoom.name} يوم ${this.formatDate(this.createForm.sessionDate)} في الموعد ${selectedSlot.label}.`;
  }

  async loadOverview(): Promise<void> {
    this.loading = true;
    this.error = '';

    try {
      this.overview = await this.sessionService.getAdminScheduleOverview(
        this.periodStart,
        this.periodEnd,
        this.selectedTerm
      );
      this.sessions = this.overview.items || [];
    } catch (error: any) {
      console.error('Failed to load schedule overview', error);
      this.error = error?.message || 'تعذر تحميل جدول الحصص الحالي.';
      this.overview = null;
      this.sessions = [];
    } finally {
      this.loading = false;
    }
  }

  async loadCreateMeta(): Promise<void> {
    this.loadingCreateMeta = true;

    try {
      const [teachers, classRooms] = await Promise.all([
        this.teacherService.getTeachers(),
        this.classRoomService.getAll()
      ]);

      this.teachers = (teachers || [])
        .filter(teacher => teacher.isActive !== false)
        .sort((first, second) => String(first.fullName || '').localeCompare(String(second.fullName || ''), 'ar'));

      this.classRooms = classRooms || [];
    } catch (error) {
      console.error('Failed to load create-session metadata', error);
      this.createError = 'تعذر تحميل بيانات المدرسين والفصول الخاصة بإنشاء الحصص.';
    } finally {
      this.loadingCreateMeta = false;
    }
  }

  async generateSchedule(): Promise<void> {
    this.generating = true;
    this.error = '';

    try {
      this.lastGeneration = await this.sessionService.generateTermSchedule({
        startDate: this.periodStart,
        endDate: this.periodEnd,
        term: this.selectedTerm
      });

      const successMessage = this.lastGeneration.createdCount > 0
        ? `تم تجهيز ${this.lastGeneration.createdCount} حصة ضمن ${this.lastGeneratedTermLabel}.`
        : `${this.lastGeneratedTermLabel} مجهز بالفعل خلال الفترة المختارة.`;

      this.notificationService.success(successMessage);
      await this.loadOverview();
    } catch (error: any) {
      console.error('Failed to generate schedule', error);
      this.error = error?.message || 'تعذر تجهيز جدول الحصص.';
      this.notificationService.error(this.error);
    } finally {
      this.generating = false;
    }
  }

  async onTeacherChanged(): Promise<void> {
    this.createError = '';
    this.teacherSubjects = [];
    this.createForm.subjectId = null;
    this.createForm.classRoomId = null;

    if (!this.createForm.teacherId) {
      return;
    }

    try {
      const subjects = await this.teacherService.getTeacherSubjects(this.createForm.teacherId);
      this.teacherSubjects = Array.isArray(subjects)
        ? subjects.filter(subject => subject?.isActive !== false)
        : [];

      if (this.filteredClassRooms.length === 1) {
        this.createForm.classRoomId = Number(this.filteredClassRooms[0].id);
      }

      if (this.filteredSubjects.length === 1) {
        this.createForm.subjectId = Number(this.filteredSubjects[0].id);
      }
    } catch (error: any) {
      console.error('Failed to load teacher subjects', error);
      this.createError = error?.message || 'تعذر تحميل المواد المرتبطة بهذا المدرس.';
    }
  }

  onClassRoomChanged(): void {
    if (!this.createForm.classRoomId) {
      this.createForm.subjectId = null;
      return;
    }

    const selectedSubjectStillValid = this.filteredSubjects.some(subject => Number(subject.id) === Number(this.createForm.subjectId));
    if (!selectedSubjectStillValid) {
      this.createForm.subjectId = this.filteredSubjects.length === 1 ? Number(this.filteredSubjects[0].id) : null;
    }
  }

  onSubjectChanged(): void {
    const subject = this.teacherSubjects.find(item => Number(item.id) === Number(this.createForm.subjectId));
    if (!subject) {
      return;
    }

    if (subject.classRoomId) {
      this.createForm.classRoomId = Number(subject.classRoomId);
    }
  }

  async createSession(): Promise<void> {
    if (!this.createForm.teacherId || !this.createForm.subjectId || !this.createForm.classRoomId || !this.createForm.sessionDate) {
      this.createError = 'أكمل بيانات الحصة أولًا: المدرس والمادة والفصل والتاريخ والموعد.';
      return;
    }

    const selectedSlot = this.selectedTimeSlot;
    if (!selectedSlot) {
      this.createError = 'اختر توقيتًا صالحًا من القائمة.';
      return;
    }

    this.creatingSession = true;
    this.createError = '';

    try {
      const payload: CreateAdminSessionPayload = {
        title: this.createForm.title.trim() || undefined,
        sessionDate: this.createForm.sessionDate,
        startTime: selectedSlot.startTime,
        endTime: selectedSlot.endTime,
        teacherId: Number(this.createForm.teacherId),
        classRoomId: Number(this.createForm.classRoomId),
        subjectId: Number(this.createForm.subjectId),
        attendanceType: this.createForm.attendanceType
      };

      this.lastCreatedSession = await this.sessionService.createSession(payload);
      this.notificationService.success('تم إنشاء الحصة وإضافتها إلى جدول المدرس بنجاح.');

      this.ensureDateIncludedInOverview(this.createForm.sessionDate);
      await this.loadOverview();

      const previousTeacherId = this.createForm.teacherId;
      this.createForm = this.createDefaultForm();
      this.createForm.teacherId = previousTeacherId;
      if (previousTeacherId) {
        await this.onTeacherChanged();
      }
    } catch (error: any) {
      console.error('Failed to create admin session', error);
      this.createError = error?.message || 'تعذر إنشاء الحصة الآن.';
      this.notificationService.error(this.createError);
    } finally {
      this.creatingSession = false;
    }
  }

  formatDate(value: string): string {
    return new Date(value).toLocaleDateString('ar-EG', {
      weekday: 'long',
      day: 'numeric',
      month: 'long'
    });
  }

  formatTime(value: string): string {
    return new Date(value).toLocaleTimeString('ar-EG', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: true
    });
  }

  formatTerm(term?: string): string {
    if (!term) {
      return 'غير محدد';
    }

    return this.getTermLabel(term);
  }

  trackBySession(_: number, session: AdminScheduleSessionItem): number {
    return session.id;
  }

  private createDefaultForm(): CreateSessionFormState {
    return {
      teacherId: null,
      subjectId: null,
      classRoomId: null,
      sessionDate: this.toDateInput(new Date()),
      slotKey: 'current',
      attendanceType: 'QR',
      title: ''
    };
  }

  private ensureDateIncludedInOverview(sessionDate: string): void {
    if (sessionDate < this.periodStart) {
      this.periodStart = sessionDate;
    }

    if (sessionDate > this.periodEnd) {
      this.periodEnd = sessionDate;
    }
  }

  private toDateInput(value: Date): string {
    const year = value.getFullYear();
    const month = `${value.getMonth() + 1}`.padStart(2, '0');
    const day = `${value.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private toTimeInput(value: Date): string {
    const hours = `${value.getHours()}`.padStart(2, '0');
    const minutes = `${value.getMinutes()}`.padStart(2, '0');
    return `${hours}:${minutes}`;
  }

  private addDays(value: Date, days: number): Date {
    const next = new Date(value);
    next.setDate(next.getDate() + days);
    return next;
  }

  private getTermLabel(term?: string): string {
    if (!term || term === 'all' || term === 'الكل') {
      return 'كل الترمات';
    }

    return this.termOptions.find(option => option.value === term)?.label || term;
  }
}
