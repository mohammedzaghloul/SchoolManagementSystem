import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GradeService } from '../../../core/services/grade.service';
import { AuthService } from '../../../core/services/auth.service';
import { Grade } from '../../../core/models/grade.model';

@Component({
  selector: 'app-grades',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './grades.component.html',
  styleUrls: ['./grades.component.css']
})
export class GradesComponent implements OnInit {
  grades: Grade[] = [];
  loading = false;
  overallAverage = 0;
  highestSubject: Grade | null = null;
  lowestSubject: Grade | null = null;

  constructor(
    private gradeService: GradeService,
    private auth: AuthService
  ) { }

  async ngOnInit() {
    this.loading = true;
    try {
      this.grades = await this.gradeService.getMyGrades();
      this.calculateStats();
    } catch (err) {
      console.error(err);
      this.grades = [];
    } finally {
      this.loading = false;
    }
  }

  calculateStats() {
    if (!this.grades || this.grades.length === 0) return;

    let total = 0;
    this.grades.forEach(g => total += g.value);

    this.overallAverage = Math.round(total / this.grades.length);

    this.highestSubject = [...this.grades].sort((a, b) => b.value - a.value)[0];
    this.lowestSubject = [...this.grades].sort((a, b) => a.value - b.value)[0];
  }

  getEvaluation(value: number): { text: string, color: string } {
    if (value >= 90) return { text: 'ممتاز', color: 'success' };
    if (value >= 80) return { text: 'جيد جداً', color: 'primary' };
    if (value >= 70) return { text: 'جيد', color: 'warning' };
    return { text: 'مقبول', color: 'danger' };
  }
}
