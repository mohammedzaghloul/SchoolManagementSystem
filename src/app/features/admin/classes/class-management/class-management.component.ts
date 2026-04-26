import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ClassRoomService } from '../../../../core/services/classroom.service';
import { GradeService } from '../../../../core/services/grade.service';
import { ClassRoom } from '../../../../core/models/class.model';
import { GradeLevel } from '../../../../core/models/grade.model';

import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';

@Component({
    selector: 'app-class-management',
    standalone: true,
    imports: [CommonModule, FormsModule, MatDialogModule],
    templateUrl: './class-management.component.html',
    styleUrls: ['./class-management.component.css']
})
export class ClassManagementComponent implements OnInit, OnDestroy {
    classes: ClassRoom[] = [];
    grades: GradeLevel[] = [];
    loading = false;
    submitting = false;
    showModal = false;
    isEditMode = false;
    currentClass: any = { name: '', gradeLevelId: null, capacity: 30 };
    searchTerm = '';
    selectedGrade = 'all';
    filteredClasses: ClassRoom[] = [];

    constructor(
        private classService: ClassRoomService,
        private gradeService: GradeService,
        private dialog: MatDialog
    ) { }

    async ngOnInit() {
        await Promise.all([this.loadClasses(), this.loadGrades()]);
    }

    ngOnDestroy(): void {
        document.body.classList.remove('modal-open-fix');
    }

    async loadClasses() {
        this.loading = true;
        try {
            this.classes = await this.classService.getAll();
            this.applyFilter();
        } catch (err) {
            console.error('Failed to load classes', err);
        } finally {
            this.loading = false;
        }
    }

    applyFilter() {
        let filtered = [...this.classes];

        if (this.searchTerm) {
            const q = this.searchTerm.toLowerCase();
            filtered = filtered.filter(c => c.name.toLowerCase().includes(q));
        }

        if (this.selectedGrade !== 'all') {
            filtered = filtered.filter(c => c.gradeLevelId == Number(this.selectedGrade));
        }

        this.filteredClasses = filtered;
    }

    async loadGrades() {
        try {
            const allGrades = await this.gradeService.getGrades();
            const uniqueGrades = [];
            const seen = new Set();
            for (const g of allGrades) {
                if (!seen.has(g.name)) {
                    seen.add(g.name);
                    uniqueGrades.push(g);
                }
            }
            this.grades = uniqueGrades;
        } catch { }
    }

    openAddModal() {
        this.isEditMode = false;
        this.currentClass = { name: '', gradeLevelId: this.grades[0]?.id || null, capacity: 30 };
        this.showModal = true;
        document.body.classList.add('modal-open-fix');
    }

    openEditModal(item: ClassRoom) {
        this.isEditMode = true;
        this.currentClass = { ...item };
        this.showModal = true;
        document.body.classList.add('modal-open-fix');
    }

    closeModal(): void {
        this.showModal = false;
        document.body.classList.remove('modal-open-fix');
    }

    async saveClass() {
        if (!this.currentClass.name || !this.currentClass.gradeLevelId) {
            alert('يرجى إدخال اسم الفصل وتحديد المستوى المتاح');
            return;
        }
        this.submitting = true;
        try {
            if (this.isEditMode) {
                const updatePayload = {
                    id: this.currentClass.id,
                    name: this.currentClass.name,
                    gradeLevelId: Number(this.currentClass.gradeLevelId),
                    capacity: this.currentClass.capacity
                };
                await this.classService.update(this.currentClass.id, updatePayload);
            } else {
                await this.classService.create(this.currentClass);
            }
            await this.loadClasses();
            this.closeModal();
        } catch (err: any) {
            alert('خطأ في الحفظ');
        } finally {
            this.submitting = false;
        }
    }

    deleteClass(id: number) {
        const dialogRef = this.dialog.open(ConfirmDialogComponent, {
            width: '400px',
            data: {
                title: 'تأكيد الحذف',
                message: 'هل أنت متأكد من حذف هذا الفصل الدراسي؟ قد يكون هناك طلاب وجداول مرتبطة به.',
                confirmText: 'حذف الفصل',
                cancelText: 'إلغاء',
                color: 'warn'
            } as ConfirmDialogData
        });

        dialogRef.afterClosed().subscribe(async (result) => {
            if (result) {
                try {
                    await this.classService.delete(id);
                    this.classes = this.classes.filter(c => c.id !== id);
                    this.applyFilter(); // Update UI immediately
                } catch (err: any) {
                    alert('عفواً، لا يمكن حذف هذا الفصل لأنه مرتبط بطلاب أو مواد دراسية. يجب نقل الطلاب وحذف الارتباطات أولاً.');
                }
            }
        });
    }

    getGradeName(id: number): string {
        return this.grades.find(g => g.id === id)?.name || 'غير محدد';
    }
}
