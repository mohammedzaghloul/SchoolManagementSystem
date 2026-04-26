import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  GradeService,
  TeacherGradebookResponse,
  TeacherGradebookStudent
} from '../../../core/services/grade.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ClassRoomService } from '../../../core/services/classroom.service';
import { SubjectService } from '../../../core/services/subject.service';
import { AuthService } from '../../../core/services/auth.service';
import { ClassRoom } from '../../../core/models/class.model';
import { Subject } from '../../../core/models/subject.model';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { ModalComponent } from '../../../shared/components/modal/modal.component';

interface EditableGradeStudent extends TeacherGradebookStudent {
  score: number | null;
  notes: string;
}

@Component({
  selector: 'app-teacher-grades',
  standalone: true,
  imports: [CommonModule, FormsModule, PaginatorComponent, ModalComponent],
  templateUrl: './teacher-grades.component.html',
  styleUrls: ['./teacher-grades.component.css']
})
export class TeacherGradesComponent implements OnInit {
  readonly gradeTypes = ['واجب', 'اختبار', 'مشاركة', 'شفوي', 'تقييم'];

  classes: ClassRoom[] = [];
  subjects: Subject[] = [];
  students: EditableGradeStudent[] = [];
  gradebook: TeacherGradebookResponse | null = null;

  selectedClassRoomId: number | null = null;
  selectedSubjectId: number | null = null;
  selectedGradeType = 'واجب';
  selectedDate = this.getDateInputValue(new Date());
  searchTerm = '';

  loadingSubjects = false;
  loadingGradebook = false;
  saving = false;
  loadErrorMessage = '';
  usingFallbackData = false;

  showSuccessModal = false;
  successModalMessage = '';

  currentPage = 1;
  pageSize = 100;

  constructor(
    private classRoomService: ClassRoomService,
    private subjectService: SubjectService,
    private gradeService: GradeService,
    private notify: NotificationService,
    private authService: AuthService
  ) {}

  async ngOnInit(): Promise<void> {
    await this.loadSubjects();
  }

  get selectedSubject(): Subject | undefined {
    return this.subjects.find(subject => subject.id === this.selectedSubjectId);
  }

  get availableClasses(): ClassRoom[] {
    const subjectClassIds = new Set(
      this.subjects
        .map(subject => Number(subject.classRoomId || 0))
        .filter(classRoomId => classRoomId > 0)
    );

    if (!subjectClassIds.size) {
      return this.classes;
    }

    const classesById = new Map(this.classes.map(classRoom => [Number(classRoom.id), classRoom]));

    return Array.from(subjectClassIds)
      .map(classRoomId => classesById.get(classRoomId) || {
        id: classRoomId,
        name: `الفصل ${classRoomId}`,
        gradeLevelId: 0
      })
      .sort((first, second) => first.name.localeCompare(second.name, 'ar'));
  }

  get filteredSubjects(): Subject[] {
    const classRoomId = Number(this.selectedClassRoomId || 0);
    const subjects = classRoomId
      ? this.subjects.filter(subject => Number(subject.classRoomId || 0) === classRoomId)
      : this.subjects.filter(subject => !subject.classRoomId);

    return this.getUniqueSubjects(subjects);
  }

  get filteredStudents(): EditableGradeStudent[] {
    const search = this.searchTerm.trim().toLowerCase();

    return this.students.filter(student =>
      !search ||
      student.fullName.toLowerCase().includes(search) ||
      (student.email || '').toLowerCase().includes(search)
    );
  }

  get totalStudents(): number {
    return this.students.length;
  }

  get paginatedStudents(): EditableGradeStudent[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredStudents.slice(start, start + this.pageSize);
  }

  onPageChange(page: number) {
    this.currentPage = page;
  }

  onFilterChange() {
    this.currentPage = 1;
  }

  get enteredGradesCount(): number {
    return this.students.filter(student => student.score !== null).length;
  }

  get pendingGradesCount(): number {
    return Math.max(this.totalStudents - this.enteredGradesCount, 0);
  }

  get averageScore(): number {
    const scoredStudents = this.students.filter(student => student.score !== null);
    if (!scoredStudents.length) {
      return 0;
    }

    const total = scoredStudents.reduce((sum, student) => sum + Number(student.score || 0), 0);
    return Math.round(total / scoredStudents.length);
  }

  async loadSubjects(): Promise<void> {
    this.loadingSubjects = true;
    this.loadErrorMessage = '';

    try {
      const [classes, subjects] = await Promise.all([
        this.loadTeacherClasses(),
        this.loadTeacherSubjects()
      ]);

      this.classes = classes;
      this.subjects = subjects.filter(subject => subject.isActive !== false);

      if (!this.selectedClassRoomId && this.availableClasses.length > 0) {
        this.selectedClassRoomId = this.availableClasses[0].id;
      }

      if (!this.selectedSubjectId && this.filteredSubjects.length > 0) {
        this.selectedSubjectId = this.filteredSubjects[0].id;
      }

      if (this.selectedSubjectId) {
        await this.loadGradebook();
      }
    } catch (error: any) {
      this.loadErrorMessage = error?.message || 'تعذر تحميل مواد المدرس.';
      this.notify.error(this.loadErrorMessage);
    } finally {
      this.loadingSubjects = false;
    }
  }

  async onSelectionChanged(): Promise<void> {
    await this.loadGradebook();
  }

  async onClassRoomChanged(): Promise<void> {
    this.selectedSubjectId = this.filteredSubjects[0]?.id || null;
    await this.loadGradebook();
  }

  async loadGradebook(): Promise<void> {
    if (!this.selectedSubjectId) {
      this.gradebook = null;
      this.students = [];
      this.loadErrorMessage = '';
      this.usingFallbackData = false;
      return;
    }

    this.currentPage = 1;
    this.loadingGradebook = true;
    this.loadErrorMessage = '';
    this.usingFallbackData = false;

    try {
      const response = await this.gradeService.getTeacherGradebook(
        this.selectedSubjectId,
        this.selectedGradeType,
        this.selectedDate
      );

      this.gradebook = response;
      this.students = (response.students || []).map(student => ({
        ...student,
        score: student.score ?? null,
        notes: student.notes || ''
      }));
    } catch (error: any) {
      this.gradebook = null;
      this.students = [];
      this.loadErrorMessage = this.getGradebookErrorMessage(error);
      this.notify.error(this.loadErrorMessage);
    } finally {
      this.loadingGradebook = false;
    }
  }

  updateScore(student: EditableGradeStudent, value: string | number | null): void {
    if (value === null || value === '' || typeof value === 'undefined') {
      student.score = null;
      return;
    }

    const parsed = Number(value);
    if (Number.isNaN(parsed)) {
      student.score = null;
      return;
    }

    student.score = Math.min(100, Math.max(0, parsed));
  }

  async saveGrades(): Promise<void> {
    if (!this.selectedClassRoomId || !this.selectedSubjectId) {
      this.notify.warning('اختر الفصل والمادة أولًا.');
      return;
    }

    const grades = this.students
      .filter(student => student.score !== null)
      .map(student => ({
        id: student.existingGradeId ?? null,
        studentId: student.id,
        score: Number(student.score),
        notes: student.notes?.trim() || null
      }));

    if (!grades.length) {
      this.notify.warning('أدخل درجة واحدة على الأقل قبل الحفظ.');
      return;
    }

    this.saving = true;

    try {
      if (this.usingFallbackData) {
        this.notify.info('تم تحميل الطلاب من الفصل مباشرة. إذا ظهر خطأ في الحفظ فحدّث الـbackend الحالي ثم أعد المحاولة.');
      }

      const result = await this.gradeService.saveTeacherGradebook({
        subjectId: this.selectedSubjectId,
        gradeType: this.selectedGradeType,
        date: this.selectedDate,
        grades
      });

      this.notify.success(result.message || 'تم حفظ الدرجات بنجاح.');
      this.successModalMessage = result.message || 'تم حفظ جميع التعديلات في قاعدة البيانات بنجاح.';
      this.showSuccessModal = true;

      this.loadErrorMessage = '';
      await this.loadGradebook();
    } catch (error: any) {
      this.notify.error(error?.message || 'حدث خطأ أثناء حفظ الدرجات.');
    } finally {
      this.saving = false;
    }
  }

  trackByStudent(_: number, student: EditableGradeStudent): number {
    return student.id;
  }

  private getDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private async loadTeacherClasses(): Promise<ClassRoom[]> {
    try {
      const classes = await this.classRoomService.getTeacherClasses(this.getNumericTeacherId());
      return this.normalizeList<ClassRoom>(classes);
    } catch {
      return [];
    }
  }

  private async loadTeacherSubjects(): Promise<Subject[]> {
    try {
      const subjects = this.normalizeList(await this.subjectService.getTeacherSubjects(this.getNumericTeacherId()));
      return subjects;
    } catch {
      return [];
    }
  }

  private getNumericTeacherId(): number | undefined {
    const id = Number(this.authService.getCurrentUser()?.id);
    return Number.isFinite(id) && id > 0 ? id : undefined;
  }

  private normalizeList<T>(value: T[] | { data?: T[] } | null | undefined): T[] {
    if (Array.isArray(value)) {
      return value;
    }

    if (value && Array.isArray(value.data)) {
      return value.data;
    }

    return [];
  }

  private getUniqueSubjects(subjects: Subject[]): Subject[] {
    const seen = new Set<string>();

    return subjects.filter(subject => {
      const key = `${Number(subject.classRoomId || this.selectedClassRoomId || 0)}-${this.normalizeSubjectName(subject.name)}`;
      if (seen.has(key)) {
        return false;
      }

      seen.add(key);
      return true;
    });
  }

  private normalizeSubjectName(name: string | undefined): string {
    return (name || '').trim().replace(/\s+/g, ' ').toLowerCase();
  }

  private getGradebookErrorMessage(error: any): string {
    if (error?.status === 404) {
      return 'خدمة كشف درجات المدرس غير متاحة في الخادم الحالي. أعد تشغيل الـbackend ثم جرّب مرة أخرى.';
    }

    return error?.message || 'تعذر تحميل كشف الدرجات.';
  }
}
