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
  attendanceSummary?: {
    total: number;
    present: number;
    late: number;
    absent: number;
  };
  recentAttendance?: Array<{
    id: number;
    subjectName: string;
    sessionDate?: string | Date | null;
    status: string;
    isPresent: boolean;
    method?: string | null;
    recordedAt?: string | Date | null;
  }>;
  assignments?: Array<{
    id: number;
    title: string;
    subjectName: string;
    dueDate: string | Date;
    status: 'Open' | 'Submitted' | 'Graded' | 'Late';
    submittedAt?: string | Date | null;
    grade?: number | null;
    feedback?: string | null;
  }>;
  payments?: Array<{
    id: number;
    title: string;
    term: string;
    amount: number;
    amountPaid: number;
    remainingAmount: number;
    dueDate: string | Date;
    status: 'Paid' | 'Pending' | 'Partial' | 'Overdue';
    paymentMethod?: string | null;
    paidAt?: string | Date | null;
  }>;
  gradeBreakdown?: Array<{
    subject: string;
    average: number;
    count: number;
    latestScore: number;
    latestType: string;
    latestDate: string | Date;
  }>;
  recentGrades: Array<{
    subject: string;
    score: number;
    type?: string;
    date?: string | Date;
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

export interface ParentPaymentMethod {
  code: string;
  label: string;
  hint: string;
  icon: string;
  providerCode: string;
  receiver?: string | null;
  requiresOtp: boolean;
  requiresReferenceConfirmation: boolean;
}

export interface ParentPaymentRequest {
  amount?: number;
  method?: string;
  methodCode?: string;
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

export interface ParentGradeHistoryItem {
  id: number;
  studentId: number;
  studentName: string;
  subjectId: number;
  subjectName: string;
  gradeType: string;
  score: number;
  percentage: number;
  notes?: string | null;
  date: string | Date;
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

  async getPaymentMethods(): Promise<ParentPaymentMethod[]> {
    return this.api.get<ParentPaymentMethod[]>('/api/Parent/payments/methods');
  }

  async payInvoice(paymentId: number, data: ParentPaymentRequest): Promise<ParentPaymentResult> {
    return this.api.post<ParentPaymentResult>(`/api/Parent/payments/${paymentId}/pay`, data);
  }

  async getGradeHistory(childId?: number, take = 80): Promise<ParentGradeHistoryItem[]> {
    const params = new URLSearchParams({ take: String(take) });
    if (childId) {
      params.set('childId', String(childId));
    }

    return this.api.get<ParentGradeHistoryItem[]>(`/api/Parent/grades/history?${params.toString()}`);
  }

  async getDashboardData(): Promise<ParentDashboardData> {
    return this.api.get<ParentDashboardData>('/api/Dashboards/parent');
  }
}
