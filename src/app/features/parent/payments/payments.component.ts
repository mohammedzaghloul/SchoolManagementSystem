import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import {
  ParentPaymentInvoice,
  ParentPaymentResult,
  ParentService
} from '../../../core/services/parent.service';
import { NotificationService } from '../../../core/services/notification.service';
import { NotificationCenterService } from '../../../core/services/notification-center.service';

type PaymentMethod = 'بطاقة' | 'محفظة إلكترونية' | 'تحويل بنكي';

@Component({
  selector: 'app-parent-payments',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './payments.component.html',
  styleUrls: ['./payments.component.css']
})
export class PaymentsComponent implements OnInit {
  loading = true;
  payingInvoiceId: number | null = null;
  selectedMethod: PaymentMethod = 'بطاقة';
  invoices: ParentPaymentInvoice[] = [];
  
  // Payment Gateway simulation
  showGateway = false;
  activeInvoice: ParentPaymentInvoice | null = null;
  gatewayLoading = false;
  cardDetails = { number: '', expiry: '', cvc: '', name: '' };
  paymentStep: 'input' | 'processing' | 'success' = 'input';
  lastReference: string | null = null;
  
  // Receipt view
  receiptToShow: ParentPaymentInvoice | null = null;
  selectedTerm: string = 'الكل';

  readonly paymentMethods: PaymentMethod[] = ['بطاقة', 'محفظة إلكترونية', 'تحويل بنكي'];

  constructor(
    private parentService: ParentService,
    private notify: NotificationService,
    private notificationCenter: NotificationCenterService
  ) { }

  async ngOnInit(): Promise<void> {
    await this.loadInvoices();
  }

  async loadInvoices(): Promise<void> {
    this.loading = true;

    try {
      this.invoices = await this.parentService.getPayments();
    } catch (error) {
      console.error('Failed to load parent payments:', error);
      this.notify.error('تعذر تحميل بيانات المصروفات.');
    } finally {
      this.loading = false;
    }
  }

  get pendingInvoices(): ParentPaymentInvoice[] {
    let filtered = this.invoices.filter(invoice => invoice.remainingAmount > 0);
    if (this.selectedTerm !== 'الكل') {
      filtered = filtered.filter(i => i.term === this.selectedTerm);
    }
    return filtered;
  }

  get paymentHistory(): ParentPaymentInvoice[] {
    let filtered = this.invoices.filter(invoice => invoice.amountPaid > 0);
    if (this.selectedTerm !== 'الكل') {
      filtered = filtered.filter(i => i.term === this.selectedTerm);
    }
    return filtered;
  }

  setTerm(term: string): void {
    this.selectedTerm = term;
  }

  get remainingTotal(): number {
    return this.pendingInvoices.reduce((sum, invoice) => sum + invoice.remainingAmount, 0);
  }

  get paidTotal(): number {
    return this.invoices.reduce((sum, invoice) => sum + invoice.amountPaid, 0);
  }

  get overdueCount(): number {
    return this.pendingInvoices.filter(invoice => invoice.status === 'Overdue').length;
  }

  selectMethod(method: PaymentMethod): void {
    this.selectedMethod = method;
  }

  openGateway(invoice: ParentPaymentInvoice): void {
    if (invoice.remainingAmount <= 0) return;
    this.activeInvoice = invoice;
    this.showGateway = true;
    this.paymentStep = 'input';
    this.gatewayLoading = false;
    this.cardDetails = { number: '', expiry: '', cvc: '', name: '' };
  }

  closeGateway(): void {
    this.showGateway = false;
    this.activeInvoice = null;
  }

  async processGatewayPayment(): Promise<void> {
    if (!this.activeInvoice) return;
    
    this.paymentStep = 'processing';
    this.gatewayLoading = true;

    // Simulate network delay
    await new Promise(r => setTimeout(r, 2000));

    try {
      const result = await this.parentService.payInvoice(this.activeInvoice.id, {
        amount: this.activeInvoice.remainingAmount,
        method: this.selectedMethod,
        note: 'سداد عبر المنصة التعليمية'
      });

      this.lastReference = result.referenceNumber || `TXN-${Math.floor(Math.random() * 1000000)}`;
      this.handlePaymentSuccess(this.activeInvoice, { ...result, referenceNumber: this.lastReference });
      this.paymentStep = 'success';
      await this.loadInvoices();
    } catch (error) {
      console.error('Gateway payment failed:', error);
      this.notify.error('فشلت عملية السداد، يرجى المحاولة لاحقًا.');
      this.paymentStep = 'input';
    } finally {
      this.gatewayLoading = false;
    }
  }

  printReceipt(invoice: ParentPaymentInvoice): void {
    this.receiptToShow = invoice;
    setTimeout(() => {
      window.print();
      this.receiptToShow = null;
    }, 500);
  }

  async payInvoice(invoice: ParentPaymentInvoice): Promise<void> {
    this.openGateway(invoice);
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('ar-EG', {
      style: 'currency',
      currency: 'EGP',
      maximumFractionDigits: 0
    }).format(value || 0);
  }

  getStatusLabel(status: ParentPaymentInvoice['status']): string {
    switch (status) {
      case 'Paid':
        return 'مسددة';
      case 'Partial':
        return 'سداد جزئي';
      case 'Overdue':
        return 'متأخرة';
      default:
        return 'مستحقة';
    }
  }

  getStatusClass(status: ParentPaymentInvoice['status']): string {
    switch (status) {
      case 'Paid':
        return 'status-paid';
      case 'Partial':
        return 'status-partial';
      case 'Overdue':
        return 'status-overdue';
      default:
        return 'status-pending';
    }
  }

  private handlePaymentSuccess(invoice: ParentPaymentInvoice, result: ParentPaymentResult): void {
    const paymentMessage = `تم سداد ${this.formatCurrency(result.paidAmount)} لفاتورة ${invoice.title}.`;
    this.notify.success(paymentMessage);
    this.notificationCenter.addNotification({
      title: 'تم دفع المصروفات',
      message: paymentMessage,
      type: 'payment',
      data: {
        studentName: invoice.studentName,
        ...result
      }
    });
  }
}
