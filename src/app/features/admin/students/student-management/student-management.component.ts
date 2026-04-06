import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { StudentService } from '../../../../core/services/student.service';
import { GradeService } from '../../../../core/services/grade.service';
import { ClassRoomService } from '../../../../core/services/classroom.service';
import { ParentService } from '../../../../core/services/parent.service';
import { Parent } from '../../../../core/models/parent.model';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-student-management',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, MatDialogModule],
  templateUrl: './student-management.component.html',
  styleUrls: ['./student-management.component.css']
})
export class StudentManagementComponent implements OnInit {
  students: any[] = [];
  filteredStudents: any[] = [];
  gradeLevels: any[] = [];
  classRooms: any[] = [];
  parents: Parent[] = [];
  loading = false;
  submitting = false;
  error = '';
  searchTerm = '';
  selectedStatus = 'all';
  selectedClassRoom: any = 'all';

  // Modal State
  showModal = false;
  isEditMode = false;
  currentStudent: any = {
    fullName: '',
    email: '',
    phone: '',
    password: 'Student@123', // Default for new students
    classRoomId: null,
    parentId: null,
    isActive: true
  };

  constructor(
    private studentService: StudentService,
    private gradeService: GradeService,
    private classRoomService: ClassRoomService,
    private parentService: ParentService,
    private dialog: MatDialog
  ) { }

  async ngOnInit() {
    await Promise.all([
      this.loadStudents(),
      this.loadMeta(),
      this.loadParents()
    ]);
  }

  async loadMeta() {
    try {
      this.gradeLevels = await this.gradeService.getGrades();
      this.classRooms = await this.classRoomService.getAll();
    } catch (err) {
      console.error('Meta load error', err);
    }
  }

  async loadStudents() {
    this.loading = true;
    this.error = '';
    try {
      const result = await this.studentService.getStudents();
      this.students = Array.isArray(result) ? result : (result as any)?.items || (result as any)?.data || [];
      this.applyFilter();
    } catch (err: any) {
      this.error = err?.message || 'حدث خطأ في تحميل بيانات الطلاب';
    } finally {
      this.loading = false;
    }
  }

  async loadParents() {
    try {
      this.parents = await this.parentService.getParents();
    } catch (err) {
      console.error('Parents load error', err);
    }
  }

  applyFilter() {
    let list = this.students;
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      list = list.filter(s =>
        (s.fullName || s.name || '').toLowerCase().includes(term) ||
        (s.email || '').toLowerCase().includes(term)
      );
    }
    if (this.selectedStatus !== 'all') {
      list = list.filter(s => s.isActive === (this.selectedStatus === 'active'));
    }
    if (this.selectedClassRoom !== 'all') {
      list = list.filter(s => s.classRoomId == this.selectedClassRoom);
    }
    this.filteredStudents = list;
  }

  openAddModal() {
    this.isEditMode = false;
    this.currentStudent = {
      fullName: '',
      email: '',
      phone: '',
      password: 'Student@123',
      classRoomId: null,
      parentId: null,
      isActive: true
    };
    this.showModal = true;
  }

  openEditModal(student: any) {
    this.isEditMode = true;
    this.currentStudent = {
      ...student,
      classRoomId: student.classRoomId ?? null,
      parentId: student.parentId ?? null
    };
    this.showModal = true;
  }

  closeModal() {
    this.showModal = false;
  }

  async saveStudent() {
    if (!this.currentStudent.fullName || !this.currentStudent.email) {
      alert('يرجى إدخال اسم الطالب والبريد الإلكتروني');
      return;
    }
    
    this.submitting = true;
    try {
      const payload = { ...this.currentStudent };
      payload.classRoomId = payload.classRoomId || null;
      payload.parentId = payload.parentId || null;
      
      if (this.isEditMode) {
        await this.studentService.updateStudent(this.currentStudent.id, payload);
      } else {
        await this.studentService.createStudent(payload);
      }
      await this.loadStudents();
      this.closeModal();
    } catch (err: any) {
      alert('خطأ في الحفظ: ' + (err.error?.message || err.message));
    } finally {
      this.submitting = false;
    }
  }

  deleteStudent(id: number) {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'تأكيد الحذف',
        message: 'هل أنت متأكد من حذف هذا الطالب؟',
        confirmText: 'حذف',
        cancelText: 'إلغاء',
        color: 'warn'
      } as ConfirmDialogData
    });

    dialogRef.afterClosed().subscribe(async (result) => {
      if (result) {
        try {
          await this.studentService.deleteStudent(id);
          this.students = this.students.filter(s => s.id !== id);
          this.applyFilter();
        } catch (err: any) {
          alert('حدث خطأ في الحذف: تأكد من الصلاحيات أو محاولة مرة أخرى.');
        }
      }
    });
  }

  async toggleStudentStatus(student: any) {
    try {
      const result = await this.studentService.toggleStatus(student.id);
      student.isActive = result.isActive;
      this.applyFilter();
    } catch {
      alert('خطأ في تغيير حالة الطالب');
    }
  }
}
