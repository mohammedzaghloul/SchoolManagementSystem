import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AssignmentService, Assignment } from '../../../../core/services/assignment.service';
import { SubjectService } from '../../../../core/services/subject.service';
import { ClassRoomService } from '../../../../core/services/classroom.service';
import { AuthService } from '../../../../core/services/auth.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { Subject } from '../../../../core/models/subject.model';
import { ClassRoom } from '../../../../core/models/class.model';
import { environment } from '../../../../../environments/environment';

interface AssignmentForm {
  title: string;
  description: string;
  dueDate: string;
  subjectId: number;
  classRoomId: number;
}

@Component({
  selector: 'app-teacher-assignments',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './teacher-assignments.component.html',
  styleUrl: './teacher-assignments.component.css'
})
export class TeacherAssignmentsComponent implements OnInit {
  assignments: Assignment[] = [];
  subjects: Subject[] = [];
  classes: ClassRoom[] = [];
  loading = false;
  loadingDependencies = false;

  // Stats
  totalSubmissions = 0;
  pendingAssignments = 0;

  // Modals
  showAddModal = false;
  showDeleteModal = false;

  // Submissions
  selectedAssignment: Assignment | null = null;
  submissions: any[] = [];
  loadingSubmissions = false;

  // Delete
  assignmentToDelete: Assignment | null = null;

  newAssignment: AssignmentForm = {
    title: '',
    description: '',
    dueDate: this.getDateInputValue(this.addDays(new Date(), 7)),
    subjectId: 0,
    classRoomId: 0
  };

  constructor(
    private assignmentService: AssignmentService,
    private subjectService: SubjectService,
    private classroomService: ClassRoomService,
    private authService: AuthService,
    private notify: NotificationService
  ) { }

  async ngOnInit() {
    await this.loadData();
  }

  async loadData() {
    this.loading = true;
    try {
      const [assignments, classes, subjects] = await Promise.all([
        this.assignmentService.getAssignments(),
        this.loadTeacherClasses(),
        this.loadTeacherSubjects()
      ]);

      this.assignments = this.normalizeList(assignments);
      this.classes = classes;
      this.subjects = subjects.filter(subject => subject.isActive !== false);
      this.calculateStats();
    } catch (error) {
      console.error(error);
      this.notify.error('تعذر تحميل بيانات الواجبات.');
    } finally {
      this.loading = false;
    }
  }

  calculateStats() {
    this.totalSubmissions = this.assignments.reduce((sum, a) => sum + (a.submissionCount || 0), 0);
    const today = new Date();
    this.pendingAssignments = this.assignments.filter(a => new Date(a.dueDate) >= today).length;
  }

  openAddModal() {
    this.resetForm();
    this.selectFirstAvailableClass();
    this.onClassRoomChange();
    this.showAddModal = true;
  }

  get availableClasses(): ClassRoom[] {
    const subjectClassIds = new Set(
      this.subjects
        .map(subject => Number(subject.classRoomId || 0))
        .filter(classRoomId => classRoomId > 0)
    );

    if (!subjectClassIds.size) {
      return [...this.classes].sort((first, second) => first.name.localeCompare(second.name, 'ar'));
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
    const classRoomId = Number(this.newAssignment.classRoomId || 0);
    const scopedSubjects = classRoomId
      ? this.subjects.filter(subject => Number(subject.classRoomId || 0) === classRoomId)
      : this.subjects;

    return this.getUniqueSubjects(scopedSubjects);
  }

  onClassRoomChange(): void {
    const subjectId = Number(this.newAssignment.subjectId || 0);
    const subjectStillAvailable = this.filteredSubjects.some(subject => Number(subject.id) === subjectId);

    if (!subjectStillAvailable) {
      this.newAssignment.subjectId = this.filteredSubjects[0]?.id || 0;
    }
  }

  async createAssignment() {
    if (!this.newAssignment.title.trim() || !this.newAssignment.subjectId || !this.newAssignment.classRoomId || !this.newAssignment.dueDate) {
      this.notify.warning('يرجى ملء عنوان الواجب والفصل والمادة وتاريخ التسليم.');
      return;
    }

    try {
      const payload: Assignment = {
        title: this.newAssignment.title.trim(),
        description: this.newAssignment.description.trim(),
        dueDate: new Date(`${this.newAssignment.dueDate}T23:59:00`),
        subjectId: Number(this.newAssignment.subjectId),
        classRoomId: Number(this.newAssignment.classRoomId)
      };

      await this.assignmentService.createAssignment(payload);
      this.showAddModal = false;
      await this.loadData();
      this.resetForm();
      this.notify.success('تم إضافة الواجب بنجاح.');
    } catch (error) {
      this.notify.error('حدث خطأ أثناء إضافة الواجب.');
    }
  }

  async viewSubmissions(assignment: Assignment) {
    this.selectedAssignment = assignment;
    this.loadingSubmissions = true;
    try {
      this.submissions = await this.assignmentService.getSubmissions(assignment.id!);
    } catch (error) {
      console.error(error);
      this.submissions = [];
    } finally {
      this.loadingSubmissions = false;
    }
  }

  confirmDelete(assignment: Assignment) {
    this.assignmentToDelete = assignment;
    this.showDeleteModal = true;
  }

  async deleteAssignment() {
    if (!this.assignmentToDelete) return;
    try {
      await this.assignmentService.deleteAssignment(this.assignmentToDelete.id!);
      this.showDeleteModal = false;
      this.assignmentToDelete = null;
      await this.loadData();
      this.notify.success('تم حذف الواجب بنجاح.');
    } catch (error) {
      this.notify.error('حدث خطأ أثناء حذف الواجب.');
    }
  }

  isOverdue(date: Date): boolean {
    return new Date(date) < new Date();
  }

  isDueSoon(date: Date): boolean {
    const d = new Date(date);
    const now = new Date();
    const diff = d.getTime() - now.getTime();
    return diff > 0 && diff < 3 * 24 * 60 * 60 * 1000; // 3 days
  }

  resetForm() {
    this.newAssignment = {
      title: '',
      description: '',
      dueDate: this.getDateInputValue(this.addDays(new Date(), 7)),
      subjectId: 0,
      classRoomId: 0
    };
  }

  private async loadTeacherClasses(): Promise<ClassRoom[]> {
    this.loadingDependencies = true;

    try {
      const teacherId = this.getNumericTeacherId();
      const classes = this.normalizeList(await this.classroomService.getTeacherClasses(teacherId));

      if (classes.length > 0) {
        return classes;
      }
    } catch {
      return [];
    } finally {
      this.loadingDependencies = false;
    }

    return [];
  }

  private async loadTeacherSubjects(): Promise<Subject[]> {
    try {
      const teacherId = this.getNumericTeacherId();
      const subjects = this.normalizeList(await this.subjectService.getTeacherSubjects(teacherId));

      if (subjects.length > 0) {
        return subjects;
      }
    } catch {
      return [];
    }

    return [];
  }

  getSubmissionFileUrl(submission: any): string {
    const rawUrl = String(submission?.fileUrl || '').trim();
    if (!rawUrl) {
      return '';
    }

    if (/^https?:\/\//i.test(rawUrl)) {
      return rawUrl;
    }

    const normalizedPath = rawUrl.startsWith('/') ? rawUrl : `/${rawUrl}`;
    return `${environment.apiUrl}${normalizedPath}`;
  }

  getSubmissionFileName(submission: any): string {
    const url = String(submission?.fileUrl || '').split('?')[0];
    const name = decodeURIComponent(url.substring(url.lastIndexOf('/') + 1));
    return name || 'ملف التسليم';
  }

  private selectFirstAvailableClass(): void {
    if (!this.newAssignment.classRoomId && this.availableClasses.length > 0) {
      this.newAssignment.classRoomId = this.availableClasses[0].id;
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
      const key = `${Number(subject.classRoomId || this.newAssignment.classRoomId || 0)}-${this.normalizeSubjectName(subject.name)}`;
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

  private addDays(date: Date, days: number): Date {
    const value = new Date(date);
    value.setDate(value.getDate() + days);
    return value;
  }

  private getDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
