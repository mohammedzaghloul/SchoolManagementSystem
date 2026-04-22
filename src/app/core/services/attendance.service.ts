// core/services/attendance.service.ts
import { Injectable } from '@angular/core';
import { ApiService } from './api.service';

@Injectable({
  providedIn: 'root'
})
export class AttendanceService {
  constructor(private api: ApiService) { }

  async generateQr(sessionId: number): Promise<any> {
    return this.api.get(`/api/Attendance/generate-qr/${sessionId}`);
  }

  async markManual(data: any): Promise<any> {
    return this.api.post('/api/Attendance/mark-manual', data);
  }

  async markQR(data: any): Promise<any> {
    return this.api.post('/api/Attendance/scan-qr', data);
  }

  async markFace(sessionId: number, image: File): Promise<any> {
    const formData = new FormData();
    formData.append('file', image);
    return this.api.postMultipart(`/api/Attendance/face/${sessionId}`, formData);
  }

  async getSessionAttendance(sessionId: number): Promise<any> {
    return this.api.get(`/api/Attendance/session/${sessionId}`);
  }

  async getSessionRoster(sessionId: number): Promise<any> {
    return this.api.get(`/api/Attendance/session/${sessionId}/roster`);
  }

  async getStudentAttendance(studentId: number): Promise<any> {
    return this.api.get(`/api/Attendance/student/${studentId}`);
  }

  async getMyAttendance(): Promise<any> {
    return this.api.get('/api/Attendance/me');
  }

  async getStudentStats(studentId: number): Promise<any> {
    return this.api.get(`/api/Attendance/student/${studentId}/stats`);
  }

  async getMyStats(): Promise<any> {
    return this.api.get('/api/Attendance/me/stats');
  }

  recordFaceAttendance(imageBase64: string): any {
    // This calls the backend to verify the student identity
    return this.api.post('/api/Attendance/verify-face', { image: imageBase64 });
  }

  async enrollFace(studentId: number, image: File): Promise<any> {
    const formData = new FormData();
    formData.append('studentId', studentId.toString());
    formData.append('file', image);
    return this.api.postMultipart('/api/Attendance/enroll-face', formData);
  }
}
