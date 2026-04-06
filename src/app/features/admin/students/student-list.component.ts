import { Component, OnInit, ViewChild, TemplateRef } from '@angular/core';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { Student } from '../../../core/models/student.model';
import { StudentService } from '../../../core/services/student.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TableColumn, SortEvent } from '../../../shared/components/table/smart-table.component';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/components/confirm-dialog/confirm-dialog.component';

// features/admin/students/student-list/student-list.component.ts
@Component({
  selector: 'app-student-list',
  templateUrl: './student-list.component.html',
  styleUrls: ['./student-list.component.css']
})
export class StudentListComponent implements OnInit {
  @ViewChild('attendanceRateTemplate') attendanceRateTemplate!: TemplateRef<any>;
  
  students: Student[] = [];
  loading = false;
  totalItems = 0;
  pageSize = 10;
  currentPage = 1;
  searchTerm = '';
  
  columns: TableColumn[] = [
    { field: 'id', title: 'المعرف', sortable: true },
    { field: 'fullName', title: 'الاسم الكامل', sortable: true },
    { field: 'email', title: 'البريد الإلكتروني', sortable: true },
    { field: 'classRoomName', title: 'الفصل', sortable: true },
    { field: 'parentName', title: 'ولي الأمر', sortable: true },
    { 
      field: 'attendanceRate', 
      title: 'نسبة الحضور', 
      type: 'badge',
      template: this.attendanceRateTemplate 
    },
    { field: 'statusLabel', title: 'الحالة', type: 'badge' }
  ];

  constructor(
    private studentService: StudentService,
    private notification: NotificationService,
    private router: Router,
    private dialog: MatDialog
  ) {}

  ngOnInit() {
    this.loadStudents();
  }

  async loadStudents(): Promise<void> {
    this.loading = true;
    try {
      const params = {
        pageIndex: this.currentPage,
        pageSize: this.pageSize,
        search: this.searchTerm,
        sort: this.getSortParam()
      };
      
      const response = await this.studentService.getStudents(params);
      this.students = response.items.map((s: any) => ({
        ...s,
        statusLabel: {
          text: s.isActive ? 'نشط' : 'غير نشط',
          class: s.isActive ? 'bg-success' : 'bg-danger'
        },
        attendanceRate: s.attendanceRate || 0 // Default to 0 if missing
      }));
      this.totalItems = response.totalCount;
    } catch (error) {
      this.notification.error('حدث خطأ في تحميل البيانات');
    } finally {
      this.loading = false;
    }
  }

  getSortParam(): string {
    // Implement sort logic
    return '';
  }

  onPageChange(page: number): void {
    this.currentPage = page;
    this.loadStudents();
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.currentPage = 1;
    this.loadStudents();
  }

  onSort(event: SortEvent): void {
    // Implement sort
    this.loadStudents();
  }

  onAddStudent(): void {
    this.router.navigate(['/admin/students/add']);
  }

  onEditStudent(student: Student): void {
    this.router.navigate(['/admin/students/edit', student.id]);
  }

  onDeleteStudent(student: Student): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'تأكيد الحذف',
        message: `هل أنت متأكد من حذف الطالب ${student.fullName}؟`,
        confirmText: 'حذف',
        cancelText: 'إلغاء',
        color: 'warn'
      } as ConfirmDialogData
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.executeDelete(student.id);
      }
    });
  }

  private async executeDelete(id: number): Promise<void> {
    try {
      this.loading = true;
      await this.studentService.deleteStudent(id);
      this.notification.success('تم حذف الطالب بنجاح');
      await this.loadStudents();
    } catch (error) {
      console.error('Delete error:', error);
      this.notification.error('حدث خطأ في حذف الطالب');
    } finally {
      this.loading = false;
    }
  }

  onViewStudent(student: Student): void {
    this.router.navigate(['/admin/students', student.id]);
  }

  onTrainFace(student: Student): void {
    this.router.navigate(['/admin/students/train-face', student.id]);
  }
}
