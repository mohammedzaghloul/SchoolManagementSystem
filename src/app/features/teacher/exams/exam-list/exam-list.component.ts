import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ExamService } from '../../../../core/services/exam.service';
import { ClassRoomService } from '../../../../core/services/classroom.service';
import { Exam } from '../../../../core/models/exam.model';
import { ClassRoom } from '../../../../core/models/class.model';

@Component({
    selector: 'app-exam-list',
    standalone: true,
    imports: [CommonModule, RouterModule, FormsModule],
    templateUrl: './exam-list.component.html',
    styleUrls: ['./exam-list.component.css']
})
export class ExamListComponent implements OnInit {
    exams: Exam[] = [];
    filteredExams: Exam[] = [];
    classes: ClassRoom[] = [];
    loading = false;
    searchTerm = '';
    selectedStatus = 'all';
    selectedClass = 'all';

    constructor(
        private examService: ExamService,
        private classService: ClassRoomService,
        private router: Router
    ) { }

    async ngOnInit() {
        await Promise.all([
            this.loadExams(),
            this.loadClasses()
        ]);
    }

    async loadExams() {
        this.loading = true;
        try {
            const res = await this.examService.getTeacherExams();
            this.exams = res.map(e => {
                // Ensure date and startTime are set correctly from backend
                const examDate = e.startTime ? new Date(e.startTime) : new Date();
                
                // Calculate status if not provided by backend
                if (!e.status) {
                    const now = new Date();
                    const start = new Date(e.startTime || '');
                    const end = new Date(e.endTime || '');
                    
                    if (now < start) e.status = 'upcoming';
                    else if (now >= start && now <= end) e.status = 'active';
                    else e.status = 'past';
                }

                return {
                    ...e,
                    date: examDate.toISOString(),
                    totalMarks: e.maxScore || e.totalMarks // Handle MaxScore from backend
                };
            });
            this.applyFilters();
        } catch (err) {
            console.error('Failed to load exams', err);
        } finally {
            this.loading = false;
        }
    }

    async loadClasses() {
        try {
            const res: any = await this.classService.getTeacherClasses(0);
            this.classes = Array.isArray(res) ? res : res.data || [];
        } catch { }
    }

    applyFilters() {
        const term = (this.searchTerm || '').toLowerCase().trim();
        const status = this.selectedStatus || 'all';
        const classId = (this.selectedClass || 'all').toString();

        this.filteredExams = (this.exams || []).filter(exam => {
            // Search Filter
            const matchesSearch = !term ||
                (exam.title && exam.title.toLowerCase().includes(term)) ||
                (exam.subjectName && exam.subjectName.toLowerCase().includes(term)) ||
                (exam.description && exam.description.toLowerCase().includes(term));

            // Status Filter
            const matchesStatus = status === 'all' || exam.status === status;

            // Class Filter
            // Match against both classRoomId and potentially classId from backend
            const examClassId = (exam.classRoomId || (exam as any).classId || '').toString();
            const matchesClass = classId === 'all' || examClassId === classId;

            return matchesSearch && matchesStatus && matchesClass;
        });
    }

    onCreateExam() {
        this.router.navigate(['/teacher/exams/create']);
    }

    onEditExam(exam: Exam) {
        this.router.navigate(['/teacher/exams/edit', exam.id]);
    }

    async onDeleteExam(exam: Exam) {
        if (!confirm(`هل أنت متأكد من حذف الاختبار: ${exam.title}؟`)) return;
        try {
            await this.examService.deleteExam(exam.id);
            this.exams = this.exams.filter(e => e.id !== exam.id);
            this.applyFilters();
        } catch {
            alert('خطأ في حذف الاختبار');
        }
    }

    onViewResults(exam: Exam) {
        this.router.navigate(['/teacher/exams/results', exam.id]);
    }

    getStatusClass(status?: string): string {
        switch (status) {
            case 'upcoming': return 'badge-upcoming';
            case 'active': return 'badge-active';
            case 'past': return 'badge-done';
            default: return 'bg-secondary';
        }
    }

    getStatusText(status?: string): string {
        switch (status) {
            case 'upcoming': return 'قادم';
            case 'active': return 'جاري';
            case 'past': return 'منتهي';
            default: return 'غير محدد';
        }
    }
}
