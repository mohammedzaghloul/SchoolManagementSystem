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
  classes: any[] = []; // Added for filter dropdown
  
  stats = {
    total: 0,
    active: 0,
    activePercent: 0,
    avgAttendance: 0,
    unpaidCount: 0
  };
  
  columns: TableColumn[] = [
    { field: 'fullName', title: 'الاسم الكامل', sortable: true },
    { field: 'classRoomName', title: 'الفصل', sortable: true },
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
        search: this.searchTerm
      };
      
      const response = await this.studentService.getStudents(params);
      this.students = response.items.map((s: any) => ({
        ...s,
        statusLabel: {
          text: s.isActive ? 'نشط' : 'غير نشط',
          class: s.isActive ? 'bg-success' : 'bg-danger'
        },
        attendanceRate: s.attendanceRate || 0
      }));
      this.totalItems = response.totalCount;
      
      // Calculate basic stats locally
      this.stats.total = response.totalCount;
      this.stats.active = this.students.filter(s => s.isActive).length;
      this.stats.activePercent = this.stats.total > 0 ? Math.round((this.stats.active / this.stats.total) * 100) : 0;
      
      const totalAttendance = this.students.reduce((sum, s) => sum + (s.attendanceRate || 0), 0);
      this.stats.avgAttendance = this.students.length > 0 ? Math.round(totalAttendance / this.students.length) : 0;
      
      // Unpaid is just a mockup for now as no field exists in model
      this.stats.unpaidCount = this.students.filter(s => !(s as any).isFeesPaid).length;

    } catch (error) {
      this.notification.error('حدث خطأ في تحميل البيانات');
    } finally {
      this.loading = false;
    }
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

  applyFilters(): void {
    this.currentPage = 1;
    this.loadStudents();
  }
}
