// features/teacher/exams/exam-list/exam-list.component.ts
@Component({
  selector: 'app-exam-list',
  templateUrl: './exam-list.component.html',
  styleUrls: ['./exam-list.component.css']
})
export class ExamListComponent implements OnInit {
  exams: Exam[] = [];
  filteredExams: Exam[] = [];
  loading = false;
  searchTerm = '';
  selectedStatus = 'all';
  selectedClass = 'all';
  classes: ClassRoom[] = [];
  
  constructor(
    private examService: ExamService,
    private classService: ClassRoomService,
    private notification: NotificationService,
    private router: Router
  ) {}

  async ngOnInit() {
    await Promise.all([
      this.loadExams(),
      this.loadClasses()
    ]);
  }

  async loadExams(): Promise<void> {
    this.loading = true;
    try {
      this.exams = await this.examService.getTeacherExams();
      this.applyFilters();
    } catch (error) {
      this.notification.error('حدث خطأ في تحميل الاختبارات');
    } finally {
      this.loading = false;
    }
  }

  async loadClasses(): Promise<void> {
    this.classes = await this.classService.getTeacherClasses();
  }

  applyFilters(): void {
    this.filteredExams = this.exams.filter(exam => {
      // Search filter
      if (this.searchTerm && !exam.title.includes(this.searchTerm) && 
          !exam.subject.includes(this.searchTerm)) {
        return false;
      }
      
      // Status filter
      if (this.selectedStatus !== 'all') {
        const now = new Date();
        const examDate = new Date(exam.date);
        
        if (this.selectedStatus === 'upcoming' && examDate < now) return false;
        if (this.selectedStatus === 'past' && examDate > now) return false;
        if (this.selectedStatus === 'today') {
          const isToday = examDate.toDateString() === now.toDateString();
          if (!isToday) return false;
        }
      }
      
      // Class filter
      if (this.selectedClass !== 'all' && exam.classRoomId.toString() !== this.selectedClass) {
        return false;
      }
      
      return true;
    });
  }

  getExamStatus(exam: Exam): { text: string; class: string } {
    const now = new Date();
    const examDate = new Date(exam.date);
    const examEnd = new Date(exam.endTime);
    
    if (now > examEnd) {
      return { text: 'منتهي', class: 'badge danger' };
    } else if (now >= examDate && now <= examEnd) {
      return { text: 'جاري', class: 'badge success' };
    } else {
      return { text: 'قادم', class: 'badge warning' };
    }
  }

  onCreateExam(): void {
    this.router.navigate(['/teacher/exams/create']);
  }

  onEditExam(exam: Exam): void {
    this.router.navigate(['/teacher/exams/edit', exam.id]);
  }

  onViewResults(exam: Exam): void {
    this.router.navigate(['/teacher/exams/results', exam.id]);
  }

  onDeleteExam(exam: Exam): void {
    if (confirm(`هل أنت متأكد من حذف الاختبار: ${exam.title}؟`)) {
      this.examService.deleteExam(exam.id).then(() => {
        this.notification.success('تم حذف الاختبار بنجاح');
        this.loadExams();
      }).catch(() => {
        this.notification.error('حدث خطأ في حذف الاختبار');
      });
    }
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.applyFilters();
  }
}