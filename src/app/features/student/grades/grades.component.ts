import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GradeService } from '../../../core/services/grade.service';
import { AuthService } from '../../../core/services/auth.service';
import { Grade } from '../../../core/models/grade.model';

@Component({
  selector: 'app-grades',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './grades.component.html',
  styleUrls: ['./grades.component.css']
})
export class GradesComponent implements OnInit {
  allGrades: Grade[] = [];
  filteredGrades: Grade[] = [];
  years: string[] = ['2023/2024', '2024/2025'];
  terms: string[] = ['الكل', 'الترم الأول', 'الترم الثاني'];
  selectedYear = 'الكل';
  selectedTerm = 'الكل';
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
      this.allGrades = await this.gradeService.getMyGrades();
      this.filterGrades();
    } catch (err) {
      console.error(err);
      this.allGrades = [];
      this.filteredGrades = [];
    } finally {
      this.loading = false;
    }
  }

  filterGrades() {
    this.filteredGrades = this.allGrades.filter(g => {
      const yearMatch = this.selectedYear === 'الكل' || g.academicYear === this.selectedYear;
      const termMatch = this.selectedTerm === 'الكل' || g.term === this.selectedTerm;
      return yearMatch && termMatch;
    });
    this.calculateStats();
  }

  calculateStats() {
    if (!this.filteredGrades || this.filteredGrades.length === 0) {
      this.overallAverage = 0;
      this.highestSubject = null;
      this.lowestSubject = null;
      return;
    }

    let total = 0;
    this.filteredGrades.forEach(g => total += g.value);

    this.overallAverage = Math.round(total / this.filteredGrades.length);

    this.highestSubject = [...this.filteredGrades].sort((a, b) => b.value - a.value)[0];
    this.lowestSubject = [...this.filteredGrades].sort((a, b) => a.value - b.value)[0];
  }

  getEvaluation(value: number): { text: string, color: string } {
    if (value >= 90) return { text: 'ممتاز', color: 'success' };
    if (value >= 80) return { text: 'جيد جداً', color: 'primary' };
    if (value >= 70) return { text: 'جيد', color: 'warning' };
    return { text: 'مقبول', color: 'danger' };
  }
}
