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
  years: string[] = [];
  terms: string[] = [];
  selectedYear = 'all';
  selectedTerm = 'all';
  loading = false;
  overallAverage = 0;
  highestSubject: Grade | null = null;
  lowestSubject: Grade | null = null;
  isDarkMode = true;

  constructor(
    private gradeService: GradeService,
    private auth: AuthService
  ) {}

  async ngOnInit() {
    this.loading = true;
    try {
      this.allGrades = await this.gradeService.getMyGrades();
      this.hydrateFilters();
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
    this.filteredGrades = this.allGrades.filter(grade => {
      const yearMatch = this.selectedYear === 'all' || grade.academicYear === this.selectedYear;
      const termMatch = this.selectedTerm === 'all' || grade.term === this.selectedTerm;
      return yearMatch && termMatch;
    });

    this.calculateStats();
  }

  calculateStats() {
    if (!this.filteredGrades.length) {
      this.overallAverage = 0;
      this.highestSubject = null;
      this.lowestSubject = null;
      return;
    }

    const total = this.filteredGrades.reduce((sum, grade) => sum + grade.value, 0);
    this.overallAverage = Math.round(total / this.filteredGrades.length);
    this.highestSubject = [...this.filteredGrades].sort((first, second) => second.value - first.value)[0];
    this.lowestSubject = [...this.filteredGrades].sort((first, second) => first.value - second.value)[0];
  }

  toggleTheme() {
    this.isDarkMode = !this.isDarkMode;
  }

  getEvaluation(value: number): { text: string, color: string } {
    if (value >= 90) return { text: 'ممتاز', color: 'success' };
    if (value >= 80) return { text: 'جيد جدا', color: 'primary' };
    if (value >= 70) return { text: 'جيد', color: 'warning' };
    return { text: 'مقبول', color: 'danger' };
  }

  private hydrateFilters() {
    this.years = Array.from(
      new Set(
        this.allGrades
          .map(grade => grade.academicYear)
          .filter((value): value is string => !!value)
      )
    );

    this.terms = Array.from(
      new Set(
        this.allGrades
          .map(grade => grade.term)
          .filter((value): value is string => !!value)
      )
    );

    if (this.selectedYear !== 'all' && !this.years.includes(this.selectedYear)) {
      this.selectedYear = 'all';
    }

    if (this.selectedTerm !== 'all' && !this.terms.includes(this.selectedTerm)) {
      this.selectedTerm = 'all';
    }
  }
}
