import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  GradeService,
  TeacherGradebookResponse,
  TeacherGradebookStudent
} from '../../../core/services/grade.service';
import { NotificationService } from '../../../core/services/notification.service';
import { StudentService } from '../../../core/services/student.service';
import { SubjectService } from '../../../core/services/subject.service';
import { Subject } from '../../../core/models/subject.model';

interface EditableGradeStudent extends TeacherGradebookStudent {
  score: number | null;
  notes: string;
}

@Component({
  selector: 'app-teacher-grades',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './teacher-grades.component.html',
  styleUrls: ['./teacher-grades.component.css']
})
export class TeacherGradesComponent implements OnInit {
  readonly gradeTypes = ['واجب', 'اختبار', 'مشاركة', 'شفوي', 'تقييم'];

  subjects: Subject[] = [];
  students: EditableGradeStudent[] = [];
  gradebook: TeacherGradebookResponse | null = null;

  selectedSubjectId: number | null = null;
  selectedGradeType = 'واجب';
  selectedDate = this.getDateInputValue(new Date());
  searchTerm = '';

  loadingSubjects = false;
  loadingGradebook = false;
  saving = false;
  loadErrorMessage = '';
  usingFallbackData = false;

  constructor(
    private subjectService: SubjectService,
    private gradeService: GradeService,
    private studentService: StudentService,
    private notify: NotificationService
  ) {}

  async ngOnInit(): Promise<void> {
    await this.loadSubjects();
  }

  get selectedSubject(): Subject | undefined {
    return this.subjects.find(subject => subject.id === this.selectedSubjectId);
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
      const subjects = await this.subjectService.getTeacherSubjects(0);
      this.subjects = (subjects || []).filter(subject => subject.isActive !== false);

      if (!this.selectedSubjectId && this.subjects.length > 0) {
        this.selectedSubjectId = this.subjects[0].id;
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

  async loadGradebook(): Promise<void> {
    if (!this.selectedSubjectId) {
      this.gradebook = null;
      this.students = [];
      this.loadErrorMessage = '';
      this.usingFallbackData = false;
      return;
    }

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
      const fallbackLoaded = await this.loadStudentsFallback(error);
      if (!fallbackLoaded) {
        this.gradebook = null;
        this.students = [];
        this.loadErrorMessage = this.getGradebookErrorMessage(error);
        this.notify.error(this.loadErrorMessage);
      }
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
    if (!this.selectedSubjectId) {
      this.notify.warning('اختر المادة أولًا.');
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

  private async loadStudentsFallback(error: any): Promise<boolean> {
    if (error?.status !== 404) {
      return false;
    }

    const subject = this.selectedSubject;
    if (!subject?.classRoomId) {
      return false;
    }

    try {
      const response = await this.studentService.getStudents({ classRoomId: subject.classRoomId });
      const fallbackStudents = (response.items || [])
        .filter(student => student.isActive !== false)
        .map(student => ({
          id: student.id,
          fullName: student.fullName || 'طالب',
          email: student.email || undefined,
          existingGradeId: null,
          score: null,
          notes: '',
          lastUpdatedAt: null
        }));

      this.gradebook = {
        subjectId: subject.id,
        subjectName: subject.name || 'المادة',
        classRoomId: subject.classRoomId,
        classRoomName: this.getFallbackClassRoomName(subject),
        gradeType: this.selectedGradeType,
        date: this.selectedDate,
        students: fallbackStudents
      };
      this.students = fallbackStudents;
      this.usingFallbackData = true;
      this.notify.info('تم تحميل طلاب الفصل مباشرة لأن خدمة كشف الدرجات غير متاحة في الخادم الحالي.');
      return true;
    } catch {
      return false;
    }
  }

  private getFallbackClassRoomName(subject: Subject): string {
    const description = subject.description?.trim();
    if (description) {
      return description;
    }

    return subject.classRoomId ? `الفصل ${subject.classRoomId}` : 'الفصل المرتبط بالمادة';
  }

  private getGradebookErrorMessage(error: any): string {
    if (error?.status === 404) {
      return 'خدمة كشف درجات المدرس غير متاحة في الخادم الحالي. أعد تشغيل الـbackend ثم جرّب مرة أخرى.';
    }

    return error?.message || 'تعذر تحميل كشف الدرجات.';
  }
}
