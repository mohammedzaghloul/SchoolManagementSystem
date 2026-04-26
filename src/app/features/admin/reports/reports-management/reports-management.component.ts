import { CommonModule } from '@angular/common';
import { Component, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';

import { ApiService } from '../../../../core/services/api.service';

@Component({
  selector: 'app-reports-management',
  standalone: true,
  providers: [provideCharts(withDefaultRegisterables())],
  imports: [CommonModule, FormsModule, BaseChartDirective],
  templateUrl: './reports-management.component.html',
  styleUrl: './reports-management.component.css'
})
export class ReportsManagementComponent implements OnInit {
  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  stats: any = {
    totalSessions: 0,
    totalAttendances: 0,
    totalStudents: 0,
    absentDays: 0,
    attendanceRate: 0,
    gradesDistribution: [],
    weeklyAttendance: []
  };

  classes: any[] = [];
  loading = false;
  errorMessage = '';
  filters = {
    from: '',
    to: '',
    classRoomId: ''
  };

  public donutChartOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false }
    },
    cutout: '72%'
  };

  public donutChartLabels: string[] = [];
  public donutChartData: ChartData<'doughnut'> = {
    labels: [],
    datasets: [{ data: [] }]
  };
  public donutChartType: 'doughnut' = 'doughnut';

  public barChartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: { 
        grid: { display: false },
        ticks: { font: { size: 10 } }
      },
      y: { 
        min: 0, 
        max: 100, 
        ticks: { 
          callback: value => `${value}%`,
          font: { size: 10 }
        } 
      }
    },
    plugins: {
      legend: { display: false },
      tooltip: {
        enabled: true,
        backgroundColor: 'rgba(15, 23, 42, 0.9)',
        padding: 10,
        cornerRadius: 8
      }
    }
  };

  public barChartLabels: string[] = ['السبت', 'الأحد', 'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس'];
  public barChartData: ChartData<'bar'> = {
    labels: this.barChartLabels,
    datasets: [
      {
        data: [],
        label: 'نسبة الحضور',
        backgroundColor: '#2563eb',
        borderRadius: 10,
        hoverBackgroundColor: '#0f766e'
      }
    ]
  };

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.loadClasses();
    this.loadStats();
  }

  async loadStats(): Promise<void> {
    this.loading = true;
    this.errorMessage = '';

    try {
      const params: any = {};
      if (this.filters.from) params.from = this.filters.from;
      if (this.filters.to) params.to = this.filters.to;
      if (this.filters.classRoomId) params.classRoomId = this.filters.classRoomId;

      const data: any = await this.api.get('/api/Dashboards/reports-stats', params);
      this.stats = {
        totalSessions: data.totalSessions || 0,
        totalAttendances: data.totalAttendances || 0,
        totalStudents: data.totalStudents || 0,
        absentDays: data.absentDays || 0,
        attendanceRate: data.attendanceRate || 0,
        gradesDistribution: data.gradesDistribution || [],
        weeklyAttendance: data.weeklyAttendance || []
      };

      this.donutChartLabels = this.stats.gradesDistribution.map((item: any) => item.name);
      this.donutChartData = {
        labels: this.donutChartLabels,
        datasets: [
          {
            data: this.stats.gradesDistribution.map((item: any) => item.value),
            backgroundColor: this.stats.gradesDistribution.map((item: any) => item.color),
            hoverOffset: 6,
            borderWidth: 0
          }
        ]
      };

      this.barChartData = {
        labels: this.barChartLabels,
        datasets: [
          {
            ...this.barChartData.datasets[0],
            data: this.stats.weeklyAttendance
          }
        ]
      };

      setTimeout(() => this.chart?.update(), 0);
    } catch (err) {
      console.error('Failed to load reports stats', err);
      this.errorMessage = 'تعذر تحميل التقارير الآن. تأكد من الاتصال وحاول مرة أخرى.';
    } finally {
      this.loading = false;
    }
  }

  async loadClasses(): Promise<void> {
    try {
      this.classes = await this.api.get<any[]>('/api/ClassRooms');
    } catch {
      this.classes = [];
    }
  }

  resetFilters(): void {
    this.filters = {
      from: '',
      to: '',
      classRoomId: ''
    };
    this.loadStats();
  }

  exportPdf(): void {
    window.print();
  }
}
