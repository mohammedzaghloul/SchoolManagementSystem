import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';

import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';

type ForgotPasswordStep = 'email' | 'otp' | 'done';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.css']
})
export class ForgotPasswordComponent implements OnInit {
  forgotPasswordForm!: FormGroup;
  loading = false;
  step: ForgotPasswordStep = 'email';
  error = '';
  infoMessage = '';

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.forgotPasswordForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      otp: ['', [Validators.pattern(/^\d{6}$/)]],
      newPassword: ['', [Validators.minLength(6)]],
      confirmPassword: ['']
    });
  }

  get isSubmitted(): boolean {
    return this.step === 'done';
  }

  get passwordsMismatch(): boolean {
    const password = this.forgotPasswordForm.get('newPassword')?.value;
    const confirmPassword = this.forgotPasswordForm.get('confirmPassword')?.value;
    return this.step === 'otp' && !!password && !!confirmPassword && password !== confirmPassword;
  }

  async onSubmit(): Promise<void> {
    if (this.step === 'email') {
      await this.sendOtp();
      return;
    }

    if (this.step === 'otp') {
      await this.verifyOtp();
    }
  }

  async resendOtp(): Promise<void> {
    await this.sendOtp();
  }

  private async sendOtp(): Promise<void> {
    const emailControl = this.forgotPasswordForm.get('email');
    emailControl?.markAsTouched();

    if (emailControl?.invalid) {
      return;
    }

    this.loading = true;
    this.error = '';
    this.infoMessage = '';

    try {
      const email = emailControl?.value;
      const response = await this.authService.sendForgotPasswordOtp(email);
      this.step = 'otp';
      this.infoMessage = response?.devOtp
        ? `كود الاختبار: ${response.devOtp}`
        : (response?.message || 'تم إرسال كود التحقق إلى بريدك الإلكتروني.');
      this.notificationService.success(this.infoMessage);
    } catch (error: any) {
      this.error = error?.message || 'تعذر إرسال كود التحقق الآن.';
      this.notificationService.error(this.error);
    } finally {
      this.loading = false;
    }
  }

  private async verifyOtp(): Promise<void> {
    const email = this.forgotPasswordForm.get('email')?.value;
    const otp = this.forgotPasswordForm.get('otp')?.value;
    const newPassword = this.forgotPasswordForm.get('newPassword')?.value;

    this.forgotPasswordForm.get('otp')?.markAsTouched();
    this.forgotPasswordForm.get('newPassword')?.markAsTouched();
    this.forgotPasswordForm.get('confirmPassword')?.markAsTouched();

    if (!otp || !/^\d{6}$/.test(otp)) {
      this.error = 'يرجى إدخال كود تحقق صحيح مكون من 6 أرقام.';
      return;
    }

    if (!newPassword || newPassword.length < 6) {
      this.error = 'كلمة المرور الجديدة يجب ألا تقل عن 6 أحرف.';
      return;
    }

    if (this.passwordsMismatch) {
      this.error = 'تأكيد كلمة المرور غير مطابق.';
      return;
    }

    this.loading = true;
    this.error = '';

    try {
      const response = await this.authService.verifyForgotPasswordOtp(email, otp, newPassword);
      this.step = 'done';
      this.infoMessage = response?.message || 'تم تحديث كلمة المرور بنجاح.';
      this.notificationService.success(this.infoMessage);
    } catch (error: any) {
      this.error = error?.message || 'تعذر التحقق من الكود.';
      this.notificationService.error(this.error);
    } finally {
      this.loading = false;
    }
  }
}
