import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { CommonModule } from '@angular/common';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {
  loginForm!: FormGroup;
  loading = false;
  error = '';
  showPassword = false;

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
      password: ['', Validators.required]
    });
  }

  get f() { return this.loginForm.controls; }

  async onSubmit() {
    if (this.loginForm.invalid) {
      return;
    }

    this.loading = true;
    this.error = '';

    try {
      const user = await this.authService.login(this.loginForm.value);
      this.notify.success(`مرحباً بك مجدداً، ${user.fullName}`);

      // Navigate based on role
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
    } catch (err: any) {
      this.error = err?.message || 'بيانات الدخول غير صحيحة';
      this.notify.error(this.error);
    } finally {
      this.loading = false;
    }
  }
}
