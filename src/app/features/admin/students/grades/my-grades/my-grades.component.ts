// features/student/grades/my-grades/my-grades.component.ts
@Component({
  selector: 'app-my-grades',
  templateUrl: './my-grades.component.html',
  styleUrls: ['./my-grades.component.css']
})
export class MyGradesComponent implements OnInit {
  grades: Grade[] = [];
  subjects: SubjectGrade[] = [];
  selectedSubject: string = 'all';
  selectedTerm: string = 'all';
  chartData: any;
  statistics: any;

  constructor(
    private gradeService: GradeService,
    private auth: AuthService
  ) {}

  async ngOnInit() {
    await this.loadGrades();
    this.prepareChartData();
    this.calculateStatistics();
  }

  async loadGrades(): Promise<void> {
    const studentId = this.auth.currentUser.studentId;
    this.grades = await this.gradeService.getStudentGrades(studentId);
    this.processSubjects();
  }

  processSubjects(): void {
    const subjectMap = new Map<string, SubjectGrade>();
    
    this.grades.forEach(grade => {
      if (!subjectMap.has(grade.subject)) {
        subjectMap.set(grade.subject, {
          name: grade.subject,
          grades: [],
          average: 0,
          highest: 0,
          lowest: 100
        });
      }
      
      const subject = subjectMap.get(grade.subject)!;
      subject.grades.push(grade);
      subject.average = subject.grades.reduce((sum, g) => sum + g.score, 0) / subject.grades.length;
      subject.highest = Math.max(subject.highest, grade.score);
      subject.lowest = Math.min(subject.lowest, grade.score);
    });
    
    this.subjects = Array.from(subjectMap.values());
  }

  prepareChartData(): void {
    this.chartData = {
      labels: this.subjects.map(s => s.name),
      datasets: [
        {
          label: 'درجاتك',
          data: this.subjects.map(s => s.average),
          backgroundColor: 'rgba(59, 130, 246, 0.5)',
          borderColor: '#3B82F6',
          borderWidth: 1
        },
        {
          label: 'متوسط الفصل',
          data: this.subjects.map(s => s.classAverage || 0),
          backgroundColor: 'rgba(16, 185, 129, 0.5)',
          borderColor: '#10B981',
          borderWidth: 1
        }
      ]
    };
  }

  calculateStatistics(): void {
    const allGrades = this.grades.map(g => g.score);
    this.statistics = {
      total: this.grades.length,
      average: allGrades.reduce((a, b) => a + b, 0) / allGrades.length,
      highest: Math.max(...allGrades),
      lowest: Math.min(...allGrades),
      passed: allGrades.filter(g => g >= 50).length,
      failed: allGrades.filter(g => g < 50).length
    };
  }

  get filteredGrades(): Grade[] {
    return this.grades.filter(grade => {
      if (this.selectedSubject !== 'all' && grade.subject !== this.selectedSubject) {
        return false;
      }
      if (this.selectedTerm !== 'all' && grade.term !== this.selectedTerm) {
        return false;
      }
      return true;
    });
  }

  getGradeClass(score: number): string {
    if (score >= 90) return 'excellent';
    if (score >= 80) return 'very-good';
    if (score >= 70) return 'good';
    if (score >= 60) return 'acceptable';
    if (score >= 50) return 'pass';
    return 'fail';
  }

  getGradeText(score: number): string {
    if (score >= 90) return 'ممتاز';
    if (score >= 80) return 'جيد جداً';
    if (score >= 70) return 'جيد';
    if (score >= 60) return 'مقبول';
    if (score >= 50) return 'ناجح';
    return 'راسب';
  }
}