import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.css']
})
export class ResetPasswordComponent implements OnInit {
  resetForm: FormGroup;
  loading = false;
  error = '';
  email = '';
  token = '';
  tokenMode = false;
  otpVerified = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private route: ActivatedRoute,
    private router: Router,
    private notificationService: NotificationService
  ) {
    this.resetForm = this.fb.group({
      otp: ['', [Validators.required, Validators.pattern(/^[0-9]{6}$/)]],
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]]
    }, { validators: this.passwordMatchValidator });
  }

  ngOnInit(): void {
    this.route.queryParams.subscribe(async params => {
      this.email = params['email'] || '';
      this.token = params['token'] || '';
      this.tokenMode = !!this.token;
      if (!this.email) {
        this.notificationService.error('البريد الإلكتروني مفقود. يرجى طلب كود جديد.');
        this.router.navigate(['/auth/forgot-password']);
        return;
      }

      if (this.tokenMode) {
        this.resetForm.get('otp')?.clearValidators();
        this.resetForm.get('otp')?.updateValueAndValidity();
        await this.validateResetToken();
      } else {
        this.resetForm.get('otp')?.setValidators([Validators.required, Validators.pattern(/^[0-9]{6}$/)]);
        this.resetForm.get('otp')?.updateValueAndValidity();
      }
    });
  }

  onOtpInput(): void {
    const otp = this.normalizeOtp(this.resetForm.get('otp')?.value || '');
    this.resetForm.get('otp')?.setValue(otp, { emitEvent: false });
  }

  async onVerifyOtp(): Promise<void> {
    const otp = this.normalizeOtp(this.resetForm.get('otp')?.value || '');
    this.resetForm.get('otp')?.setValue(otp, { emitEvent: false });

    if (!/^[0-9]{6}$/.test(otp)) {
      this.error = 'يرجى إدخال كود تحقق صحيح مكون من 6 أرقام.';
      return;
    }

    this.loading = true;
    this.error = '';

    try {
      await this.authService.verifyResetOtp(this.email, otp);
      this.otpVerified = true;
      this.notificationService.success('تم التحقق من الكود بنجاح. يمكنك الآن تغيير كلمة المرور.');
    } catch (err: any) {
      this.error = err?.message || 'كود التحقق غير صحيح أو منتهي الصلاحية.';
      this.notificationService.error(this.error);
    } finally {
      this.loading = false;
    }
  }

  async onSubmit(): Promise<void> {
    if (this.resetForm.invalid || !this.otpVerified) {
      return;
    }

    this.loading = true;
    this.error = '';

    try {
      if (this.tokenMode) {
        await this.authService.resetPasswordWithToken(
          this.email,
          this.token,
          this.resetForm.get('newPassword')?.value
        );
      } else {
        await this.authService.verifyForgotPasswordOtp(
          this.email,
          this.normalizeOtp(this.resetForm.get('otp')?.value || ''),
          this.resetForm.get('newPassword')?.value
        );
      }
      this.notificationService.success('تم تغيير كلمة المرور بنجاح. يمكنك الآن تسجيل الدخول.');
      this.router.navigate(['/auth/login']);
    } catch (err: any) {
      this.error = err?.message || 'حدث خطأ أثناء تغيير كلمة المرور.';
      this.notificationService.error(this.error);
    } finally {
      this.loading = false;
    }
  }

  private async validateResetToken(): Promise<void> {
    this.loading = true;
    this.error = '';

    try {
      const response = await this.authService.validateResetToken(this.email, this.token);
      if (!response.isValid) {
        this.error = 'رابط إعادة تعيين كلمة المرور غير صالح أو انتهت صلاحيته.';
        this.notificationService.error(this.error);
        return;
      }

      this.otpVerified = true;
      this.notificationService.success('تم التحقق من رابط إعادة التعيين. يمكنك الآن اختيار كلمة مرور جديدة.');
    } catch (err: any) {
      this.error = err?.message || 'تعذر التحقق من رابط إعادة تعيين كلمة المرور.';
      this.notificationService.error(this.error);
    } finally {
      this.loading = false;
    }
  }

  private passwordMatchValidator(group: FormGroup) {
    return group.get('newPassword')?.value === group.get('confirmPassword')?.value
      ? null
      : { mismatch: true };
  }

  private normalizeOtp(value: string): string {
    return value
      .trim()
      .replace(/[٠-٩]/g, digit => String(digit.charCodeAt(0) - 0x0660))
      .replace(/[۰-۹]/g, digit => String(digit.charCodeAt(0) - 0x06f0))
      .replace(/\D/g, '')
      .slice(0, 6);
  }
}
