// core/services/exam.service.ts
import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { Exam } from '../models/exam.model';

@Injectable({
  providedIn: 'root'
})
export class ExamService {
  constructor(private api: ApiService) { }

  async getExams(): Promise<Exam[]> {
    return this.api.get<Exam[]>('/api/Exams');
  }

  async getExamDetails(id: number): Promise<Exam> {
    return this.api.get<Exam>(`/api/Exams/${id}`);
  }

  async getTeacherExams(): Promise<Exam[]> {
    return this.api.get<Exam[]>('/api/Exams/teacher');
  }

  async getStudentExams(): Promise<Exam[]> {
    return this.api.get<Exam[]>('/api/Exams/student');
  }

  async getClassExams(classId: number): Promise<Exam[]> {
    return this.api.get<Exam[]>(`/api/Exams/classroom/${classId}`);
  }

  async createExam(data: any): Promise<Exam> {
    return this.api.post<Exam>('/api/Exams', data);
  }

  async updateExam(id: number, data: any): Promise<Exam> {
    return this.api.put<Exam>(`/api/Exams/${id}`, data);
  }

  async deleteExam(id: number): Promise<void> {
    return this.api.delete(`/api/Exams/${id}`);
  }

  async getExamForStudent(id: number): Promise<Exam> {
    return this.api.get<Exam>(`/api/Exams/${id}/take`);
  }

  async submitExam(id: number, answers: any[]): Promise<any> {
    return this.api.post(`/api/Exams/${id}/submit`, { answers });
  }

  async getExamResults(examId: number): Promise<any> {
    return this.api.get(`/api/Exams/results-by-exam/${examId}`);
  }

  async getStudentResults(studentId: number): Promise<any> {
    return this.api.get(`/api/Exams/results-by-student/${studentId}`);
  }
}