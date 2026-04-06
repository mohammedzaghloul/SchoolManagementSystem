// core/services/classroom.service.ts
import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { ClassRoom } from '../models/class.model';

@Injectable({
  providedIn: 'root'
})
export class ClassRoomService {
  constructor(private api: ApiService) { }

  async getAll(): Promise<ClassRoom[]> {
    return this.api.get<ClassRoom[]>('/api/ClassRooms');
  }

  async getClassRooms(): Promise<ClassRoom[]> {
    return this.getAll();
  }

  async getById(id: number): Promise<ClassRoom> {
    return this.api.get<ClassRoom>(`/api/ClassRooms/${id}`);
  }

  async create(data: any): Promise<ClassRoom> {
    return this.api.post<ClassRoom>('/api/ClassRooms', data);
  }

  async update(id: number, data: any): Promise<ClassRoom> {
    return this.api.put<ClassRoom>(`/api/ClassRooms/${id}`, data);
  }

  async delete(id: number): Promise<void> {
    return this.api.delete(`/api/ClassRooms/${id}`);
  }

  async getStudents(classId: number): Promise<any[]> {
    return this.api.get(`/api/ClassRoom/${classId}/students`);
  }

  async getTeacherClasses(teacherId: number): Promise<ClassRoom[]> {
    return this.api.get<ClassRoom[]>(`/api/ClassRoom/teacher/${teacherId}`);
  }
}