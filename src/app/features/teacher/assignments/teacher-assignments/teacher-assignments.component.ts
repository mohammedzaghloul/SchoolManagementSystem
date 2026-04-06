import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AssignmentService, Assignment } from '../../../../core/services/assignment.service';
import { SubjectService } from '../../../../core/services/subject.service';
import { ClassRoomService } from '../../../../core/services/classroom.service';

@Component({
  selector: 'app-teacher-assignments',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './teacher-assignments.component.html',
  styleUrl: './teacher-assignments.component.css'
})
export class TeacherAssignmentsComponent implements OnInit {
  assignments: Assignment[] = [];
  subjects: any[] = [];
  classes: any[] = [];
  loading = false;

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

  newAssignment: Assignment = {
    title: '',
    description: '',
    dueDate: new Date(),
    subjectId: 0,
    classRoomId: 0
  };

  constructor(
    private assignmentService: AssignmentService,
    private subjectService: SubjectService,
    private classroomService: ClassRoomService
  ) { }

  async ngOnInit() {
    await this.loadData();
  }

  async loadData() {
    this.loading = true;
    try {
      this.assignments = await this.assignmentService.getAssignments();
      this.subjects = await this.subjectService.getSubjects();
      this.classes = await this.classroomService.getClassRooms();
      this.calculateStats();
    } catch (error) {
      console.error(error);
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
    this.showAddModal = true;
  }

  async createAssignment() {
    if (!this.newAssignment.title || !this.newAssignment.subjectId || !this.newAssignment.classRoomId) {
      alert('يرجى ملء جميع الحقول المطلوبة');
      return;
    }

    try {
      await this.assignmentService.createAssignment(this.newAssignment);
      this.showAddModal = false;
      await this.loadData();
      this.resetForm();
    } catch (error) {
      alert('حدث خطأ أثناء إضافة الواجب');
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
    } catch (error) {
      alert('حدث خطأ أثناء حذف الواجب');
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
      dueDate: new Date(),
      subjectId: 0,
      classRoomId: 0
    };
  }
}
