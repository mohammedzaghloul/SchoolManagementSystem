import { Component, OnDestroy, OnInit } from '@angular/core';
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
export class TeacherManagementComponent implements OnInit, OnDestroy {
  teachers: any[] = [];
  filteredTeachers: any[] = [];
  subjects: any[] = [];
  classRooms: any[] = [];
  loading = false;
  submitting = false;
  error = '';
  searchTerm = '';
  selectedSubject = 'all';
  selectedStatus = 'all';

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
    this.loading = true;
    try {
      await this.loadMeta();
      await this.loadTeachers();
    } finally {
      this.loading = false;
    }
  }

  ngOnDestroy(): void {
    document.body.classList.remove('modal-open-fix');
  }

  async loadMeta() {
    try {
      const allSubjects = await this.subjectService.getAll();
      const uniqueSubjects = [];
      const seenNames = new Set();
      for (const s of allSubjects) {
          if (!seenNames.has(s.name)) {
              seenNames.add(s.name);
              uniqueSubjects.push(s);
          }
      }
      this.subjects = uniqueSubjects;
      
      this.classRooms = await this.classRoomService.getAll();
    } catch (err) {
      console.error('Meta load error', err);
    }
  }

  async loadTeachers() {
    this.loading = true;
    this.error = '';
    try {
      const result = await this.teacherService.getTeachers();
      this.teachers = result.map(t => {
        if (!t.subjectName && t.subjectId) {
          const sub = this.subjects.find(s => s.id == t.subjectId);
          if (sub) t.subjectName = sub.name;
        }
        return t;
      });
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

    if (this.selectedStatus !== 'all') {
      filtered = filtered.filter(t => t.isActive === (this.selectedStatus === 'active'));
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
    document.body.classList.add('modal-open-fix');
  }

  openEditModal(teacher: any) {
    this.isEditMode = true;
    this.currentTeacher = { ...teacher };
    this.showModal = true;
    document.body.classList.add('modal-open-fix');
  }

  closeModal() {
    this.showModal = false;
    document.body.classList.remove('modal-open-fix');
  }

  async saveTeacher() {
    if (!this.currentTeacher.fullName || !this.currentTeacher.email) {
      alert('يرجى إدخال اسم المدرس والبريد الإلكتروني');
      return;
    }

    this.submitting = true;
    try {
      const payload = { ...this.currentTeacher };
      
      if (payload.subjectId === 'null' || payload.subjectId === '') {
        delete payload.subjectId; 
      } else if (payload.subjectId !== null && payload.subjectId !== undefined) {
        payload.subjectId = Number(payload.subjectId);
      }

      if (this.isEditMode) {
        const updatePayload = {
          id: payload.id,
          fullName: payload.fullName,
          email: payload.email,
          phone: payload.phone,
          subjectId: payload.subjectId,
          isActive: payload.isActive
        };
        await this.teacherService.updateTeacher(this.currentTeacher.id, updatePayload);
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
              const message = err?.message || err?.error?.message;
              if (message) {
                alert(message);
                return;
              }
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
