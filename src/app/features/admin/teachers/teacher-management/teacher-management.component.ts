import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TeacherService } from '../../../../core/services/teacher.service';
import { SubjectService } from '../../../../core/services/subject.service';
import { ClassRoomService } from '../../../../core/services/classroom.service';

import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-teacher-management',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, MatDialogModule],
  templateUrl: './teacher-management.component.html',
  styleUrls: ['./teacher-management.component.css']
})
export class TeacherManagementComponent implements OnInit {
  teachers: any[] = [];
  filteredTeachers: any[] = [];
  subjects: any[] = [];
  classRooms: any[] = [];
  loading = false;
  submitting = false;
  error = '';
  searchTerm = '';
  selectedSubject = 'all';

  // Modal State
  showModal = false;
  isEditMode = false;
  currentTeacher: any = {
    fullName: '',
    email: '',
    phone: '',
    password: 'Teacher@123',
    subjectId: null,
    isActive: true
  };

  constructor(
    private teacherService: TeacherService,
    private subjectService: SubjectService,
    private classRoomService: ClassRoomService,
    private dialog: MatDialog
  ) { }

  async ngOnInit() {
    await Promise.all([
      this.loadTeachers(),
      this.loadMeta()
    ]);
  }

  async loadMeta() {
    try {
      this.subjects = await this.subjectService.getAll();
      this.classRooms = await this.classRoomService.getAll();
    } catch (err) {
      console.error('Meta load error', err);
    }
  }

  async loadTeachers() {
    this.loading = true;
    this.error = '';
    try {
      this.teachers = await this.teacherService.getTeachers();
      this.applyFilter();
    } catch (err: any) {
      this.error = err?.message || 'حدث خطأ في تحميل بيانات المدرسين';
    } finally {
      this.loading = false;
    }
  }

  applyFilter() {
    let filtered = [...this.teachers];

    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(t =>
        (t.fullName || '').toLowerCase().includes(term) ||
        (t.email || '').toLowerCase().includes(term)
      );
    }

    if (this.selectedSubject !== 'all') {
      filtered = filtered.filter(t => t.subjectId == this.selectedSubject);
    }

    this.filteredTeachers = filtered;
  }

  openAddModal() {
    this.isEditMode = false;
    this.currentTeacher = {
      fullName: '',
      email: '',
      phone: '',
      password: 'Teacher@123',
      subjectId: null,
      isActive: true
    };
    this.showModal = true;
  }

  openEditModal(teacher: any) {
    this.isEditMode = true;
    this.currentTeacher = { ...teacher };
    this.showModal = true;
  }

  closeModal() {
    this.showModal = false;
  }

  async saveTeacher() {
    if (!this.currentTeacher.fullName || !this.currentTeacher.email) {
      alert('يرجى إدخال اسم المدرس والبريد الإلكتروني');
      return;
    }

    this.submitting = true;
    try {
      const payload = { ...this.currentTeacher };
      
      // Fix: API requires subjectId as a valid number, and it might come from template as a string
      if (payload.subjectId === 'null' || payload.subjectId === '') {
        delete payload.subjectId; 
      } else if (payload.subjectId !== null && payload.subjectId !== undefined) {
        payload.subjectId = Number(payload.subjectId);
      }

      if (this.isEditMode) {
        await this.teacherService.updateTeacher(this.currentTeacher.id, payload);
      } else {
        await this.teacherService.createTeacher(payload);
      }
      await this.loadTeachers();
      this.closeModal();
    } catch (err: any) {
      alert('خطأ في الحفظ: ' + (err.error?.message || err.message));
    } finally {
      this.submitting = false;
    }
  }

  deleteTeacher(id: number) {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
        width: '400px',
        data: {
            title: 'تأكيد الحذف',
            message: 'هل أنت متأكد من حذف هذا المعلم؟ سيتم حذف كافة الجداول والروابط المرتبطة به.',
            confirmText: 'حذف المعلم',
            cancelText: 'إلغاء',
            color: 'warn'
        } as ConfirmDialogData
    });

    dialogRef.afterClosed().subscribe(async (result) => {
        if (result) {
            try {
              await this.teacherService.deleteTeacher(id);
              this.teachers = this.teachers.filter(t => t.id !== id);
              this.applyFilter();
            } catch (err: any) {
              alert('عفواً، لا يمكن حذف المعلم لأنه مرتبط بمواد أو جداول دراسية. يجب حذف الارتباط أولاً.');
            }
        }
    });
  }
  async toggleTeacherStatus(teacher: any) {
    try {
      const result = await this.teacherService.toggleStatus(teacher.id);
      teacher.isActive = result.isActive;
    } catch {
      alert('خطأ في تغيير حالة المدرس');
    }
  }
}
