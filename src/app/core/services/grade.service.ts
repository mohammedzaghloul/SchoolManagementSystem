// core/services/grade.service.ts
import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { Grade, GradeLevel } from '../models/grade.model';

export interface TeacherGradebookStudent {
  id: number;
  fullName: string;
  email?: string;
  existingGradeId?: number | null;
  score?: number | null;
  notes?: string | null;
  lastUpdatedAt?: string | null;
}

export interface TeacherGradebookResponse {
  subjectId: number;
  subjectName: string;
  classRoomId?: number;
  classRoomName: string;
  gradeType: string;
  date: string;
  isConfirmed?: boolean;
  confirmedAt?: string | null;
  missingGradesCount?: number;
  status?: 'IN_PROGRESS' | 'COMPLETED';
  statusLabel?: string;
  students: TeacherGradebookStudent[];
}

export interface TeacherGradebookSaveRequest {
  subjectId: number;
  gradeType: string;
  date: string;
  grades: Array<{
    id?: number | null;
    studentId: number;
    score: number;
    notes?: string | null;
  }>;
}

export interface TeacherGradebookConfirmRequest {
  subjectId: number;
  gradeType: string;
  date: string;
  isConfirmed: boolean;
}

export interface TeacherGradebookConfirmResponse {
  success: boolean;
  isConfirmed: boolean;
  confirmedAt?: string | null;
  totalStudents: number;
  gradedStudents: number;
  missingGradesCount: number;
  status: 'IN_PROGRESS' | 'COMPLETED';
  statusLabel: string;
}

export interface AdminGradeUploadSubjectStatus {
  subjectId: number;
  subjectName: string;
  classRoomId: number;
  classRoomName: string;
  totalStudents: number;
  gradedStudents: number;
  missingGradesCount: number;
  isConfirmed: boolean;
  confirmedAt?: string | null;
  updatedAt?: string | null;
  status: 'IN_PROGRESS' | 'COMPLETED';
  statusLabel: string;
}

export interface AdminGradeUploadTeacherStatus {
  teacherId: number;
  teacherName: string;
  teacherEmail?: string | null;
  totalSubjects: number;
  confirmedSubjects: number;
  pendingSubjects: number;
  isComplete: boolean;
  status: 'IN_PROGRESS' | 'COMPLETED';
  statusLabel: string;
  subjects: AdminGradeUploadSubjectStatus[];
}

export interface AdminGradeUploadStatusResponse {
  gradeType: string;
  date: string;
  status: 'IN_PROGRESS' | 'COMPLETED';
  statusLabel: string;
  totalTeachers: number;
  completeTeachers: number;
  pendingTeachers: number;
  totalSubjects: number;
  confirmedSubjects: number;
  pendingSubjects: number;
  completionPercent: number;
  teachers: AdminGradeUploadTeacherStatus[];
}

export interface PublishGradeSessionsRequest {
  scope: 'All' | 'Class' | 'GradeLevel';
  classId?: number | null;
  gradeLevelId?: number | null;
  subjectId: number;
  type: string;
  date: string;
  deadline?: string | null;
}

export interface PublishGradeSessionsResult {
  createdCount: number;
  duplicateCount: number;
  skippedCount: number;
  skippedClasses: string[];
  createdSessions: PublishedGradeSession[];
}

export interface PublishedGradeSession {
  sessionId: number;
  classId: number;
  className: string;
  subjectId: number;
  subjectName: string;
  teacherId: number;
  teacherName: string;
  type: string;
  date: string;
  deadline?: string | null;
}

export interface AdminGradeSessionsDashboard {
  globalStatus: 'Completed' | 'In Progress' | string;
  totalSessions: number;
  approvedSessions: number;
  inProgressSessions: number;
  sessions: GradeSessionMonitor[];
}

export interface GradeSessionMonitor {
  sessionId: number;
  className: string;
  gradeLevelName?: string | null;
  subjectName: string;
  type: string;
  date: string;
  deadline?: string | null;
  teacherId: number;
  teacherName: string;
  totalStudents: number;
  gradedStudents: number;
  missingGradesCount: number;
  progressPercent: number;
  status: 'NotStarted' | 'InProgress' | 'Approved' | string;
}

export interface TeacherGradeSessionOption {
  sessionId: number;
  classId: number;
  className: string;
  gradeLevelName?: string | null;
  subjectId: number;
  subjectName: string;
  type: string;
  date: string;
  deadline?: string | null;
  status: 'NotStarted' | 'InProgress' | 'Approved' | string;
  progressPercent: number;
}

export interface TeacherSessionGradebook {
  sessionId: number;
  className: string;
  subjectName: string;
  type: string;
  date: string;
  deadline?: string | null;
  isApproved: boolean;
  isDeadlinePassed: boolean;
  isLocked: boolean;
  status: 'NotStarted' | 'InProgress' | 'Approved' | string;
  totalStudents: number;
  gradedStudents: number;
  missingGradesCount: number;
  progressPercent: number;
  students: TeacherSessionGradeStudent[];
}

export interface TeacherSessionGradeStudent {
  studentId: number;
  studentName: string;
  gradeId?: number | null;
  score?: number | null;
  maxScore: number;
  percentage?: number | null;
  isGraded: boolean;
}

export interface SaveTeacherSessionGradesRequest {
  sessionId: number;
  grades: Array<{
    studentId: number;
    score?: number | null;
    maxScore: number;
  }>;
}

export interface GradeOperationResult {
  success: boolean;
  message: string;
  totalStudents: number;
  gradedStudents: number;
  missingGradesCount: number;
  status: string;
}

@Injectable({
  providedIn: 'root'
})
export class GradeService {
  constructor(private api: ApiService) { }

  // Grade Levels (Academic Levels)
  async getGrades(): Promise<GradeLevel[]> {
    return this.api.get<GradeLevel[]>('/api/GradeLevels');
  }

  async createGrade(data: any): Promise<GradeLevel> {
    return this.api.post<GradeLevel>('/api/GradeLevels', data);
  }

  async updateGrade(id: number, data: any): Promise<GradeLevel> {
    return this.api.put<GradeLevel>(`/api/GradeLevels/${id}`, data);
  }

  async deleteGrade(id: number): Promise<void> {
    return this.api.delete(`/api/GradeLevels/${id}`);
  }

  // Student Marks/Grades
  async getMyGrades(): Promise<Grade[]> {
    return this.api.get<Grade[]>('/api/Grade/student');
  }

  async getStudentGrades(studentId: number): Promise<Grade[]> {
    return this.api.get<Grade[]>(`/api/Grade/student/${studentId}`);
  }

  async addGrade(data: any): Promise<Grade> {
    return this.api.post<Grade>('/api/Grade', data);
  }

  async getTeacherGradebook(subjectId: number, gradeType: string, date: string): Promise<TeacherGradebookResponse> {
    return this.api.get<TeacherGradebookResponse>('/api/Grade/teacher/gradebook', {
      subjectId,
      gradeType,
      date
    });
  }

  async saveTeacherGradebook(data: TeacherGradebookSaveRequest): Promise<{ success: boolean; savedCount: number; message: string }> {
    return this.api.post<{ success: boolean; savedCount: number; message: string }>('/api/Grade/teacher/gradebook', data);
  }

  async confirmTeacherGradebook(data: TeacherGradebookConfirmRequest): Promise<TeacherGradebookConfirmResponse> {
    return this.api.post<TeacherGradebookConfirmResponse>('/api/Grade/teacher/gradebook/confirm', data);
  }

  async getAdminGradeUploadStatus(gradeType: string, date: string): Promise<AdminGradeUploadStatusResponse> {
    return this.api.get<AdminGradeUploadStatusResponse>('/api/Grade/upload-status', {
      gradeType,
      date
    });
  }

  async publishGradeSessions(data: PublishGradeSessionsRequest): Promise<PublishGradeSessionsResult> {
    return this.api.post<PublishGradeSessionsResult>('/api/GradeManagement/sessions/publish', data);
  }

  async getAdminGradeSessionsDashboard(type?: string, date?: string): Promise<AdminGradeSessionsDashboard> {
    const params: any = {};
    if (type) params.type = type;
    if (date) params.date = date;
    return this.api.get<AdminGradeSessionsDashboard>('/api/GradeManagement/admin/dashboard', params);
  }

  async getTeacherGradeSessions(teacherId?: number): Promise<TeacherGradeSessionOption[]> {
    const params = teacherId && teacherId > 0 ? { teacherId } : undefined;
    return this.api.get<TeacherGradeSessionOption[]>('/api/GradeManagement/teacher/sessions', params);
  }

  async getTeacherSessionGradebook(sessionId: number): Promise<TeacherSessionGradebook> {
    return this.api.get<TeacherSessionGradebook>(`/api/GradeManagement/teacher/sessions/${sessionId}/gradebook`);
  }

  async saveTeacherSessionGrades(data: SaveTeacherSessionGradesRequest): Promise<GradeOperationResult> {
    return this.api.post<GradeOperationResult>('/api/GradeManagement/teacher/grades', data);
  }

  async approveTeacherSession(sessionId: number): Promise<GradeOperationResult> {
    return this.api.post<GradeOperationResult>(`/api/GradeManagement/teacher/sessions/${sessionId}/approve`, {});
  }
}
