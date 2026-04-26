import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  ParentPaymentInvoice,
  ParentPaymentMethod,
  ParentPaymentResult,
  ParentService
} from '../../../core/services/parent.service';
import { NotificationService } from '../../../core/services/notification.service';
import { NotificationCenterService } from '../../../core/services/notification-center.service';
import { environment } from '../../../../environments/environment';

type PaymentStep = 'input' | 'processing' | 'otp' | 'reference' | 'finalizing' | 'success';

@Component({
  selector: 'app-parent-payments',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './payments.component.html',
  styleUrls: ['./payments.component.css']
})
export class PaymentsComponent implements OnInit {
  loading = true;
  payingInvoiceId: number | null = null;
  selectedMethod = environment.paymentGateway.vodafoneCashNumber ? 'vodafone_cash' : 'card';
  invoices: ParentPaymentInvoice[] = [];

  showGateway = false;
  activeInvoice: ParentPaymentInvoice | null = null;
  gatewayLoading = false;
  cardDetails = { number: '', expiry: '', cvc: '', name: '' };
  paymentStep: PaymentStep = 'input';
  otpCode = '';
  lastReference: string | null = null;
  paymentCode = '';
  gatewayMessage = '';
  successMessage = '';
  verificationTarget = '';

  paymentAmount = 0;
  walletNumber = environment.paymentGateway.vodafoneCashNumber;
  supportNumber = environment.paymentGateway.supportPhone;
  receiptToShow: ParentPaymentInvoice | null = null;
  selectedTerm = 'الكل';

  paymentMethods: ParentPaymentMethod[] = [
    {
      code: 'vodafone_cash',
      label: 'Vodafone Cash',
      hint: 'إرسال التعليمات ورقم المرجع على رقمك مباشرة',
      icon: 'fas fa-mobile-screen-button',
      providerCode: 'VODAFONE_CASH',
      receiver: environment.paymentGateway.vodafoneCashNumber,
      requiresOtp: true,
      requiresReferenceConfirmation: false
    },
    {
      code: 'instapay',
      label: 'InstaPay',
      hint: 'تحويل لحظي ثم تأكيد رقم المرجع',
      icon: 'fas fa-building-columns',
      providerCode: 'INSTAPAY',
      receiver: 'school@instapay',
      requiresOtp: false,
      requiresReferenceConfirmation: true
    },
    {
      code: 'card',
      label: 'بطاقة بنكية',
      hint: 'محاكاة دفع آمنة داخل البوابة بدون أي مفاتيح خارجية',
      icon: 'far fa-credit-card',
      providerCode: 'CARD_DEMO',
      requiresOtp: true,
      requiresReferenceConfirmation: false
    },
    {
      code: 'fawry',
      label: 'فوري',
      hint: 'إنشاء كود سداد فوري للاستخدام في نقاط الدفع',
      icon: 'fas fa-barcode',
      providerCode: 'FAWRY',
      receiver: '788',
      requiresOtp: false,
      requiresReferenceConfirmation: true
    }
  ];

  constructor(
    private parentService: ParentService,
    private notify: NotificationService,
    private notificationCenter: NotificationCenterService
  ) {}

  async ngOnInit(): Promise<void> {
    await Promise.all([this.loadPaymentMethods(), this.loadInvoices()]);
  }

  get termOptions(): string[] {
    const terms = new Set<string>(['الكل']);
    this.invoices.forEach(invoice => {
      if (invoice.term) {
        terms.add(invoice.term);
      }
    });
    return Array.from(terms);
  }

  get pendingInvoices(): ParentPaymentInvoice[] {
    let filtered = this.invoices.filter(invoice => invoice.remainingAmount > 0);
    if (this.selectedTerm !== 'الكل') {
      filtered = filtered.filter(invoice => invoice.term === this.selectedTerm);
    }
    return filtered.sort((left, right) => new Date(left.dueDate).getTime() - new Date(right.dueDate).getTime());
  }

  get paymentHistory(): ParentPaymentInvoice[] {
    let filtered = this.invoices.filter(invoice => invoice.amountPaid > 0);
    if (this.selectedTerm !== 'الكل') {
      filtered = filtered.filter(invoice => invoice.term === this.selectedTerm);
    }
    return filtered.sort((left, right) => new Date(right.paidAt || right.createdAt).getTime() - new Date(left.paidAt || left.createdAt).getTime());
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

  get canSubmitGateway(): boolean {
    if (!this.activeInvoice || this.gatewayLoading || this.normalizedPaymentAmount <= 0) {
      return false;
    }

    if (this.selectedMethod === 'card') {
      return this.isCardFormValid();
    }

    if (this.selectedMethod === 'vodafone_cash') {
      return this.getDigitsOnly(this.walletNumber).length >= 4;
    }

    return true;
  }

  async loadPaymentMethods(): Promise<void> {
    try {
      const methods = await this.parentService.getPaymentMethods();
      if (Array.isArray(methods) && methods.length > 0) {
        this.paymentMethods = methods;
        this.selectedMethod = methods.some(method => method.code === this.selectedMethod)
          ? this.selectedMethod
          : methods[0].code;
        this.walletNumber = methods.find(method => method.code === 'vodafone_cash')?.receiver || this.walletNumber;
      }
    } catch (error) {
      console.warn('Failed to load payment methods, using local fallback:', error);
    }
  }

  async loadInvoices(): Promise<void> {
    this.loading = true;

    try {
      const response = await this.parentService.getPayments();
      const normalized = this.normalizeInvoices(response || []);
      this.invoices = this.needsDemoInvoices(normalized)
        ? this.mergeDemoInvoices(normalized)
        : normalized;
    } catch (error) {
      console.error('Failed to load parent payments:', error);
      this.invoices = this.getDemoInvoices();
      this.notify.error('تعذر تحميل بيانات المصروفات من الخادم، فتم عرض بيانات جاهزة للتجربة.');
    } finally {
      this.loading = false;
    }
  }

  setTerm(term: string): void {
    this.selectedTerm = term;
  }

  selectMethod(method: string): void {
    this.selectedMethod = method;
    this.gatewayMessage = '';
    this.otpCode = '';
    this.paymentCode = '';
  }

  openGateway(invoice: ParentPaymentInvoice): void {
    if (invoice.remainingAmount <= 0) {
      return;
    }

    this.activeInvoice = invoice;
    this.showGateway = true;
    this.gatewayLoading = false;
    this.paymentStep = 'input';
    this.paymentAmount = Number(invoice.remainingAmount.toFixed(2));
    this.otpCode = '';
    this.lastReference = null;
    this.paymentCode = '';
    this.gatewayMessage = '';
    this.successMessage = '';
    this.verificationTarget = '';
    this.cardDetails = { number: '', expiry: '', cvc: '', name: '' };
  }

  closeGateway(): void {
    this.showGateway = false;
    this.activeInvoice = null;
    this.gatewayLoading = false;
    this.paymentStep = 'input';
    this.otpCode = '';
    this.paymentCode = '';
    this.gatewayMessage = '';
    this.successMessage = '';
    this.lastReference = null;
    this.payingInvoiceId = null;
  }

  async processGatewayPayment(): Promise<void> {
    if (!this.activeInvoice || !this.canSubmitGateway) {
      return;
    }

    this.paymentStep = 'processing';
    this.gatewayLoading = true;
    await this.delay(1200);
    this.gatewayLoading = false;

    const method = this.activeMethod;

    if (this.selectedMethod === 'card') {
        this.verificationTarget = 'البطاقة البنكية';
        this.gatewayMessage = `تم إرسال رمز تحقق بنكي لإتمام سداد ${this.formatCurrency(this.normalizedPaymentAmount)}.`;
        this.paymentStep = 'otp';
        return;
    }

    if (this.selectedMethod === 'vodafone_cash') {
        this.paymentCode = this.generateReference('VC');
        this.verificationTarget = this.maskPhone(this.walletNumber);
        this.gatewayMessage = `تم إرسال تعليمات السداد ورقم المرجع ${this.paymentCode} إلى رقم فودافون كاش ${this.maskPhone(this.walletNumber)}.`;
        this.paymentStep = 'otp';
        return;
    }

    const prefix = this.selectedMethod === 'instapay' ? 'IPY' : 'FWY';
    this.paymentCode = this.generateReference(prefix);
    this.gatewayMessage = this.selectedMethod === 'instapay'
      ? `حوّل ${this.formatCurrency(this.normalizedPaymentAmount)} إلى ${method?.receiver || 'حساب المدرسة'} ثم أكد رقم المرجع ${this.paymentCode}.`
      : `استخدم الكود ${this.paymentCode} لسداد ${this.formatCurrency(this.normalizedPaymentAmount)} من أي نقطة فوري خلال 24 ساعة.`;
    this.paymentStep = 'reference';
  }

  async verifyOtp(): Promise<void> {
    if (this.otpCode.trim().length < 4) {
      return;
    }

    await this.completePayment(this.buildGatewayNote());
  }

  async confirmReferencePayment(): Promise<void> {
    await this.completePayment(this.buildGatewayNote());
  }

  formatCardNumber(event: Event): void {
    const target = event.target as HTMLInputElement;
    const value = this.getDigitsOnly(target.value).slice(0, 16);
    const parts = value.match(/.{1,4}/g) || [];
    this.cardDetails.number = parts.join(' ');
  }

  formatExpiry(event: Event): void {
    const target = event.target as HTMLInputElement;
    const digits = this.getDigitsOnly(target.value).slice(0, 4);
    const formatted = digits.length >= 3 ? `${digits.slice(0, 2)}/${digits.slice(2)}` : digits;
    this.cardDetails.expiry = formatted;
  }

  showReceipt(invoice: ParentPaymentInvoice): void {
    this.receiptToShow = invoice;
  }

  closeReceipt(): void {
    this.receiptToShow = null;
  }

  printReceipt(): void {
    if (!this.receiptToShow) {
      return;
    }

    setTimeout(() => {
      window.print();
    }, 50);
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

  getMethodLabel(method: string): string {
    return this.paymentMethods.find(item => item.code === method)?.label || method;
  }

  private async completePayment(note: string): Promise<void> {
    if (!this.activeInvoice) {
      return;
    }

    const invoice = this.activeInvoice;
    const amount = this.normalizedPaymentAmount;
    const methodLabel = this.getMethodLabel(this.selectedMethod);

    this.paymentStep = 'finalizing';
    this.gatewayLoading = true;
    this.payingInvoiceId = invoice.id;

    await this.delay(1500);

    try {
      const result = this.isDemoInvoice(invoice)
        ? this.applyLocalPayment(invoice, amount, methodLabel)
        : await this.parentService.payInvoice(invoice.id, {
            amount,
            method: methodLabel,
            methodCode: this.selectedMethod,
            note
          });

      this.lastReference = result.referenceNumber || this.generateReference('PAY');
      this.successMessage = this.buildSuccessMessage(amount, methodLabel);
      this.handlePaymentSuccess(invoice, {
        ...result,
        referenceNumber: this.lastReference,
        paymentMethod: result.paymentMethod || methodLabel
      });
      this.paymentStep = 'success';

      if (!this.isDemoInvoice(invoice)) {
        await this.loadInvoices();
      } else {
        this.activeInvoice = this.invoices.find(item => item.id === invoice.id) || invoice;
      }
    } catch (error) {
      console.error('Gateway payment failed:', error);
      this.notify.error('فشلت عملية السداد، يرجى المحاولة مرة أخرى.');
      this.paymentStep = 'input';
    } finally {
      this.gatewayLoading = false;
      this.payingInvoiceId = null;
    }
  }

  private handlePaymentSuccess(invoice: ParentPaymentInvoice, result: ParentPaymentResult): void {
    const paymentMessage = `تم سداد ${this.formatCurrency(result.paidAmount)} لفاتورة ${invoice.title} عبر ${result.paymentMethod || this.getMethodLabel(this.selectedMethod)}.`;
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

  private normalizeInvoices(invoices: ParentPaymentInvoice[]): ParentPaymentInvoice[] {
    return invoices
      .map((invoice, index) => ({
        ...invoice,
        id: Number(invoice.id || index + 1),
        title: this.isReadableText(invoice.title) ? invoice.title.trim() : `فاتورة دراسية ${index + 1}`,
        description: invoice.description || 'رسوم دراسية وخدمات مدرسية',
        academicYear: invoice.academicYear || '2025/2026',
        term: invoice.term || 'الفصل الثاني',
        amount: this.normalizeMoney(invoice.amount, 0),
        amountPaid: this.normalizeMoney(invoice.amountPaid, 0),
        remainingAmount: this.normalizeMoney(invoice.remainingAmount, Math.max(this.normalizeMoney(invoice.amount, 0) - this.normalizeMoney(invoice.amountPaid, 0), 0)),
        dueDate: invoice.dueDate || new Date().toISOString(),
        createdAt: invoice.createdAt || new Date().toISOString(),
        paidAt: invoice.paidAt || null,
        status: invoice.status || 'Pending',
        paymentMethod: invoice.paymentMethod,
        referenceNumber: invoice.referenceNumber,
        studentId: Number(invoice.studentId || index + 1),
        studentName: this.isReadableText(invoice.studentName) ? invoice.studentName : 'الطالب'
      }))
      .filter(invoice => invoice.amount > 0 && invoice.amount < 100000)
      .sort((left, right) => new Date(left.dueDate).getTime() - new Date(right.dueDate).getTime());
  }

  private needsDemoInvoices(invoices: ParentPaymentInvoice[]): boolean {
    return invoices.length < 3 || invoices.filter(invoice => invoice.remainingAmount > 0).length < 2;
  }

  private mergeDemoInvoices(invoices: ParentPaymentInvoice[]): ParentPaymentInvoice[] {
    const merged = [...invoices];
    const seen = new Set(merged.map(invoice => `${invoice.title}-${invoice.studentName}-${invoice.term}`));

    for (const demoInvoice of this.getDemoInvoices()) {
      const key = `${demoInvoice.title}-${demoInvoice.studentName}-${demoInvoice.term}`;
      if (!seen.has(key)) {
        merged.push(demoInvoice);
      }
    }

    return merged.sort((left, right) => new Date(left.dueDate).getTime() - new Date(right.dueDate).getTime());
  }

  private getDemoInvoices(): ParentPaymentInvoice[] {
    const today = new Date();
    const twoDaysAgo = new Date(today);
    twoDaysAgo.setDate(today.getDate() - 2);
    const inTenDays = new Date(today);
    inTenDays.setDate(today.getDate() + 10);
    const inTwentyDays = new Date(today);
    inTwentyDays.setDate(today.getDate() + 20);

    return [
      {
        id: -101,
        title: 'مصروفات الفصل الثاني',
        description: 'القسط الحالي للطالب أحمد خالد',
        academicYear: '2025/2026',
        term: 'الفصل الثاني',
        amount: 4200,
        amountPaid: 1800,
        remainingAmount: 2400,
        dueDate: twoDaysAgo.toISOString(),
        createdAt: new Date(today.getFullYear(), today.getMonth() - 1, 18).toISOString(),
        paidAt: new Date(today.getFullYear(), today.getMonth() - 1, 20).toISOString(),
        status: 'Overdue',
        paymentMethod: 'بطاقة بنكية',
        referenceNumber: 'INV-DEMO-2401',
        studentId: 1,
        studentName: 'أحمد خالد'
      },
      {
        id: -102,
        title: 'رسوم الأنشطة والخدمات',
        description: 'رسوم المنصة الرقمية والأنشطة العلمية',
        academicYear: '2025/2026',
        term: 'الخدمات',
        amount: 950,
        amountPaid: 0,
        remainingAmount: 950,
        dueDate: inTenDays.toISOString(),
        createdAt: new Date(today.getFullYear(), today.getMonth(), 3).toISOString(),
        paidAt: null,
        status: 'Pending',
        paymentMethod: undefined,
        referenceNumber: undefined,
        studentId: 1,
        studentName: 'أحمد خالد'
      },
      {
        id: -103,
        title: 'مصروفات الفصل الأول',
        description: 'تمت تسويتها سابقًا',
        academicYear: '2025/2026',
        term: 'الفصل الأول',
        amount: 3900,
        amountPaid: 3900,
        remainingAmount: 0,
        dueDate: new Date(today.getFullYear(), today.getMonth() - 4, 10).toISOString(),
        createdAt: new Date(today.getFullYear(), today.getMonth() - 5, 14).toISOString(),
        paidAt: new Date(today.getFullYear(), today.getMonth() - 4, 9).toISOString(),
        status: 'Paid',
        paymentMethod: 'Vodafone Cash',
        referenceNumber: 'INV-DEMO-1388',
        studentId: 1,
        studentName: 'أحمد خالد'
      },
      {
        id: -104,
        title: 'رسوم باص المدرسة',
        description: 'القسط الشهري لخدمة النقل',
        academicYear: '2025/2026',
        term: 'الخدمات',
        amount: 650,
        amountPaid: 0,
        remainingAmount: 650,
        dueDate: inTwentyDays.toISOString(),
        createdAt: new Date(today.getFullYear(), today.getMonth(), 8).toISOString(),
        paidAt: null,
        status: 'Pending',
        paymentMethod: undefined,
        referenceNumber: undefined,
        studentId: 1,
        studentName: 'أحمد خالد'
      }
    ];
  }

  private applyLocalPayment(
    invoice: ParentPaymentInvoice,
    amount: number,
    methodLabel: string
  ): ParentPaymentResult {
    const referenceNumber = this.generateReference('PAY');
    const paidAt = new Date().toISOString();

    this.invoices = this.invoices.map(item => {
      if (item.id !== invoice.id) {
        return item;
      }

      const totalPaid = Number((item.amountPaid + amount).toFixed(2));
      const remainingAmount = Number(Math.max(item.amount - totalPaid, 0).toFixed(2));
      const status: ParentPaymentInvoice['status'] = remainingAmount <= 0 ? 'Paid' : 'Partial';

      return {
        ...item,
        amountPaid: totalPaid,
        remainingAmount,
        status,
        paidAt,
        paymentMethod: methodLabel,
        referenceNumber
      };
    });

    const updatedInvoice = this.invoices.find(item => item.id === invoice.id)!;

    return {
      invoiceId: updatedInvoice.id,
      paidAmount: amount,
      totalPaid: updatedInvoice.amountPaid,
      remainingAmount: updatedInvoice.remainingAmount,
      status: updatedInvoice.status,
      referenceNumber,
      paymentMethod: methodLabel,
      paidAt
    };
  }

  private buildGatewayNote(): string {
    if (this.selectedMethod === 'vodafone_cash') {
      return `سداد عبر Vodafone Cash مع إرسال التعليمات إلى الرقم ${this.walletNumber}.`;
    }

    if (this.selectedMethod === 'fawry') {
      return `سداد عبر فوري باستخدام الكود ${this.paymentCode}.`;
    }

    if (this.selectedMethod === 'instapay') {
      return `سداد عبر InstaPay إلى ${this.activeMethod?.receiver || 'حساب المدرسة'} بالمرجع ${this.paymentCode}.`;
    }

    return 'سداد عبر بوابة الدفع الآمنة داخل المنصة.';
  }

  private buildSuccessMessage(amount: number, methodLabel: string): string {
    if (this.selectedMethod === 'vodafone_cash') {
      return `تم تأكيد دفع ${this.formatCurrency(amount)} عبر ${methodLabel} وإرسال المرجع إلى ${this.maskPhone(this.walletNumber)}.`;
    }

    if (this.selectedMethod === 'fawry') {
      return `تم تأكيد السداد باستخدام كود فوري ${this.paymentCode}.`;
    }

    if (this.selectedMethod === 'instapay') {
      return `تم تأكيد تحويل ${this.formatCurrency(amount)} عبر ${methodLabel} بالمرجع ${this.paymentCode}.`;
    }

    return `تمت عملية سداد ${this.formatCurrency(amount)} عبر ${methodLabel} بنجاح.`;
  }

  private isCardFormValid(): boolean {
    return (
      this.getDigitsOnly(this.cardDetails.number).length === 16 &&
      this.cardDetails.expiry.length === 5 &&
      this.getDigitsOnly(this.cardDetails.cvc).length >= 3 &&
      this.cardDetails.name.trim().length >= 3
    );
  }

  private isDemoInvoice(invoice: ParentPaymentInvoice): boolean {
    return invoice.id < 0;
  }

  private get normalizedPaymentAmount(): number {
    if (!this.activeInvoice) {
      return 0;
    }

    const parsed = Number(this.paymentAmount);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      return 0;
    }

    return Number(Math.min(parsed, this.activeInvoice.remainingAmount).toFixed(2));
  }

  private get activeMethod(): ParentPaymentMethod | undefined {
    return this.paymentMethods.find(method => method.code === this.selectedMethod);
  }

  private normalizeMoney(value: unknown, fallback: number): number {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
  }

  private isReadableText(value?: string): boolean {
    if (!value) {
      return false;
    }

    return /[\u0600-\u06FFA-Za-z0-9]/.test(value);
  }

  private generateReference(prefix: string): string {
    return `${prefix}-${Date.now().toString().slice(-8)}`;
  }

  private maskPhone(phone: string): string {
    const digits = this.getDigitsOnly(phone);
    if (digits.length <= 4) {
      return phone || 'رقمك المسجل';
    }

    return `${digits.slice(0, 4)}****${digits.slice(-2)}`;
  }

  private getDigitsOnly(value: string): string {
    return (value || '').replace(/\D/g, '');
  }

  private delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }
}
