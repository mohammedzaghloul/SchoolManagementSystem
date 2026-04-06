import { Injectable } from '@angular/core';
import { ApiService } from './api.service';

export interface AdminPaymentInvoice {
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
  parentId?: number | null;
  parentName?: string;
  classRoomName?: string;
}

export interface AdminPaymentUpsertRequest {
  studentId: number;
  title: string;
  description?: string;
  academicYear: string;
  term: string;
  amount: number;
  dueDate: string;
}

@Injectable({
  providedIn: 'root'
})
export class AdminPaymentService {
  constructor(private api: ApiService) {}

  async getInvoices(): Promise<AdminPaymentInvoice[]> {
    return this.api.get<AdminPaymentInvoice[]>('/api/TuitionInvoices');
  }

  async createInvoice(data: AdminPaymentUpsertRequest): Promise<AdminPaymentInvoice> {
    return this.api.post<AdminPaymentInvoice>('/api/TuitionInvoices', data);
  }

  async updateInvoice(id: number, data: AdminPaymentUpsertRequest): Promise<AdminPaymentInvoice> {
    return this.api.put<AdminPaymentInvoice>(`/api/TuitionInvoices/${id}`, data);
  }

  async deleteInvoice(id: number): Promise<void> {
    return this.api.delete(`/api/TuitionInvoices/${id}`);
  }
}
