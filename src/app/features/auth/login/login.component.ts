import { CommonModule } from '@angular/common';
import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';

import { AuthService, LoginOtpChallenge } from '../../../core/services/auth.service';
import { User } from '../../../core/models/user.model';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit, OnDestroy {
  loginForm!: FormGroup;
  loading = false;
  error = '';
  infoMessage = '';
  requiresOtp = false;
  pendingChallengeId = '';
  showPassword = false;
  
  cooldownSeconds = 0;
  private cooldownInterval: any;

  constructor(
    private formBuilder: FormBuilder,
    private router: Router,
    private authService: AuthService,
    private notify: NotificationService
  ) { }

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  ngOnInit() {
    this.loginForm = this.formBuilder.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', Validators.required],
      otp: ['', [Validators.pattern(/^\d{6}$/)]]
    });
  }

  ngOnDestroy(): void {
    if (this.cooldownInterval) {
      clearInterval(this.cooldownInterval);
    }
  }

  get f() { return this.loginForm.controls; }

  async onSubmit() {
    if (this.requiresOtp) {
      await this.verifyOtp();
      return;
    }

    if (this.loginForm.get('email')?.invalid || this.loginForm.get('password')?.invalid) {
      this.loginForm.get('email')?.markAsTouched();
      this.loginForm.get('password')?.markAsTouched();
      return;
    }

    this.loading = true;
    this.error = '';
    this.infoMessage = '';

    try {
      const result = await this.authService.login({
        email: this.loginForm.get('email')?.value,
        password: this.loginForm.get('password')?.value
      });

      if (this.isOtpChallenge(result)) {
        this.requiresOtp = true;
        this.pendingChallengeId = result.challengeId;
        this.infoMessage = result.devOtp
          ? `كود الاختبار: ${result.devOtp}`
          : (result.message || 'تم إرسال كود التحقق إلى بريدك الإلكتروني.');
        this.notify.info(this.infoMessage, 'تحقق من الجهاز الجديد');
        
        this.cooldownSeconds = 120;
        this.startCooldownTimer();
        return;
      }

      this.completeLogin(result);
    } catch (err: any) {
      this.error = this.translateError(err?.message) || 'بيانات الدخول غير صحيحة';
    } finally {
      this.loading = false;
    }
  }

  async resendOtp() {
    if (this.cooldownSeconds > 0) return;

    this.loading = true;
    this.error = '';

    try {
      const result = await this.authService.login({
        email: this.loginForm.get('email')?.value,
        password: this.loginForm.get('password')?.value
      });

      if (this.isOtpChallenge(result)) {
        this.pendingChallengeId = result.challengeId;
        this.infoMessage = result.devOtp
          ? `كود الاختبار: ${result.devOtp}`
          : (result.message || 'تم إعادة إرسال كود التحقق بنجاح.');
        this.notify.success(this.infoMessage, 'إعادة الإرسال');

        this.cooldownSeconds = 120;
        this.startCooldownTimer();
      } else {
        this.completeLogin(result);
      }
    } catch (err: any) {
      this.error = this.translateError(err?.message) || 'حدث خطأ أثناء إعادة إرسال الكود.';
    } finally {
      this.loading = false;
    }
  }

  startCooldownTimer(): void {
    if (this.cooldownInterval) clearInterval(this.cooldownInterval);
    
    this.cooldownInterval = setInterval(() => {
      this.cooldownSeconds--;
      if (this.cooldownSeconds <= 0) {
        clearInterval(this.cooldownInterval);
        this.cooldownSeconds = 0;
      }
    }, 1000);
  }

  resetOtpStep(): void {
    this.requiresOtp = false;
    this.pendingChallengeId = '';
    this.infoMessage = '';
    this.error = '';
    this.cooldownSeconds = 0;
    if (this.cooldownInterval) clearInterval(this.cooldownInterval);
    this.loginForm.get('otp')?.reset();
  }

  private async verifyOtp(): Promise<void> {
    const otpControl = this.loginForm.get('otp');
    otpControl?.markAsTouched();

    if (!this.pendingChallengeId || otpControl?.invalid || !otpControl?.value) {
      this.error = 'يرجى إدخال كود تحقق صحيح مكون من 6 أرقام.';
      return;
    }

    this.loading = true;
    this.error = '';

    try {
      const user = await this.authService.verifyLoginOtp(this.pendingChallengeId, otpControl.value);
      this.completeLogin(user);
    } catch (err: any) {
      this.error = this.translateError(err?.message) || 'تعذر التحقق من كود الدخول.';
    } finally {
      this.loading = false;
    }
  }

  private isOtpChallenge(result: User | LoginOtpChallenge): result is LoginOtpChallenge {
    return !!(result as LoginOtpChallenge).requiresOtp && !!(result as LoginOtpChallenge).challengeId;
  }

  private completeLogin(user: User): void {
    this.notify.success(`مرحباً بك مجدداً، ${user.fullName}`);

    switch (user.role) {
      case 'Admin':
        this.router.navigate(['/admin/dashboard']);
        break;
      case 'Teacher':
        this.router.navigate(['/teacher/dashboard']);
        break;
      case 'Student':
        this.router.navigate(['/student/dashboard']);
        break;
      case 'Parent':
        this.router.navigate(['/parent/dashboard']);
        break;
      default:
        this.router.navigate(['/']);
    }
  }

  private translateError(message: string): string {
    if (!message) return '';
    if (message.includes('rate limit')) return 'لقد تجاوزت حد المحاولات المسموح به. يرجى المحاولة بعد قليل.';
    if (message.includes('الرجاء الانتظار')) return message;
    return message;
  }
}
