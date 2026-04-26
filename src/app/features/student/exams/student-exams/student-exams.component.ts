import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ExamService } from '../../../../core/services/exam.service';
import { Exam } from '../../../../core/models/exam.model';

@Component({
  selector: 'app-student-exams',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './student-exams.component.html',
  styleUrls: ['./student-exams.component.css']
})
export class StudentExamsComponent implements OnInit, OnDestroy {
  exams: Exam[] = [];
  loading = false;
  activeTab: 'ongoing' | 'upcoming' | 'completed' = 'ongoing';

  ongoingExams: Exam[] = [];
  upcomingExams: Exam[] = [];
  completedExams: Exam[] = [];

  constructor(private examService: ExamService) { }

  refreshTimer: any;

  ngOnInit(): void {
    this.loadExams();
    // Refresh countdown every 60 seconds
    this.refreshTimer = setInterval(() => {
      this.categorizeExams();
    }, 60000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) {
      clearInterval(this.refreshTimer);
    }
  }

  async loadExams() {
    this.loading = true;
    try {
      this.exams = await this.examService.getStudentExams();
      this.categorizeExams();
    } catch (err) {
      console.error(err);
      this.exams = [];
    } finally {
      this.loading = false;
    }
  }

  categorizeExams() {
    const now = new Date().getTime();
    this.ongoingExams = [];
    this.upcomingExams = [];
    this.completedExams = [];

    this.exams.forEach(exam => {
      // 1. Priority: If student already finished it
      if (exam.isCompleted) {
        this.completedExams.push(exam);
        return;
      }

      const startTime = new Date(exam.startTime || "").getTime();
      const endTime = new Date(exam.endTime || "").getTime();

      // 2. Logic based on real clock
      if (now < startTime) {
        this.upcomingExams.push(this.enrichWithTimeUntil(exam, now, startTime));
      } else if (now >= startTime && now <= endTime) {
        this.ongoingExams.push(exam);
      } else {
        // Exam finished but student didn't take it
        this.completedExams.push(exam);
      }
    });
  }

  private enrichWithTimeUntil(exam: any, now: number, startTime: number): any {
    const diffMs = startTime - now;
    const diffHrs = Math.floor(diffMs / (1000 * 60 * 60));
    const diffMins = Math.floor((diffMs % (1000 * 60 * 60)) / (1000 * 60));

    if (diffHrs > 24) {
      exam.timeUntil = `يبدأ بعد ${Math.floor(diffHrs / 24)} يوم`;
    } else if (diffHrs > 0) {
      exam.timeUntil = `يبدأ بعد ${diffHrs} ساعة و ${diffMins} دقيقة`;
    } else {
      exam.timeUntil = `يبدأ بعد ${diffMins} دقيقة`;
    }
    return exam;
  }

  showScoreCard = false;
  selectedExam: Exam | null = null;

  viewResult(exam: Exam) {
    this.selectedExam = {
      ...exam,
      maxScore: exam.maxScore || exam.totalMarks || 0
    };
    this.showScoreCard = true;
  }

  closeScoreCard() {
    this.showScoreCard = false;
    this.selectedExam = null;
  }

  setTab(tab: 'ongoing' | 'upcoming' | 'completed') {
    this.activeTab = tab;
  }
}
