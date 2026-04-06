import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartOptions, ChartType } from 'chart.js';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-student-report',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatSelectModule, MatIconModule, BaseChartDirective, FormsModule],
  template: `
    <div class="report-container" dir="rtl">
      <mat-card>
        <mat-card-header>
          <div mat-card-avatar>
            <mat-icon color="primary">bar_chart</mat-icon>
          </div>
          <mat-card-title>تقرير أداء الطلاب</mat-card-title>
          <mat-card-subtitle>مستويات الأداء والدرجات</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <div class="filters">
            <mat-form-field appearance="outline">
              <mat-label>الصف الدراسي</mat-label>
              <mat-select [(ngModel)]="selectedClass" (selectionChange)="updateChart()">
                <mat-option value="all">جميع الصفوف</mat-option>
                <mat-option value="grade1">الصف الأول</mat-option>
                <mat-option value="grade2">الصف الثاني</mat-option>
              </mat-select>
            </mat-form-field>
          </div>

          <div class="chart-wrapper">
            <canvas baseChart
              [data]="barChartData"
              [options]="barChartOptions"
              [type]="barChartType">
            </canvas>
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .report-container {
      padding: 2rem;
      max-width: 900px;
      margin: 0 auto;
    }
    .filters {
      margin: 1.5rem 0;
    }
    .chart-wrapper {
      height: 400px;
      display: block;
    }
  `]
})
export class StudentReportComponent implements OnInit {
  selectedClass: string = 'all';

  public barChartOptions: ChartOptions<'bar'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        labels: { font: { family: 'Tajawal, sans-serif' } }
      }
    },
    scales: {
      y: {
        beginAtZero: true,
        max: 100
      }
    }
  };

  public barChartType: ChartType = 'bar';

  public barChartData: ChartConfiguration<'bar'>['data'] = {
    labels: ['الرياضيات', 'العلوم', 'اللغة العربية', 'اللغة الإنجليزية', 'التاريخ'],
    datasets: [
      { data: [85, 76, 90, 81, 88], label: 'متوسط درجات الطلاب', backgroundColor: '#3f51b5' },
      { data: [60, 55, 65, 50, 60], label: 'الحد الأدنى للنجاح', backgroundColor: '#f44336' }
    ]
  };

  ngOnInit(): void { }

  updateChart() {
    let newData = [85, 76, 90, 81, 88];
    if (this.selectedClass === 'grade1') {
      newData = [90, 80, 95, 85, 92];
    } else if (this.selectedClass === 'grade2') {
      newData = [80, 72, 85, 78, 84];
    }

    this.barChartData = {
      ...this.barChartData,
      datasets: [
        { ...this.barChartData.datasets[0], data: newData },
        this.barChartData.datasets[1]
      ]
    };
  }
}
