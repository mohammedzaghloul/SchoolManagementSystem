import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { AdminPaymentInvoice, AdminPaymentService, AdminPaymentUpsertRequest } from '../../../../core/services/admin-payment.service';
import { StudentService } from '../../../../core/services/student.service';

type InvoiceStatusFilter = 'all' | 'Pending' | 'Partial' | 'Overdue' | 'Paid';

@Component({
  selector: 'app-payment-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MatDialogModule],
  templateUrl: './payment-management.component.html',
  styleUrls: ['./payment-management.component.css']
})
export class PaymentManagementComponent implements OnInit, OnDestroy {
  invoices: AdminPaymentInvoice[] = [];
  filteredInvoices: AdminPaymentInvoice[] = [];
  students: any[] = [];

  loading = false;
  submitting = false;
  error = '';
  searchTerm = '';
  selectedStatus: InvoiceStatusFilter = 'all';
  selectedStudent = 'all';

  readonly pageSize = 10;
  currentPage = 1;

  showModal = false;
  isEditMode = false;
  currentInvoice: Partial<AdminPaymentInvoice> = this.createEmptyInvoice();

  constructor(
    private adminPaymentService: AdminPaymentService,
    private studentService: StudentService,
    private dialog: MatDialog
  ) {}

  async ngOnInit(): Promise<void> {
    await Promise.all([
      this.loadInvoices(),
      this.loadStudents()
    ]);
  }

  ngOnDestroy(): void {
    document.body.classList.remove('modal-open-fix');
  }

  async loadInvoices(): Promise<void> {
    this.loading = true;
    this.error = '';

    try {
      this.invoices = await this.adminPaymentService.getInvoices();
      this.applyFilter();
    } catch (err: any) {
      this.error = err?.message || 'حدث خطأ في تحميل الفواتير.';
    } finally {
      this.loading = false;
    }
  }

  async loadStudents(): Promise<void> {
    try {
      const response = await this.studentService.getStudents({ pageIndex: 1, pageSize: 1000 });
      this.students = Array.isArray(response) ? response : response.items || [];
    } catch (err) {
      console.error('Students load error', err);
    }
  }

  applyFilter(): void {
    const term = this.searchTerm.trim().toLowerCase();

    this.filteredInvoices = this.invoices.filter(invoice => {
      const matchesSearch = !term ||
        invoice.title.toLowerCase().includes(term) ||
        (invoice.studentName || '').toLowerCase().includes(term) ||
        (invoice.parentName || '').toLowerCase().includes(term);

      const matchesStatus = this.selectedStatus === 'all' || invoice.status === this.selectedStatus;
      const matchesStudent = this.selectedStudent === 'all' || `${invoice.studentId}` === `${this.selectedStudent}`;

      return matchesSearch && matchesStatus && matchesStudent;
    });
    this.currentPage = 1;
  }

  get totalPages(): number {
    return Math.ceil(this.filteredInvoices.length / this.pageSize);
  }

  get pagedInvoices(): AdminPaymentInvoice[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredInvoices.slice(start, start + this.pageSize);
  }

  get pageNumbers(): (number | string)[] {
    const total = this.totalPages;
    const current = this.currentPage;
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
    const pages: (number | string)[] = [1];
    if (current > 3) pages.push('...');
    for (let i = Math.max(2, current - 1); i <= Math.min(total - 1, current + 1); i++) pages.push(i);
    if (current < total - 2) pages.push('...');
    pages.push(total);
    return pages;
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
  }

  openAddModal(): void {
    this.isEditMode = false;
    this.currentInvoice = this.createEmptyInvoice();
    this.showModal = true;
    document.body.classList.add('modal-open-fix');
  }

  openEditModal(invoice: AdminPaymentInvoice): void {
    this.isEditMode = true;
    this.currentInvoice = {
      ...invoice,
      dueDate: this.toDateInput(invoice.dueDate)
    };
    this.showModal = true;
    document.body.classList.add('modal-open-fix');
  }

  closeModal(): void {
    this.showModal = false;
    document.body.classList.remove('modal-open-fix');
  }

  async saveInvoice(): Promise<void> {
    if (!this.currentInvoice.studentId || !this.currentInvoice.title || !this.currentInvoice.academicYear || !this.currentInvoice.term || !this.currentInvoice.amount || !this.currentInvoice.dueDate) {
      alert('يرجى استكمال بيانات الفاتورة أولاً.');
      return;
    }

    this.submitting = true;

    try {
      const payload: AdminPaymentUpsertRequest = {
        studentId: Number(this.currentInvoice.studentId),
        title: `${this.currentInvoice.title}`.trim(),
        description: this.currentInvoice.description || '',
        academicYear: `${this.currentInvoice.academicYear}`.trim(),
        term: `${this.currentInvoice.term}`.trim(),
        amount: Number(this.currentInvoice.amount),
        dueDate: `${this.currentInvoice.dueDate}`
      };

      if (this.isEditMode && this.currentInvoice.id) {
        await this.adminPaymentService.updateInvoice(this.currentInvoice.id, payload);
      } else {
        await this.adminPaymentService.createInvoice(payload);
      }

      await this.loadInvoices();
      this.closeModal();
    } catch (err: any) {
      alert('تعذر حفظ الفاتورة: ' + (err?.error?.message || err?.message || 'يرجى المحاولة مرة أخرى.'));
    } finally {
      this.submitting = false;
    }
  }

  deleteInvoice(invoice: AdminPaymentInvoice): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'تأكيد الحذف',
        message: `هل أنت متأكد من حذف فاتورة ${invoice.title} الخاصة بالطالب ${invoice.studentName}؟`,
        confirmText: 'حذف',
        cancelText: 'إلغاء',
        color: 'warn'
      } as ConfirmDialogData
    });

    dialogRef.afterClosed().subscribe(async (result) => {
      if (!result) {
        return;
      }

      try {
        await this.adminPaymentService.deleteInvoice(invoice.id);
        this.invoices = this.invoices.filter(item => item.id !== invoice.id);
        this.applyFilter();
      } catch (err: any) {
        alert('تعذر حذف الفاتورة: ' + (err?.error?.message || err?.message || 'قد تكون الفاتورة مسدد منها مبلغ بالفعل.'));
      }
    });
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('ar-EG', {
      style: 'currency',
      currency: 'EGP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 2
    }).format(value || 0);
  }

  getStatusLabel(status: AdminPaymentInvoice['status']): string {
    switch (status) {
      case 'Paid':
        return 'مدفوعة';
      case 'Partial':
        return 'مدفوعة جزئياً';
      case 'Overdue':
        return 'متأخرة';
      default:
        return 'معلقة';
    }
  }

  getStatusClass(status: AdminPaymentInvoice['status']): string {
    switch (status) {
      case 'Paid':
        return 'bg-success-subtle text-success';
      case 'Partial':
        return 'bg-warning-subtle text-warning';
      case 'Overdue':
        return 'bg-danger-subtle text-danger';
      default:
        return 'bg-info-subtle text-info';
    }
  }

  get monthlyRevenue(): number {
    const now = new Date();

    return this.invoices
      .filter(invoice => !!invoice.paidAt && this.isSameMonth(invoice.paidAt, now))
      .reduce((sum, invoice) => sum + (invoice.amountPaid || 0), 0);
  }

  get overdueAmount(): number {
    const today = this.startOfDay(new Date());

    return this.invoices
      .filter(invoice => invoice.remainingAmount > 0 && this.startOfDay(invoice.dueDate) < today)
      .reduce((sum, invoice) => sum + invoice.remainingAmount, 0);
  }

  get pendingCount(): number {
    return this.invoices.filter(invoice => invoice.remainingAmount > 0).length;
  }

  private createEmptyInvoice(): Partial<AdminPaymentInvoice> {
    return {
      studentId: undefined,
      title: '',
      description: '',
      academicYear: this.getDefaultAcademicYear(),
      term: 'الترم الأول',
      amount: 0,
      dueDate: this.toDateInput(this.getDefaultDueDate())
    };
  }

  private getDefaultAcademicYear(): string {
    const now = new Date();
    const startYear = now.getMonth() >= 7 ? now.getFullYear() : now.getFullYear() - 1;
    return `${startYear}/${startYear + 1}`;
  }

  private getDefaultDueDate(): Date {
    const dueDate = new Date();
    dueDate.setDate(dueDate.getDate() + 14);
    return dueDate;
  }

  private toDateInput(value: string | Date): string {
    const date = new Date(value);
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${date.getFullYear()}-${month}-${day}`;
  }

  private isSameMonth(value: string | Date, reference: Date): boolean {
    const date = new Date(value);
    return date.getFullYear() === reference.getFullYear() && date.getMonth() === reference.getMonth();
  }

  private startOfDay(value: string | Date): number {
    const date = new Date(value);
    date.setHours(0, 0, 0, 0);
    return date.getTime();
  }
}
