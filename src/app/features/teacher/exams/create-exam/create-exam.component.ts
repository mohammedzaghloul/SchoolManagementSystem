import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatStepperModule } from '@angular/material/stepper';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { ExamService } from '../../../../core/services/exam.service';
import { ClassRoomService } from '../../../../core/services/classroom.service';
import { SubjectService } from '../../../../core/services/subject.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { ClassRoom } from '../../../../core/models/class.model';
import { Subject } from '../../../../core/models/subject.model';
import { AuthService } from '../../../../core/services/auth.service';

export interface Question {
  id: number;
  text: string;
  type: string;
  marks: number;
  options: string[];
  correctAnswer: number | string;
}

@Component({
  selector: 'app-create-exam',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatStepperModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatCheckboxModule,
    MatIconModule
  ],
  templateUrl: './create-exam.component.html',
  styleUrls: ['./create-exam.component.css']
})
export class CreateExamComponent implements OnInit {
  examForm: FormGroup;
  classes: ClassRoom[] = [];
  subjects: Subject[] = [];
  questions: Question[] = [];
  currentStep = 1;
  totalSteps = 3;
  loading = false;
  dependenciesLoading = false;
  dependencyError = '';
  examTypes = [
    { value: 'midterm', label: 'منتصف الفصل' },
    { value: 'final', label: 'نهائي' },
    { value: 'quiz', label: 'اختبار قصير' }
  ];

  constructor(
    private fb: FormBuilder,
    private examService: ExamService,
    private classService: ClassRoomService,
    private subjectService: SubjectService,
    private notification: NotificationService,
    private router: Router,
    private auth: AuthService,
    private route: ActivatedRoute
  ) {
    this.examForm = this.fb.group({
      // Step 1: Basic Info
      title: ['', Validators.required],
      description: [''],
      classRoomId: ['', Validators.required],
      subjectId: ['', Validators.required],
      type: ['midterm', Validators.required],

      // Step 2: Schedule
      date: ['', Validators.required],
      startTime: ['', Validators.required],
      endTime: ['', Validators.required],
      duration: ['', Validators.required],

      // Step 3: Settings
      totalMarks: ['100', Validators.required],
      passMark: ['50', Validators.required],
      shuffleQuestions: [false],
      showResults: [true],
      allowReview: [true],
      instructions: ['']
    });
  }

  isEditMode = false;
  examId: number | null = null;

  get selectedClassRoomId(): number {
    return Number(this.examForm.get('classRoomId')?.value || 0);
  }

  get filteredSubjects(): Subject[] {
    if (!this.selectedClassRoomId) {
      return [];
    }

    const classSubjects = this.subjects.filter(subject =>
      !subject.classRoomId || Number(subject.classRoomId) === this.selectedClassRoomId
    );

    return this.getUniqueSubjects(classSubjects);
  }

  async ngOnInit() {
    this.examId = Number(this.route.snapshot.paramMap.get('id'));
    if (this.examId) {
      this.isEditMode = true;
    }

    // Load dropdown options first
    this.dependenciesLoading = true;
    this.dependencyError = '';

    try {
      await Promise.all([
        this.loadClasses(),
        this.loadSubjects()
      ]);
    } finally {
      this.dependenciesLoading = false;
    }

    // Now load and patch exam data if in edit mode
    if (this.isEditMode && this.examId) {
      await this.loadExamForEdit(this.examId);
    }
  }

  async loadExamForEdit(id: number) {
    this.loading = true;
    try {
      const exam = await this.examService.getExamDetails(id);
      if (exam) {
        // Formats date for input[type="date"] (yyyy-MM-dd)
        const dateObj = new Date(exam.startTime);
        const dateStr = dateObj.toISOString().split('T')[0];
        
        // Format times for input[type="time"] (HH:mm)
        const startStr = dateObj.toTimeString().substring(0, 5);
        const endStr = new Date(exam.endTime || "").toTimeString().substring(0, 5);

        this.examForm.patchValue({
          title: exam.title,
          description: exam.description,
          classRoomId: exam.classRoomId,
          subjectId: exam.subjectId,
          type: exam.status || 'midterm',
          date: dateStr,
          startTime: startStr,
          endTime: endStr,
          duration: exam.duration,
          totalMarks: exam.totalMarks,
          passMark: exam.passingMarks || 50
        });

        if (exam.questions && exam.questions.length > 0) {
          this.questions = exam.questions.map((q: any) => ({
            id: q.id || Date.now() + Math.random(),
            text: q.text,
            type: q.type || 'multiple-choice',
            marks: q.marks,
            options: q.options || ['', '', '', ''],
            correctAnswer: q.correctAnswer
          }));
        }
      }
    } catch (error) {
      console.error('[CreateExam] Error loading exam:', error);
      this.notification.error('حدث خطأ في تحميل بيانات الاختبار');
    } finally {
      this.loading = false;
    }
  }

  async loadClasses(): Promise<void> {
    try {
      const teacherId = this.getNumericTeacherId();
      const classes = await this.classService.getTeacherClasses(teacherId);
      this.classes = this.normalizeList(classes);

      if (this.classes.length === 0) {
        this.classes = this.normalizeList(await this.classService.getClassRooms());
      }
    } catch (error) {
      console.error('[CreateExam] Error loading classes:', error);
      const fallbackClasses = await this.classService.getClassRooms().catch((): ClassRoom[] => []);
      this.classes = this.normalizeList<ClassRoom>(fallbackClasses);
      this.setDependencyError();
    }
  }

  onClassRoomChange(): void {
    const currentSubjectId = Number(this.examForm.get('subjectId')?.value || 0);
    const subjectStillAvailable = this.filteredSubjects.some(subject => Number(subject.id) === currentSubjectId);

    if (!subjectStillAvailable) {
      this.examForm.get('subjectId')?.setValue('');
    }
  }

  async loadSubjects(): Promise<void> {
    try {
      const teacherId = this.getNumericTeacherId();
      const subjects = await this.subjectService.getTeacherSubjects(teacherId);
      this.subjects = this.normalizeList(subjects).filter(subject => subject.isActive !== false);

      if (this.subjects.length === 0) {
        this.subjects = this.normalizeList(await this.subjectService.getSubjects())
          .filter(subject => subject.isActive !== false);
      }
    } catch (error) {
      console.error('[CreateExam] Error loading subjects:', error);
      const fallbackSubjects = await this.subjectService.getSubjects().catch((): Subject[] => []);
      this.subjects = this.normalizeList<Subject>(fallbackSubjects)
        .filter(subject => subject.isActive !== false);
      this.setDependencyError();
    }
  }

  private getNumericTeacherId(): number | undefined {
    const id = Number(this.auth.getCurrentUser()?.id);
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
      const key = `${subject.classRoomId || this.selectedClassRoomId}-${this.normalizeSubjectName(subject.name)}`;
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

  private setDependencyError(): void {
    this.dependencyError = 'تعذر تحميل بعض البيانات، فتم عرض البيانات المتاحة بدلًا منها.';
  }

  nextStep(): void {
    if (this.currentStep < this.totalSteps) {
      // Validate current step
      if (this.currentStep === 1 && (
        this.examForm.get('title')?.invalid ||
        this.examForm.get('classRoomId')?.invalid ||
        this.examForm.get('subjectId')?.invalid ||
        this.examForm.get('type')?.invalid
      )) {
        ['title', 'classRoomId', 'subjectId', 'type'].forEach(controlName =>
          this.examForm.get(controlName)?.markAsTouched()
        );
        this.notification.warning('يرجى إكمال معلومات الاختبار الأساسية');
        return;
      }

      if (this.currentStep === 2) {
        const { date, startTime, endTime } = this.examForm.value;
        if (!date || !startTime || !endTime) {
          this.notification.warning('يرجى إكمال معلومات التوقيت');
          return;
        }
      }

      this.currentStep++;
    }
  }

  prevStep(): void {
    if (this.currentStep > 1) {
      this.currentStep--;
    }
  }

  addQuestion(): void {
    this.questions.push({
      id: Date.now(),
      text: '',
      type: 'multiple-choice',
      marks: 5,
      options: ['', '', '', ''],
      correctAnswer: 0
    });
  }

  removeQuestion(index: number): void {
    this.questions.splice(index, 1);
  }

  async onSubmit(): Promise<void> {
    if (this.examForm.invalid) {
      Object.keys(this.examForm.controls).forEach(key => {
        this.examForm.get(key)?.markAsTouched();
      });
      this.notification.warning('يرجى إكمال جميع الحقول المطلوبة');
      return;
    }

    if (this.questions.length === 0) {
      this.notification.warning('يرجى إضافة أسئلة للاختبار');
      return;
    }

    try {
      const formValue = this.examForm.value;
      
      // Combine date and time strings into valid Date objects
      const examDate = formValue.date; // e.g. "2025-10-01"
      const startDateTime = new Date(`${examDate}T${formValue.startTime}`);
      const endDateTime = new Date(`${examDate}T${formValue.endTime}`);

      const examData = {
        title: formValue.title,
        description: formValue.description,
        classRoomId: Number(formValue.classRoomId),
        subjectId: Number(formValue.subjectId),
        type: formValue.type,
        date: startDateTime.toISOString(),
        startTime: startDateTime.toISOString(),
        endTime: endDateTime.toISOString(),
        duration: Number(formValue.duration),
        totalMarks: Number(formValue.totalMarks),
        questions: this.questions.map(q => {
          let options = q.options;
          let correctAnswer = q.correctAnswer;

          if (q.type === 'true-false') {
            options = ['صح', 'خطأ'];
            correctAnswer = q.correctAnswer === 'true' ? 0 : 1;
          } else if (q.type === 'essay') {
            options = [];
            correctAnswer = 0;
          }

          return {
            text: q.text,
            marks: Number(q.marks),
            options: options,
            correctAnswer: Number(correctAnswer)
          };
        })
      };

      console.log('[CreateExam] Submitting data:', examData);

      if (this.isEditMode && this.examId) {
        await this.examService.updateExam(this.examId, examData);
        this.notification.success('تم تحديث الاختبار بنجاح');
      } else {
        await this.examService.createExam(examData);
        this.notification.success('تم إنشاء الاختبار بنجاح');
      }
      
      this.router.navigate(['/teacher/exams']);

    } catch (error) {
      console.error('[CreateExam] Submit error:', error);
      this.notification.error(this.isEditMode ? 'حدث خطأ في تحديث الاختبار' : 'حدث خطأ في إنشاء الاختبار');
    }
  }
}
