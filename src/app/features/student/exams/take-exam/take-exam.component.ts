import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatRadioModule } from '@angular/material/radio';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatIconModule } from '@angular/material/icon';
import { ExamService } from '../../../../core/services/exam.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { Exam } from '../../../../core/models/exam.model';

@Component({
  selector: 'app-take-exam',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatRadioModule,
    MatProgressBarModule,
    MatIconModule
  ],
  templateUrl: './take-exam.component.html',
  styleUrls: ['./take-exam.component.css']
})
export class TakeExamComponent implements OnInit, OnDestroy {
  exam: Exam | null = null;
  currentQuestionIndex = 0;
  answers: Map<number, any> = new Map();
  timeRemaining: number = 0;
  timerInterval: any;
  loading = true;
  submitting = false;
  fullscreenWarning = false;

  constructor(
    private route: ActivatedRoute,
    private examService: ExamService,
    private notification: NotificationService,
    private router: Router
  ) { }

  async ngOnInit() {
    const examId = this.route.snapshot.params['id'];
    await this.loadExam(examId);
    this.startTimer();
    this.enableFullscreen();
    this.preventTabSwitch();
  }

  async loadExam(examId: number): Promise<void> {
    try {
      this.exam = await this.examService.getExamDetails(examId);

      const now = new Date().getTime();
      const start = new Date(this.exam.startTime!).getTime();
      const end = new Date(this.exam.endTime!).getTime();

      // 1. Check if exam is in the future
      if (now < start) {
        this.notification.error('عذراً، هذا الاختبار لم يبدأ بعد. يرجى العودة في الموعد المحدد.');
        this.router.navigate(['/student/exams']);
        return;
      }

      // 2. Check if student already finished it
      if (this.exam.isCompleted) {
        this.notification.warning('لقد قمت بتأدية هذا الاختبار مسبقاً.');
        this.router.navigate(['/student/exams']);
        return;
      }

      // 3. Setup timer
      this.timeRemaining = Math.max(0, Math.floor((end - now) / 1000));
      
      if (this.timeRemaining <= 0) {
        this.notification.error('انتهى الوقت المحدد لهذا الاختبار.');
        this.router.navigate(['/student/exams']);
        return;
      }

      this.exam.questions?.forEach(q => {
        this.answers.set(q.id, null);
      });

    } catch (error) {
      this.notification.error('لا يمكن الوصول إلى هذا الاختبار');
      this.router.navigate(['/student/exams']);
    } finally {
      this.loading = false;
    }
  }

  startTimer(): void {
    this.timerInterval = setInterval(() => {
      if (this.timeRemaining > 0) {
        this.timeRemaining--;

        // Auto-submit when time runs out
        if (this.timeRemaining === 0) {
          this.autoSubmit();
        }
      }
    }, 1000);
  }

  enableFullscreen(): void {
    const elem = document.documentElement;
    if (elem.requestFullscreen) {
      elem.requestFullscreen();
    }
  }

  preventTabSwitch(): void {
    document.addEventListener('visibilitychange', () => {
      if (document.hidden) {
        this.fullscreenWarning = true;
        this.notification.warning('تم تسجيل محاولة الخروج من الاختبار');
      }
    });
  }

  onAnswerChange(questionId: number, answer: any): void {
    this.answers.set(questionId, answer);
    this.autoSave();
  }

  autoSave(): void {
    localStorage.setItem(`exam_${this.exam?.id}`, JSON.stringify({
      answers: Array.from(this.answers.entries()),
      timestamp: new Date()
    }));
  }

  getProgress(): number {
    if (!this.exam || !this.exam.questions) return 0;
    const answered = Array.from(this.answers.values()).filter(a => a !== null).length;
    return (answered / this.exam.questions.length) * 100;
  }

  get formattedTime(): string {
    const hours = Math.floor(this.timeRemaining / 3600);
    const minutes = Math.floor((this.timeRemaining % 3600) / 60);
    const seconds = this.timeRemaining % 60;

    return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
  }

  get timeProgress(): number {
    if (!this.exam) return 100;
    const total = this.exam.duration * 60;
    return (this.timeRemaining / total) * 100;
  }

  async onSubmit(): Promise<void> {
    if (!confirm('هل أنت متأكد من تسليم الاختبار؟')) return;

    this.submitting = true;

    try {
      const answers = Array.from(this.answers.entries())
        .filter(([_, choiceId]) => choiceId !== null)
        .map(([qId, choiceId]) => ({
          questionId: qId,
          choiceId: choiceId
        }));

      await this.examService.submitExam(this.exam!.id, answers);

      // Clear saved data
      localStorage.removeItem(`exam_${this.exam?.id}`);

      this.notification.success('تم تسليم الاختبار بنجاح');
      this.router.navigate(['/student/exams/results', this.exam!.id]);

    } catch (error) {
      this.notification.error('حدث خطأ في تسليم الاختبار');
    } finally {
      this.submitting = false;
    }
  }

  async autoSubmit(): Promise<void> {
    this.notification.warning('انتهى الوقت المحدد للاختبار');
    await this.onSubmit();
  }

  ngOnDestroy(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
    }

    if (document.exitFullscreen) {
      document.exitFullscreen();
    }
  }

  getChar(index: number): string {
    return String.fromCharCode(65 + index);
  }
}