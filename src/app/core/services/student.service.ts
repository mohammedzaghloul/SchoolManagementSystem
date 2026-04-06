// core/services/student.service.ts
import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { Student, StudentFilter, PaginatedResponse } from '../models/student.model';

@Injectable({
  providedIn: 'root'
})
export class StudentService {
  constructor(private api: ApiService) { }

  async getStudents(filter?: StudentFilter): Promise<PaginatedResponse<Student>> {
    const params: any = {};

    if (filter?.classRoomId) params.classRoomId = filter.classRoomId;

    try {
      const response: any = await this.api.get<any>('/api/Students', params);

      // The backend currently returns a flat array List<StudentDto>
      const allItems = Array.isArray(response) ? response : (response.items || response.data || []);

      // Filter locally if necessary (e.g. search string)
      let filteredItems = allItems;
      if (filter?.search) {
        const term = filter.search.toLowerCase();
        filteredItems = filteredItems.filter((s: any) =>
          (s.fullName && s.fullName.toLowerCase().includes(term)) ||
          (s.email && s.email.toLowerCase().includes(term))
        );
      }

      // Paginate locally
      const pageIndex = filter?.pageIndex || 1;
      const pageSize = filter?.pageSize || 10;
      const startIndex = (pageIndex - 1) * pageSize;
      const paginatedItems = filteredItems.slice(startIndex, startIndex + pageSize);

      return {
        items: paginatedItems,
        totalCount: filteredItems.length,
        pageIndex: pageIndex,
        pageSize: pageSize,
        totalPages: Math.ceil(filteredItems.length / pageSize),
        hasPreviousPage: pageIndex > 1,
        hasNextPage: startIndex + pageSize < filteredItems.length
      };

    } catch (error) {
      console.error('Error fetching students:', error);
      throw error;
    }
  }

  async getStudentById(id: number): Promise<Student> {
    return this.api.get<Student>(`/api/Students/${id}`);
  }

  async createStudent(data: any): Promise<Student> {
    return this.api.post<Student>('/api/Students', data);
  }

  async updateStudent(id: number, data: any): Promise<Student> {
    return this.api.put<Student>(`/api/Students/${id}`, data);
  }

  async deleteStudent(id: number): Promise<void> {
    return this.api.delete(`/api/Students/${id}`);
  }

  async toggleStatus(id: number): Promise<any> {
    return this.api.patch(`/api/Students/${id}/toggle-status`, {});
  }

  async getStudentAttendance(studentId: number, date?: Date): Promise<any> {
    const params = date ? { date: date.toISOString() } : undefined;
    return this.api.get(`/api/Attendance/student/${studentId}`, params);
  }

  async getStudentStats(studentId: number): Promise<any> {
    return this.api.get(`/api/Attendance/student/${studentId}/stats`);
  }

  async trainFace(studentId: number, image: File): Promise<any> {
    return this.api.upload('/api/Attendance/enroll-face', image, { studentId });
  }
}
