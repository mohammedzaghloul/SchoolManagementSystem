import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

import { ApiService } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/services/auth.service';
import { NotificationService } from '../../../../core/services/notification.service';

interface CreateAdminForm {
  fullName: string;
  email: string;
  password: string;
}

interface UserSummaryDto {
  id: string;
  fullName: string;
  email: string;
  role: string;
}

@Component({
  selector: 'app-admin-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './admin-management.component.html',
  styleUrls: ['./admin-management.component.css']
})
export class AdminManagementComponent implements OnInit {
  readonly ownerEmail = 'mohammedzaghloul0123@gmail.com';

  form: CreateAdminForm = this.createEmptyForm();
  createdAdmin: UserSummaryDto | null = null;
  canCreateAdmins = false;
  submitting = false;
  error = '';

  constructor(
    private api: ApiService,
    private authService: AuthService,
    private notifications: NotificationService
  ) {}

  ngOnInit(): void {
    this.canCreateAdmins = this.authService.canCreateAdmins();
  }

  async createAdmin(): Promise<void> {
    this.error = '';
    this.createdAdmin = null;

    if (!this.canCreateAdmins) {
      this.error = 'إنشاء مدير جديد متاح فقط لمالك النظام.';
      this.notifications.error(this.error, 'صلاحيات غير كافية');
      return;
    }

    if (!this.isValidForm()) {
      this.error = 'اكتب الاسم والبريد وكلمة مرور لا تقل عن 6 أحرف.';
      this.notifications.warning(this.error, 'راجع البيانات');
      return;
    }

    this.submitting = true;

    try {
      this.createdAdmin = await this.api.post<UserSummaryDto>('/api/admin/admins', {
        fullName: this.form.fullName.trim(),
        email: this.form.email.trim(),
        password: this.form.password
      });

      this.notifications.success('تم إنشاء حساب المدير بنجاح.', 'تم الحفظ');
      this.form = this.createEmptyForm();
    } catch (err: any) {
      this.error = err?.message || 'تعذر إنشاء المدير الآن.';
      this.notifications.error(this.error, 'حدث خطأ');
    } finally {
      this.submitting = false;
    }
  }

  private isValidForm(): boolean {
    return Boolean(
      this.form.fullName.trim() &&
      this.form.email.trim().includes('@') &&
      this.form.password.length >= 6
    );
  }

  private createEmptyForm(): CreateAdminForm {
    return {
      fullName: '',
      email: '',
      password: 'Admin@123'
    };
  }
}
