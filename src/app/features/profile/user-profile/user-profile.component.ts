import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { RouterModule } from '@angular/router';

import { AuthService } from '../../../core/services/auth.service';
import { UserService } from '../../../core/services/user.service';
import { User } from '../../../core/models/user.model';

type PasswordField = 'current' | 'new' | 'confirm';

@Component({
  selector: 'app-user-profile',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule],
  templateUrl: './user-profile.component.html',
  styleUrl: './user-profile.component.css'
})
export class UserProfileComponent implements OnInit {
  currentUser: User | null = null;

  profileLoading = false;
  passwordLoading = false;
  avatarLoading = false;

  profileSuccess = false;
  passwordSuccess = false;

  profileError = '';
  passwordError = '';

  passwordVisibility: Record<PasswordField, boolean> = {
    current: false,
    new: false,
    confirm: false
  };

  readonly profileForm = this.fb.group({
    fullName: ['', [Validators.required]],
    email: [{ value: '', disabled: true }, [Validators.required, Validators.email]],
    phone: ['']
  });

  readonly passwordForm = this.fb.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, Validators.minLength(6), this.passwordStrengthValidator()]],
      confirmPassword: ['', [Validators.required]]
    },
    {
      validators: [this.passwordsMatchValidator()]
    }
  );

  constructor(
    private readonly authService: AuthService,
    private readonly userService: UserService,
    private readonly fb: FormBuilder
  ) { }

  async ngOnInit(): Promise<void> {
    await this.loadProfile();
  }

  get userInitial(): string {
    const source = this.currentUser?.fullName || this.currentUser?.email || '?';
    return source.charAt(0).toUpperCase();
  }

  getRoleText(role: string): string {
    const roles: Record<string, string> = {
      Admin: 'مدير النظام',
      Teacher: 'معلم',
      Student: 'طالب',
      Parent: 'ولي أمر'
    };

    return roles[role] || role || 'مستخدم';
  }

  async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0];

    if (!file) {
      return;
    }

    this.clearProfileFeedback();

    if (!file.type.startsWith('image/')) {
      this.profileError = 'يرجى اختيار ملف صورة صالح.';
      if (input) {
        input.value = '';
      }
      return;
    }

    this.avatarLoading = true;

    try {
      const response = await this.userService.uploadProfileImage(file);
      const avatar = response.avatar || response.url;

      this.currentUser = {
        ...(this.currentUser || this.createEmptyUser()),
        avatar: avatar || this.currentUser?.avatar
      };

      this.profileSuccess = true;
    } catch (error: any) {
      this.profileError = this.extractServerMessage(error, 'فشل في تحديث الصورة الشخصية.');
    } finally {
      this.avatarLoading = false;
      if (input) {
        input.value = '';
      }
    }
  }

  async saveProfile(): Promise<void> {
    this.clearProfileFeedback();

    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      this.profileError = 'يرجى استكمال البيانات المطلوبة أولاً.';
      return;
    }

    this.profileLoading = true;

    try {
      const formValue = this.profileForm.getRawValue();
      const updatedUser = await this.userService.updateProfile({
        fullName: formValue.fullName?.trim(),
        phone: formValue.phone?.trim()
      });

      this.currentUser = {
        ...(this.currentUser || this.createEmptyUser()),
        ...updatedUser
      };

      this.profileForm.patchValue({
        fullName: this.currentUser.fullName || '',
        email: this.currentUser.email || '',
        phone: this.currentUser.phone || ''
      });

      this.profileSuccess = true;
    } catch (error: any) {
      this.profileError = this.extractServerMessage(error, 'حدث خطأ أثناء حفظ البيانات.');
    } finally {
      this.profileLoading = false;
    }
  }

  async changePassword(): Promise<void> {
    this.clearPasswordFeedback();

    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      this.passwordError = this.getPasswordFormErrorSummary();
      return;
    }

    const currentPassword = this.passwordForm.get('currentPassword')?.value || '';
    const newPassword = this.passwordForm.get('newPassword')?.value || '';

    this.passwordLoading = true;

    try {
      await this.authService.changePassword({
        currentPassword,
        newPassword
      });

      this.passwordForm.reset();
      this.passwordVisibility = {
        current: false,
        new: false,
        confirm: false
      };
      this.passwordSuccess = true;
    } catch (error: any) {
      const rawMessage = this.extractServerMessage(error, 'حدث خطأ أثناء تغيير كلمة المرور.');
      this.passwordError = this.mapPasswordErrorMessage(rawMessage);
    } finally {
      this.passwordLoading = false;
    }
  }

  clearProfileFeedback(): void {
    this.profileError = '';
    this.profileSuccess = false;
  }

  clearPasswordFeedback(): void {
    this.passwordError = '';
    this.passwordSuccess = false;
  }

  togglePasswordVisibility(field: PasswordField): void {
    this.passwordVisibility[field] = !this.passwordVisibility[field];
  }

  getPasswordInputType(field: PasswordField): 'text' | 'password' {
    return this.passwordVisibility[field] ? 'text' : 'password';
  }

  getCurrentPasswordErrorMessage(): string {
    const control = this.passwordForm.get('currentPassword');
    if (!control || !this.shouldShowFieldError(control)) {
      return '';
    }

    if (control.hasError('required')) {
      return 'أدخل كلمة المرور الحالية.';
    }

    return '';
  }

  getNewPasswordErrorMessage(): string {
    const control = this.passwordForm.get('newPassword');
    if (!control || !this.shouldShowFieldError(control)) {
      return '';
    }

    if (control.hasError('required')) {
      return 'أدخل كلمة المرور الجديدة.';
    }

    if (control.hasError('minlength')) {
      return 'كلمة المرور الجديدة يجب ألا تقل عن 6 أحرف.';
    }

    if (control.hasError('uppercase')) {
      return 'كلمة المرور الجديدة يجب أن تحتوي على حرف كبير واحد على الأقل.';
    }

    if (control.hasError('lowercase')) {
      return 'كلمة المرور الجديدة يجب أن تحتوي على حرف صغير واحد على الأقل.';
    }

    if (control.hasError('number')) {
      return 'كلمة المرور الجديدة يجب أن تحتوي على رقم واحد على الأقل.';
    }

    if (control.hasError('special')) {
      return 'كلمة المرور الجديدة يجب أن تحتوي على رمز خاص واحد على الأقل.';
    }

    return '';
  }

  getConfirmPasswordErrorMessage(): string {
    const control = this.passwordForm.get('confirmPassword');
    if (!control || !this.shouldShowFieldError(control)) {
      return '';
    }

    if (control.hasError('required')) {
      return 'أعد كتابة كلمة المرور الجديدة.';
    }

    if (this.passwordForm.hasError('passwordMismatch')) {
      return 'تأكيد كلمة المرور غير مطابق.';
    }

    return '';
  }

  private async loadProfile(): Promise<void> {
    this.profileLoading = true;
    this.clearProfileFeedback();

    try {
      const profile = await this.userService.getCurrentUser();
      const fallbackUser = this.authService.getCurrentUser();
      this.currentUser = profile || fallbackUser;

      this.profileForm.patchValue({
        fullName: this.currentUser?.fullName || '',
        email: this.currentUser?.email || '',
        phone: this.currentUser?.phone || ''
      });
    } catch (error: any) {
      this.profileError = this.extractServerMessage(error, 'تعذر تحميل بيانات الملف الشخصي.');
      this.currentUser = this.authService.getCurrentUser();
    } finally {
      this.profileLoading = false;
    }
  }

  private createEmptyUser(): User {
    return {
      email: this.currentUser?.email || '',
      role: this.currentUser?.role || 'User'
    };
  }

  private shouldShowFieldError(control: AbstractControl): boolean {
    return control.invalid && (control.dirty || control.touched);
  }

  private getPasswordFormErrorSummary(): string {
    return (
      this.getCurrentPasswordErrorMessage()
      || this.getNewPasswordErrorMessage()
      || this.getConfirmPasswordErrorMessage()
      || 'يرجى مراجعة بيانات كلمة المرور.'
    );
  }

  private passwordsMatchValidator(): ValidatorFn {
    return (group: AbstractControl): ValidationErrors | null => {
      const newPassword = group.get('newPassword')?.value;
      const confirmPassword = group.get('confirmPassword')?.value;

      if (!newPassword || !confirmPassword) {
        return null;
      }

      return newPassword === confirmPassword ? null : { passwordMismatch: true };
    };
  }

  private passwordStrengthValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const value = `${control.value || ''}`;
      if (!value) {
        return null;
      }

      const errors: ValidationErrors = {};

      if (!/[A-Z]/.test(value)) {
        errors['uppercase'] = true;
      }

      if (!/[a-z]/.test(value)) {
        errors['lowercase'] = true;
      }

      if (!/\d/.test(value)) {
        errors['number'] = true;
      }

      if (!/[^A-Za-z0-9]/.test(value)) {
        errors['special'] = true;
      }

      return Object.keys(errors).length > 0 ? errors : null;
    };
  }

  private extractServerMessage(error: any, fallback: string): string {
    const validationErrors = error?.error?.errors;
    if (validationErrors && typeof validationErrors === 'object') {
      const firstMessage = Object.values(validationErrors)
        .flat()
        .find((value) => typeof value === 'string');

      if (typeof firstMessage === 'string' && firstMessage.trim()) {
        return firstMessage;
      }
    }

    const message =
      error?.error?.message
      || error?.message
      || error?.error?.title
      || fallback;

    return typeof message === 'string' && message.trim() ? message : fallback;
  }

  private mapPasswordErrorMessage(message: string): string {
    const normalizedMessage = message.toLowerCase();

    if (normalizedMessage.includes('current password') && normalizedMessage.includes('incorrect')) {
      return 'كلمة المرور الحالية غير صحيحة.';
    }

    if (normalizedMessage.includes('current password') && normalizedMessage.includes('wrong')) {
      return 'كلمة المرور الحالية غير صحيحة.';
    }

    if (normalizedMessage.includes('uppercase')) {
      return 'كلمة المرور الجديدة يجب أن تحتوي على حرف كبير واحد على الأقل.';
    }

    if (normalizedMessage.includes('lowercase')) {
      return 'كلمة المرور الجديدة يجب أن تحتوي على حرف صغير واحد على الأقل.';
    }

    if (normalizedMessage.includes('digit') || normalizedMessage.includes('number')) {
      return 'كلمة المرور الجديدة يجب أن تحتوي على رقم واحد على الأقل.';
    }

    if (normalizedMessage.includes('non alphanumeric') || normalizedMessage.includes('special')) {
      return 'كلمة المرور الجديدة يجب أن تحتوي على رمز خاص واحد على الأقل.';
    }

    if (normalizedMessage.includes('6') && normalizedMessage.includes('characters')) {
      return 'كلمة المرور الجديدة يجب ألا تقل عن 6 أحرف.';
    }

    return message;
  }
}
