import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { CreateParentRequest, Parent } from '../models/parent.model';

export interface ParentDashboardChild {
  id: number;
  fullName: string;
  avatar?: string | null;
  classRoomName: string;
  gradeLevel: string;
  attendanceRate: number;
  absences: number;
  average: number;
  pendingBalance: number;
  latestAttendanceStatus: string;
  latestAttendanceAt?: string | Date | null;
  recentGrades: Array<{
    subject: string;
    score: number;
  }>;
}

export interface ParentDashboardSummary {
  totalChildren: number;
  averageAttendanceRate: number;
  averageScore: number;
  totalAbsences: number;
  pendingPaymentsAmount: number;
  pendingInvoicesCount: number;
}

export interface ParentDashboardActivity {
  type: 'attendance' | 'grade';
  title: string;
  description: string;
  createdAt: string | Date;
}

export interface ParentDashboardData {
  parentName: string;
  parentEmail: string;
  parentPhone?: string;
  parentAddress?: string;
  totalChildren: number;
  summary: ParentDashboardSummary;
  recentActivity: ParentDashboardActivity[];
  children: ParentDashboardChild[];
}

export interface ParentEvent {
  id: string;
  title: string;
  description: string;
  date: string | Date;
  type: 'exam' | 'payment';
  studentName: string;
}

export interface ParentPaymentInvoice {
  id: number;
  title: string;
  description?: string;
  academicYear: string;
  term: string;
  amount: number;
  amountPaid: number;
  remainingAmount: number;
  dueDate: string | Date;
  createdAt: string | Date;
  paidAt?: string | Date | null;
  status: 'Paid' | 'Pending' | 'Partial' | 'Overdue';
  paymentMethod?: string;
  referenceNumber?: string;
  studentId: number;
  studentName: string;
}

export interface ParentPaymentRequest {
  amount?: number;
  method?: string;
  note?: string;
}

export interface ParentPaymentResult {
  invoiceId: number;
  paidAmount: number;
  totalPaid: number;
  remainingAmount: number;
  status: string;
  referenceNumber?: string;
  paymentMethod?: string;
  paidAt?: string | Date | null;
}

@Injectable({
  providedIn: 'root'
})
export class ParentService {
  constructor(private api: ApiService) { }

  async getAll(): Promise<Parent[]> {
    return this.getParents();
  }

  async getParents(): Promise<Parent[]> {
    return this.api.get<Parent[]>('/api/Parent');
  }

  async getParentById(id: number): Promise<Parent> {
    return this.api.get<Parent>(`/api/Parent/${id}`);
  }

  async createParent(data: CreateParentRequest): Promise<Parent> {
    return this.api.post<Parent>('/api/Account/add-parent', data);
  }

  async updateParent(id: number, data: any): Promise<Parent> {
    return this.api.put<Parent>(`/api/Parent/${id}`, data);
  }

  async deleteParent(id: number): Promise<void> {
    return this.api.delete(`/api/Parent/${id}`);
  }

  async getChildren(parentId: number): Promise<any[]> {
    return this.api.get<any[]>(`/api/Parent/${parentId}/children`);
  }

  async getUpcomingEvents(): Promise<ParentEvent[]> {
    return this.api.get<ParentEvent[]>('/api/Parent/events');
  }

  async getPendingPayments(): Promise<number> {
    return this.api.get<number>('/api/Parent/pending-payments');
  }

  async getPayments(): Promise<ParentPaymentInvoice[]> {
    return this.api.get<ParentPaymentInvoice[]>('/api/Parent/payments');
  }

  async payInvoice(paymentId: number, data: ParentPaymentRequest): Promise<ParentPaymentResult> {
    return this.api.post<ParentPaymentResult>(`/api/Parent/payments/${paymentId}/pay`, data);
  }

  async getDashboardData(): Promise<ParentDashboardData> {
    return this.api.get<ParentDashboardData>('/api/Dashboards/parent');
  }
}
