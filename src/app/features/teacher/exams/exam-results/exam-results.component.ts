import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ExamService } from '../../../../core/services/exam.service';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

export interface ExamResult {
  id: number;
  studentName: string;
  studentCode: string;
  score: number;
  createdAt: string;
  notes?: string;
  isPass?: boolean;
  isDateValid?: boolean;
}

@Component({
  selector: 'app-exam-results',
  standalone: true,
  imports: [
    CommonModule, 
    RouterModule, 
    MatCardModule, 
    MatTableModule, 
    MatButtonModule, 
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './exam-results.component.html',
  styleUrls: ['./exam-results.component.css']
})
export class ExamResultsComponent implements OnInit {
  results: ExamResult[] = [];
  loading = true;
  examId: number = 0;
  examTitle: string = 'نتائج الاختبار';
  maxScore: number = 100;
  
  // Stats
  averageScore: number = 0;
  highestScore: number = 0;
  passCount: number = 0;
  totalParticipants: number = 0;

  constructor(
    private route: ActivatedRoute,
    private examService: ExamService
  ) { }

  async ngOnInit() {
    this.examId = Number(this.route.snapshot.paramMap.get('id'));
    if (this.examId) {
      await this.loadResults();
    }
  }

  async loadResults() {
    this.loading = true;
    try {
      const examData = await this.examService.getExamDetails(this.examId);
      this.examTitle = examData.title;
      this.maxScore = examData.maxScore || 100;
      
      const res = await this.examService.getExamResults(this.examId);
      this.results = (res || []).map((r: any) => ({
        ...r,
        score: Number(r.score) || 0,
        // Check for 0001 or invalid dates
        isDateValid: r.createdAt && !r.createdAt.startsWith('0001') && !r.createdAt.startsWith('1900'),
        // Add a helper for visual passing
        isPass: (Number(r.score) || 0) >= (this.maxScore / 2)
      }));
      this.calculateStats();
    } catch (error) {
      console.error('Error loading exam results', error);
    } finally {
      this.loading = false;
    }
  }

  calculateStats() {
    if (!this.results || this.results.length === 0) {
      this.totalParticipants = 0;
      this.averageScore = 0;
      this.highestScore = 0;
      this.passCount = 0;
      return;
    }

    // Filter out results with score 0 if they haven't actually started/submitted
    const validResults = this.results;
    this.totalParticipants = validResults.length;
    
    const scores = validResults.map(r => Number(r.score) || 0);
    const totalScoreSum = scores.reduce((a, b) => a + b, 0);
    
    this.averageScore = totalScoreSum / this.totalParticipants;
    this.highestScore = Math.max(...scores);
    
    // Pass mark is 50% of maxScore. If maxScore is 1, passMark is 0.5.
    // Ensure we are comparing numbers accurately
    const currentMax = Number(this.maxScore) || 100;
    const passMarkThreshold = currentMax / 2;
    
    this.passCount = validResults.filter(r => (Number(r.score) || 0) >= passMarkThreshold).length;
    
    console.log('[Stats] Max:', currentMax, 'PassMark:', passMarkThreshold, 'PassCount:', this.passCount);
  }

  getPassPercentage(): number {
    return this.totalParticipants > 0 ? (this.passCount / this.totalParticipants) * 100 : 0;
  }

  showDetailedResults = false;
  selectedResult: ExamResult | null = null;

  showAnswers(result: ExamResult) {
    this.selectedResult = result;
    this.showDetailedResults = true;
  }

  closeModal() {
    this.showDetailedResults = false;
    this.selectedResult = null;
  }
}
