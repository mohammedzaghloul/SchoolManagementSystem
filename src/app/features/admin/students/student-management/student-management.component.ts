import { Component, OnDestroy, OnInit } from '@angular/core';
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
export class StudentManagementComponent implements OnInit, OnDestroy {
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
  selectedFaceFilter = 'all';
  selectedGrade: any = 'all';
  modalGradeId: number | null = null;

  // Pagination
  readonly pageSize = 10;
  currentPage = 1;

  // Modal State
  showModal = false;
  isEditMode = false;
  currentStudent: any = this.createEmptyStudent();

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
    // Ensure filters are applied after all metadata (like gradeLevels) is loaded
    this.applyFilter();
  }

  ngOnDestroy(): void {
    document.body.classList.remove('modal-open-fix');
  }

  // ─── Stats ────────────────────────────────────────────
  get activeCount(): number { return this.students.filter(s => s.isActive).length; }
  get inactiveCount(): number { return this.students.filter(s => !s.isActive).length; }
  get withParentCount(): number { return this.students.filter(s => s.parentId).length; }

  // ─── Pagination ───────────────────────────────────────
  get totalPages(): number { return Math.ceil(this.filteredStudents.length / this.pageSize); }

  get pagedStudents(): any[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredStudents.slice(start, start + this.pageSize);
  }

  get pageNumbers(): (number | string)[] {
    const total = this.totalPages;
    const current = this.currentPage;
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
    const pages: (number | string)[] = [1];
    if (current > 3) pages.push('...');
    for (let i = Math.max(2, current - 1); i <= Math.min(total - 1, current + 1); i++) pages.push(i);
    if (current < total - 2) pages.push('...');
    pages.push(total);
    return pages;
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
  }

  // ─── Avatar Helpers ───────────────────────────────────
  getInitials(name: string): string {
    if (!name) return '?';
    const parts = name.trim().split(' ');
    return parts.length >= 2
      ? (parts[0][0] + parts[1][0]).toUpperCase()
      : parts[0].substring(0, 2).toUpperCase();
  }

  getAvatarColor(name: string): string {
    const colors = [
      '#6366f1', '#8b5cf6', '#ec4899', '#f43f5e',
      '#f97316', '#eab308', '#22c55e', '#14b8a6',
      '#3b82f6', '#06b6d4'
    ];
    if (!name) return colors[0];
    const index = name.charCodeAt(0) % colors.length;
    return colors[index];
  }

  // ─── Data Loading ─────────────────────────────────────
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

  // ─── Filtering ────────────────────────────────────────
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
    if (this.selectedGrade !== 'all') {
      const selectedGradeObj = this.gradeLevels.find(g => g.id == this.selectedGrade);
      const gradeName = selectedGradeObj ? selectedGradeObj.name : null;
      
      list = list.filter(s => 
        s.gradeId == this.selectedGrade ||
        s.gradeLevelId == this.selectedGrade ||
        s.gradeName == this.selectedGrade ||
        s.gradeLevelName == this.selectedGrade ||
        (gradeName && (s.gradeName == gradeName || s.gradeLevelName == gradeName))
      );
    }
    if (this.selectedFaceFilter !== 'all') {
      list = list.filter(s => this.selectedFaceFilter === 'trained' ? !!s.isFaceTrained : !s.isFaceTrained);
    }
    this.filteredStudents = list;
    this.currentPage = 1;
  }

  // Count students per grade
  getGradeCount(gradeId: any): number {
    if (gradeId === 'all') return this.students.length;
    
    const gradeObj = this.gradeLevels.find(g => g.id == gradeId);
    const gradeName = gradeObj ? gradeObj.name : null;

    return this.students.filter(s => 
      s.gradeId == gradeId ||
      s.gradeLevelId == gradeId ||
      s.gradeName == gradeId ||
      s.gradeLevelName == gradeId ||
      (gradeName && (s.gradeName == gradeName || s.gradeLevelName == gradeName))
    ).length;
  }

  selectGrade(gradeId: any): void {
    this.selectedGrade = gradeId;
    this.applyFilter();
  }

  // ─── Modal ────────────────────────────────────────────
  openAddModal() {
    this.isEditMode = false;
    this.currentStudent = this.createEmptyStudent();
    this.modalGradeId = this.selectedGrade !== 'all' ? Number(this.selectedGrade) : null;
    this.showModal = true;
    document.body.classList.add('modal-open-fix');
    this.resetModalScroll();
  }

  openEditModal(student: any) {
    this.isEditMode = true;
    const classRoom = this.classRooms.find(c => c.id === student.classRoomId);
    this.modalGradeId = classRoom ? this.getClassRoomGradeId(classRoom) : null;

    this.currentStudent = {
      ...student,
      classRoomId: student.classRoomId ?? null,
      parentId: student.parentId ?? null
    };
    this.showModal = true;
    document.body.classList.add('modal-open-fix');
    this.resetModalScroll();
  }

  onModalGradeChange(): void {
    this.currentStudent.classRoomId = null;
  }

  getClassRoomsForModalGrade(): any[] {
    if (!this.modalGradeId) return this.classRooms;
    return this.classRooms.filter(c => this.getClassRoomGradeId(c) == this.modalGradeId);
  }

  getClassRoomLabel(classRoom: any): string {
    const gradeId = this.getClassRoomGradeId(classRoom);
    const gradeName = classRoom.gradeLevelName ||
      classRoom.gradeName ||
      this.gradeLevels.find(g => g.id == gradeId)?.name;

    return gradeName ? `${classRoom.name} - ${gradeName}` : classRoom.name;
  }

  private getClassRoomGradeId(classRoom: any): number | null {
    return classRoom?.gradeLevelId ??
      classRoom?.gradeId ??
      classRoom?.gradeLevel?.id ??
      classRoom?.grade?.id ??
      null;
  }

  closeModal() { 
    this.showModal = false; 
    document.body.classList.remove('modal-open-fix');
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
        const updatePayload = {
          id: payload.id,
          fullName: payload.fullName,
          email: payload.email,
          phone: payload.phone,
          address: payload.address,
          dateOfBirth: payload.dateOfBirth,
          gender: payload.gender,
          classRoomId: payload.classRoomId,
          parentId: payload.parentId,
          isActive: payload.isActive
        };
        await this.studentService.updateStudent(this.currentStudent.id, updatePayload);
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

  // ─── Delete ───────────────────────────────────────────
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
          this.applyFilter(); // Crucial: update UI
        } catch (err: any) {
          alert('حدث خطأ في الحذف: تأكد من الارتباطات ببيانات أخرى أو حاول مرة أخرى.');
        }
      }
    });
  }

  // ─── Toggle Status ────────────────────────────────────
  async toggleStudentStatus(student: any) {
    try {
      const result = await this.studentService.toggleStatus(student.id);
      student.isActive = result.isActive;
      this.applyFilter();
    } catch {
      alert('خطأ في تغيير حالة الطالب');
    }
  }

  private createEmptyStudent(): any {
    return {
      fullName: '',
      email: '',
      phone: '',
      password: '',
      classRoomId: null,
      parentId: null,
      isActive: true
    };
  }

  private resetModalScroll(): void {
    setTimeout(() => {
      document.querySelector<HTMLElement>('.student-modal-body')?.scrollTo({ top: 0 });
    });
  }
}
