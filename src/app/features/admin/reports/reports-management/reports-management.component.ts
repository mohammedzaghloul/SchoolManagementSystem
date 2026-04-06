import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../../core/services/api.service';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';

@Component({
  selector: 'app-reports-management',
  standalone: true,
  providers: [provideCharts(withDefaultRegisterables())],
  imports: [CommonModule, FormsModule, BaseChartDirective],
  templateUrl: './reports-management.component.html',
  styleUrl: './reports-management.component.css'
})
export class ReportsManagementComponent implements OnInit {
  @ViewChild(BaseChartDirective) chart: BaseChartDirective | undefined;

  stats: any = {
    totalSessions: 0,
    absentDays: 0,
    attendanceRate: 0,
    teacherPerformance: 95,
    gradesDistribution: [],
    weeklyAttendance: []
  };

  loading = false;

  // Donut Chart Config
  public donutChartOptions: any = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false }
    },
    cutout: 75
  };
  public donutChartLabels: string[] = [];
  public donutChartData: ChartData<'doughnut'> = {
    labels: [],
    datasets: [{ data: [] }]
  };
  public donutChartType: ChartType = 'doughnut';

  // Bar Chart Config
  public barChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: { grid: { display: false } },
      y: { min: 0, max: 100 }
    },
    plugins: {
      legend: { display: false }
    }
  };
  public barChartLabels: string[] = ['أحد', 'اثنين', 'ثلاثاء', 'أربعاء', 'خميس', 'جمعة'];
  public barChartData: ChartData<'bar'> = {
    labels: this.barChartLabels,
    datasets: [
      { 
        data: [], 
        label: 'نسبة الحضور',
        backgroundColor: '#6366f1',
        borderRadius: 8,
        hoverBackgroundColor: '#a855f7'
      }
    ]
  };

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.loadStats();
  }

  async loadStats() {
    this.loading = true;
    try {
      const data: any = await this.api.get('/api/Dashboards/reports-stats');
      this.stats = {
        ...this.stats,
        totalSessions: data.totalSessions,
        absentDays: data.absentDays,
        attendanceRate: data.attendanceRate,
        gradesDistribution: data.gradesDistribution,
        weeklyAttendance: data.weeklyAttendance
      };

      // Map Donut Data
      this.donutChartLabels = data.gradesDistribution.map((g: any) => g.name);
      this.donutChartData = {
        labels: this.donutChartLabels,
        datasets: [{
          data: data.gradesDistribution.map((g: any) => g.value),
          backgroundColor: data.gradesDistribution.map((g: any) => g.color),
          hoverOffset: 4,
          borderWidth: 0
        }]
      };

      // Map Bar Data
      this.barChartData.datasets[0].data = data.weeklyAttendance;
      
      this.chart?.update();

    } catch (err) {
      console.error('Failed to load reports stats', err);
    } finally {
      this.loading = false;
    }
  }

  exportPdf() {
    window.print();
  }
}

