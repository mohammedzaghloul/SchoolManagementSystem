import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartOptions, ChartType } from 'chart.js';
import { FormsModule } from '@angular/forms';

import { ApiService } from '../../../core/services/api.service';

@Component({
  selector: 'app-attendance-report',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatSelectModule, MatIconModule, BaseChartDirective, FormsModule],
  template: `
    <div class="report-container" dir="rtl">
      <mat-card>
        <mat-card-header>
          <div mat-card-avatar>
            <mat-icon color="primary">pie_chart</mat-icon>
          </div>
          <mat-card-title>تقرير الحضور والغياب</mat-card-title>
          <mat-card-subtitle>إحصائيات الحضور</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <div class="filters">
            <mat-form-field appearance="outline">
              <mat-label>الفترة الزمنية</mat-label>
              <mat-select [(ngModel)]="selectedPeriod" (selectionChange)="updateChart()">
                <mat-option value="week">هذا الأسبوع</mat-option>
                <mat-option value="month">هذا الشهر</mat-option>
                <mat-option value="term">الفصل الدراسي</mat-option>
              </mat-select>
            </mat-form-field>
          </div>

          <div class="chart-wrapper">
            <canvas baseChart *ngIf="!loading"
              [data]="pieChartData"
              [type]="pieChartType"
              [options]="pieChartOptions">
            </canvas>
            <div *ngIf="loading" class="text-center w-100 mt-5">
              جاري تحميل البيانات...
            </div>
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .report-container {
      padding: 2rem;
      max-width: 800px;
      margin: 0 auto;
    }
    .filters {
      margin: 1.5rem 0;
    }
    .chart-wrapper {
      height: 400px;
      display: flex;
      justify-content: center;
      align-items: center;
    }
  `]
})
export class AttendanceReportComponent implements OnInit {
  selectedPeriod: string = 'week';
  loading = false;

  public pieChartOptions: ChartOptions<'pie'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'top',
        labels: { font: { family: 'Tajawal, sans-serif' } }
      }
    }
  };

  public pieChartData: ChartConfiguration<'pie'>['data'] = {
    labels: ['حاضر', 'غائب', 'متأخر', 'بعذر'],
    datasets: [{
      data: [0, 0, 0, 0],
      backgroundColor: ['#4caf50', '#f44336', '#ff9800', '#2196f3']
    }]
  };

  public pieChartType: ChartType = 'pie';

  constructor(private api: ApiService) { }

  ngOnInit(): void {
    this.updateChart();
  }

  async updateChart() {
    this.loading = true;
    try {
      const res: any = await this.api.get(`/api/Reports/attendance?period=${this.selectedPeriod}`);
      const data = res?.data || [0, 0, 0, 0];

      this.pieChartData = {
        ...this.pieChartData,
        datasets: [{
          ...this.pieChartData.datasets[0],
          data: data
        }]
      };
    } catch {
      this.pieChartData = {
        ...this.pieChartData,
        datasets: [{
          ...this.pieChartData.datasets[0],
          data: [0, 0, 0, 0]
        }]
      };
    } finally {
      this.loading = false;
    }
  }
}
