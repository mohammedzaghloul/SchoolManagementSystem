// core/services/subject.service.ts
import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { Subject } from '../models/subject.model';

@Injectable({
  providedIn: 'root'
})
export class SubjectService {
  constructor(private api: ApiService) { }

  async getAll(): Promise<Subject[]> {
    return this.api.get<Subject[]>('/api/Subjects');
  }

  async getSubjects(): Promise<Subject[]> {
    return this.getAll();
  }

  async getById(id: number): Promise<Subject> {
    return this.api.get<Subject>(`/api/Subjects/${id}`);
  }

  async create(data: any): Promise<Subject> {
    return this.api.post<Subject>('/api/Subjects', data);
  }

  async update(id: number, data: any): Promise<Subject> {
    return this.api.put<Subject>(`/api/Subjects/${id}`, data);
  }

  async delete(id: number): Promise<void> {
    return this.api.delete(`/api/Subjects/${id}`);
  }

  async getTeacherSubjects(teacherId?: number): Promise<Subject[]> {
    const suffix = teacherId && teacherId > 0 ? `/${teacherId}` : '';
    return this.api.get<Subject[]>(`/api/Subjects/teacher${suffix}`);
  }
}
