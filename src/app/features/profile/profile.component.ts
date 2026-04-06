import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';
import { UserService } from '../../core/services/user.service';
import { NotificationService } from '../../core/services/notification.service';
import { User } from '../../core/models/user.model';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.css']
})
export class ProfileComponent implements OnInit {
  user: User | null = null;
  isEditing = false;
  editForm: FormGroup;
  passwordForm: FormGroup;
  showPasswordForm = false;
  imagePreview: string | null = null;
  showPass1 = true;
  showPass2 = false;
  showPass3 = false;

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private userService: UserService,
    private notification: NotificationService
  ) {
    this.editForm = this.fb.group({
      fullName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      phone: [''],
      address: ['']
    });

    this.passwordForm = this.fb.group({
      currentPassword: ['12345678', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', Validators.required]
    }, { validator: this.passwordMatchValidator });
  }

  ngOnInit() {
    this.loadUserProfile();
  }

  async loadUserProfile(): Promise<void> {
    this.user = await this.userService.getCurrentUser();
    if (this.user) {
        this.editForm.patchValue({
          fullName: this.user.fullName,
          email: this.user.email,
          phone: this.user.phone,
          address: this.user.address
        });
    }
  }

  passwordMatchValidator(group: FormGroup): any {
    const newPass = group.get('newPassword')?.value;
    const confirmPass = group.get('confirmPassword')?.value;
    return newPass === confirmPass ? null : { mismatch: true };
  }

  onImageSelected(event: any): void {
    const file = event.target.files[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = () => {
        this.imagePreview = reader.result as string;
      };
      reader.readAsDataURL(file);
      
      this.userService.uploadProfileImage(file).then(() => {
        this.notification.success('تم تحديث الصورة الشخصية');
      }).catch(() => {
        this.notification.error('حدث خطأ في تحديث الصورة');
      });
    }
  }

  async saveProfile(): Promise<void> {
    if (this.editForm.invalid) return;
    
    try {
      await this.userService.updateProfile(this.editForm.value);
      this.notification.success('تم تحديث الملف الشخصي بنجاح');
      this.isEditing = false;
      this.loadUserProfile();
    } catch (error) {
      this.notification.error('حدث خطأ في تحديث الملف الشخصي');
    }
  }

  async changePassword(): Promise<void> {
    if (this.passwordForm.invalid) {
      this.notification.warning('يرجى إدخال كلمة المرور بشكل صحيح');
      return;
    }
    
    try {
      await this.auth.changePassword(this.passwordForm.value);
      this.notification.success('تم تغيير كلمة المرور بنجاح');
      this.showPasswordForm = false;
      this.passwordForm.reset();
    } catch (error) {
      this.notification.error('حدث خطأ في تغيير كلمة المرور');
    }
  }
}
