import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GradeService } from '../../../../core/services/grade.service';
import { GradeLevel } from '../../../../core/models/grade.model';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';

@Component({
    selector: 'app-grade-management',
    standalone: true,
    imports: [CommonModule, FormsModule, MatDialogModule],
    templateUrl: './grade-management.component.html',
    styleUrls: ['./grade-management.component.css']
})
export class GradeManagementComponent implements OnInit, OnDestroy {
    grades: GradeLevel[] = [];
    loading = false;
    submitting = false;
    showModal = false;
    isEditMode = false;
    currentGrade: any = { name: '', description: '' };
    searchTerm = '';
    filteredGrades: GradeLevel[] = [];

    constructor(
        private gradeService: GradeService,
        private dialog: MatDialog
    ) { }

    async ngOnInit() {
        await this.loadGrades();
    }

    ngOnDestroy(): void {
        document.body.classList.remove('modal-open-fix');
    }

    async loadGrades() {
        this.loading = true;
        try {
            this.grades = await this.gradeService.getGrades();
            this.applyFilter();
        } catch (err) {
            console.error('Failed to load grades', err);
        } finally {
            this.loading = false;
        }
    }

    applyFilter() {
        if (!this.searchTerm.trim()) {
            this.filteredGrades = [...this.grades];
        } else {
            const q = this.searchTerm.toLowerCase();
            this.filteredGrades = this.grades.filter(g => 
                g.name.toLowerCase().includes(q) || 
                (g.description && g.description.toLowerCase().includes(q))
            );
        }
    }

    openAddModal() {
        this.isEditMode = false;
        this.currentGrade = { name: '', description: '' };
        this.showModal = true;
        document.body.classList.add('modal-open-fix');
    }

    openEditModal(grade: GradeLevel) {
        this.isEditMode = true;
        this.currentGrade = { ...grade };
        this.showModal = true;
        document.body.classList.add('modal-open-fix');
    }

    closeModal() {
        this.showModal = false;
        document.body.classList.remove('modal-open-fix');
    }

    async saveGrade() {
        if (!this.currentGrade.name) {
            alert('يرجى إدخال اسم المستوى الدراسي');
            return;
        }
        this.submitting = true;
        try {
            const payload = { ...this.currentGrade };
            if (this.isEditMode) {
                await this.gradeService.updateGrade(payload.id, payload);
            } else {
                delete payload.id; // Clean up id if it exists
                await this.gradeService.createGrade(payload);
            }
            await this.loadGrades();
            this.closeModal();
        } catch (err: any) {
            alert('خطأ أثناء الحفظ: ' + (err.error?.message || err.message));
        } finally {
            this.submitting = false;
        }
    }

    deleteGrade(id: number) {
        const dialogRef = this.dialog.open(ConfirmDialogComponent, {
            width: '400px',
            data: {
                title: 'تأكيد الحذف',
                message: 'هل أنت متأكد من حذف هذا المستوى الدراسي؟ سيتم حذف كافة الارتباطات المرتبطة به.',
                confirmText: 'حذف',
                cancelText: 'إلغاء',
                color: 'warn'
            } as ConfirmDialogData
        });

        dialogRef.afterClosed().subscribe(async (result) => {
            if (result) {
                try {
                    await this.gradeService.deleteGrade(id);
                    this.grades = this.grades.filter(g => g.id !== id);
                    this.applyFilter();
                } catch (err: any) {
                    alert('عفواً، لا يمكن حذف هذا المستوى لأنه مرتبط ببيانات أخرى (فصول أو طلاب).');
                }
            }
        });
    }
}
