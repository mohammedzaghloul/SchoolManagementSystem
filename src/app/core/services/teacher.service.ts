// core/services/teacher.service.ts
import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { Teacher } from '../models/teacher.model';

@Injectable({
  providedIn: 'root'
})
export class TeacherService {
  constructor(private api: ApiService) { }

  async getTeachers(): Promise<Teacher[]> {
    return this.api.get<Teacher[]>('/api/Teachers');
  }

  async getTeacherById(id: number): Promise<Teacher> {
    return this.api.get<Teacher>(`/api/Teachers/${id}`);
  }

  async getTeacherStats(): Promise<any> {
    return this.api.get('/api/Teachers/stats');
  }

  async createTeacher(data: any): Promise<Teacher> {
    return this.api.post<Teacher>('/api/Teachers', data);
  }

  async updateTeacher(id: number, data: any): Promise<Teacher> {
    return this.api.put<Teacher>(`/api/Teachers/${id}`, data);
  }

  async deleteTeacher(id: number): Promise<void> {
    return this.api.delete(`/api/Teachers/${id}`);
  }

  async toggleStatus(id: number): Promise<any> {
    return this.api.patch(`/api/Teachers/${id}/toggle-status`, {});
  }

  async activateAll(): Promise<any> {
    return this.api.post('/api/Teachers/activate-all', {});
  }

  async getTeacherClasses(teacherId: number): Promise<any[]> {
    return this.api.get(`/api/ClassRooms/teacher/${teacherId}`);
  }

  async getTeacherSubjects(teacherId: number): Promise<any[]> {
    return this.api.get(`/api/Subjects/teacher/${teacherId}`);
  }
}