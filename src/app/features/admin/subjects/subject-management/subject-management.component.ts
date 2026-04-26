import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';

import { ClassRoom } from '../../../../core/models/class.model';
import { Subject } from '../../../../core/models/subject.model';
import { ClassRoomService } from '../../../../core/services/classroom.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { SubjectService } from '../../../../core/services/subject.service';
import {
  ConfirmDialogComponent,
  ConfirmDialogData
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-subject-management',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule],
  templateUrl: './subject-management.component.html',
  styleUrls: ['./subject-management.component.css']
})
export class SubjectManagementComponent implements OnInit, OnDestroy {
  subjects: Subject[] = [];
  filteredSubjects: Subject[] = [];
  classRooms: ClassRoom[] = [];

  loading = false;
  submitting = false;
  showModal = false;
  isEditMode = false;

  searchTerm = '';
  selectedTerm = 'all';
  selectedClassRoomId: number | 'all' | 'general' = 'all';
  selectedGradeLevelId: number | 'all' = 'all';
  selectedStatus = 'all';
  modalGradeId: number | null = null;
  page = 1;
  pageSize = 9;

  readonly terms = ['الترم الأول', 'الترم الثاني'];

  currentSubject: any = this.createEmptySubject();

  constructor(
    private subjectService: SubjectService,
    private classRoomService: ClassRoomService,
    private notificationService: NotificationService,
    private dialog: MatDialog
  ) {}

  async ngOnInit(): Promise<void> {
    await Promise.all([
      this.loadSubjects(),
      this.loadClassRooms()
    ]);
  }

  ngOnDestroy(): void {
    document.body.classList.remove('modal-open-fix');
  }

  get pagedSubjects(): Subject[] {
    const start = (this.page - 1) * this.pageSize;
    return this.filteredSubjects.slice(start, start + this.pageSize);
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filteredSubjects.length / this.pageSize));
  }

  get selectedClassLabel(): string {
    if (this.selectedClassRoomId === 'all' && this.selectedGradeLevelId === 'all') return 'كل الفصول';
    if (this.selectedClassRoomId === 'general') return 'مواد عامة';
    if (this.selectedGradeLevelId !== 'all') {
      const gl = this.gradeLevels.find(g => g.id == this.selectedGradeLevelId);
      return gl ? gl.name : 'سنة محددة';
    }
    return this.getClassRoomName(this.selectedClassRoomId as number);
  }

  get gradeLevels(): { id: number; name: string }[] {
    const seen = new Set<string>();
    const result: { id: number; name: string }[] = [];
    for (const c of this.classRooms) {
      if (c.gradeLevelId) {
        const name = c.gradeLevelName || `السنة ${c.gradeLevelId}`;
        if (!seen.has(name)) {
          seen.add(name);
          result.push({ id: c.gradeLevelId, name });
        }
      }
    }
    return result;
  }

  get classRoomsForGrade(): ClassRoom[] {
    if (this.selectedGradeLevelId === 'all') return this.classRooms;
    const filtered = this.classRooms.filter(c => c.gradeLevelId == this.selectedGradeLevelId);
    
    // Deduplicate by name
    const seen = new Set<string>();
    const unique: ClassRoom[] = [];
    for (const c of filtered) {
        if (!seen.has(c.name)) {
            seen.add(c.name);
            unique.push(c);
        }
    }
    return unique;
  }

  getGradeLevelSubjectCount(gradeLevelId: number | 'all'): number {
    if (gradeLevelId === 'all') return this.subjects.length;
    const ids = this.classRooms.filter(c => c.gradeLevelId == gradeLevelId).map(c => c.id);
    return this.subjects.filter(s => s.classRoomId != null && ids.includes(s.classRoomId as number)).length;
  }

  selectGradeLevel(id: number | 'all'): void {
    this.selectedGradeLevelId = id;
    this.selectedClassRoomId = 'all';
    this.applyFilter();
  }

  async loadClassRooms(): Promise<void> {
    try {
      this.classRooms = await this.classRoomService.getAll();
    } catch {
      this.classRooms = [];
      this.notificationService.warning('تعذر تحميل قائمة الفصول الآن.');
    }
  }

  async loadSubjects(): Promise<void> {
    this.loading = true;

    try {
      const data = await this.subjectService.getAll();
      this.subjects = data.map(subject => ({
        ...subject,
        term: this.normalizeTerm(subject.term),
        isActive: subject.isActive !== false
      }));
      this.applyFilter();
    } catch {
      this.subjects = [];
      this.filteredSubjects = [];
      this.notificationService.error('تعذر تحميل المواد الدراسية.');
    } finally {
      this.loading = false;
    }
  }

  applyFilter(): void {
    const search = this.searchTerm.trim().toLowerCase();
    const gradeClassIds = this.selectedGradeLevelId !== 'all'
      ? this.classRooms.filter(c => c.gradeLevelId == this.selectedGradeLevelId).map(c => c.id)
      : null;

    this.filteredSubjects = this.subjects.filter(subject => {
      const matchesGrade = !gradeClassIds || (subject.classRoomId != null && gradeClassIds.includes(subject.classRoomId as number));
      const matchesClass = this.selectedClassRoomId === 'all'
        || (this.selectedClassRoomId === 'general' && !subject.classRoomId)
        || subject.classRoomId === Number(this.selectedClassRoomId);
      const matchesTerm = this.selectedTerm === 'all'
        || this.normalizeTerm(subject.term) === this.selectedTerm;
      const matchesSearch = !search
        || subject.name.toLowerCase().includes(search)
        || (subject.code || '').toLowerCase().includes(search)
        || (subject.description || '').toLowerCase().includes(search);

      const matchesStatus = this.selectedStatus === 'all'
        || subject.isActive === (this.selectedStatus === 'active');

      return matchesGrade && matchesClass && matchesTerm && matchesSearch && matchesStatus;
    });
    this.page = 1;
  }

  openAddModal(): void {
    this.isEditMode = false;
    this.currentSubject = this.createEmptySubject();
    this.showModal = true;
    document.body.classList.add('modal-open-fix');
  }

  openEditModal(item: Subject): void {
    this.isEditMode = true;
    const classRoom = this.classRooms.find(c => c.id === item.classRoomId);
    this.modalGradeId = classRoom ? classRoom.gradeLevelId : null;
    
    this.currentSubject = {
      ...item,
      term: this.normalizeTerm(item.term),
      classRoomId: item.classRoomId ?? null,
      isActive: item.isActive !== false
    };
    this.showModal = true;
    document.body.classList.add('modal-open-fix');
  }

  getClassRoomsForModalGrade(): ClassRoom[] {
    if (!this.modalGradeId) return [];
    return this.classRooms.filter(c => c.gradeLevelId == this.modalGradeId);
  }

  closeModal(): void {
    this.showModal = false;
    this.currentSubject = this.createEmptySubject();
    document.body.classList.remove('modal-open-fix');
  }

  async saveSubject(): Promise<void> {
    if (!this.currentSubject.name?.trim() || !this.currentSubject.code?.trim()) {
      this.notificationService.warning('يرجى إدخال اسم المادة وكود المادة.');
      return;
    }

    this.submitting = true;

    try {
      const payload = {
        ...this.currentSubject,
        name: this.currentSubject.name.trim(),
        code: this.currentSubject.code.trim(),
        description: this.currentSubject.description?.trim() || null,
        classRoomId: this.currentSubject.classRoomId ? Number(this.currentSubject.classRoomId) : null,
        term: this.normalizeTerm(this.currentSubject.term),
        isActive: this.currentSubject.isActive !== false
      };

      if (this.isEditMode) {
        await this.subjectService.update(payload.id, payload);
        this.notificationService.success('تم تحديث المادة بنجاح.');
      } else {
        await this.subjectService.create(payload);
        this.notificationService.success('تم إضافة المادة بنجاح.');
      }

      await this.loadSubjects();
      this.closeModal();
    } catch (error: any) {
      this.notificationService.error(error?.message || 'تعذر حفظ المادة الآن.');
    } finally {
      this.submitting = false;
    }
  }

  deleteSubject(subject: Subject): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      direction: 'rtl',
      data: {
        title: 'تأكيد الحذف',
        message: `هل أنت متأكد من حذف مادة "${subject.name}"؟`,
        confirmText: 'حذف المادة',
        cancelText: 'إلغاء',
        color: 'warn'
      } as ConfirmDialogData
    });

    dialogRef.afterClosed().subscribe(async result => {
      if (!result) {
        return;
      }

      try {
        await this.subjectService.delete(subject.id);
        this.subjects = this.subjects.filter(item => item.id !== subject.id);
        this.applyFilter();
        this.notificationService.success('تم حذف المادة بنجاح.');
      } catch (error: any) {
        this.notificationService.error(error?.message || 'لا يمكن حذف هذه المادة لأنها مرتبطة ببيانات أخرى.');
      }
    });
  }

  getClassRoomName(id: number | undefined | null): string {
    if (id === undefined || id === null) {
      return 'مادة عامة';
    }

    const cls = this.classRooms.find(c => c.id === id);
    if (!cls) return 'فصل غير محدد';
    
    return cls.gradeLevelName ? `${cls.gradeLevelName} - ${cls.name}` : cls.name;
  }

  goToPage(nextPage: number): void {
    this.page = Math.min(Math.max(1, nextPage), this.totalPages);
  }

  private createEmptySubject(): any {
    const classRoomId = typeof this.selectedClassRoomId === 'number' ? this.selectedClassRoomId : null;

    return {
      id: null,
      name: '',
      code: '',
      description: '',
      classRoomId,
      term: 'الترم الأول',
      isActive: true
    };
  }

  private normalizeTerm(term?: string): string {
    if (!term || term.includes('أول') || term.toLowerCase().includes('first')) {
      return 'الترم الأول';
    }

    if (term.includes('ثاني') || term.toLowerCase().includes('second')) {
      return 'الترم الثاني';
    }

    return term;
  }
}
