// core/services/grade.service.ts
import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { Grade, GradeLevel } from '../models/grade.model';

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
}