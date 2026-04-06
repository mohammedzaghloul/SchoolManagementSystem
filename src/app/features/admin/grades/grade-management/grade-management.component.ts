import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GradeService } from '../../../../core/services/grade.service';
import { GradeLevel } from '../../../../core/models/grade.model';

@Component({
    selector: 'app-grade-management',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './grade-management.component.html',
    styleUrls: ['./grade-management.component.css']
})
export class GradeManagementComponent implements OnInit {
    grades: GradeLevel[] = [];
    loading = false;
    submitting = false;
    showModal = false;
    isEditMode = false;
    currentGrade: any = { name: '', description: '' };
    searchTerm = '';
    filteredGrades: GradeLevel[] = [];

    constructor(private gradeService: GradeService) { }

    async ngOnInit() {
        await this.loadGrades();
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
    }

    openEditModal(grade: GradeLevel) {
        this.isEditMode = true;
        this.currentGrade = { ...grade };
        this.showModal = true;
    }

    closeModal() {
        this.showModal = false;
    }

    async saveGrade() {
        this.submitting = true;
        try {
            if (this.isEditMode) {
                await this.gradeService.updateGrade(this.currentGrade.id, this.currentGrade);
            } else {
                await this.gradeService.createGrade(this.currentGrade);
            }
            await this.loadGrades();
            this.closeModal();
        } catch (err: any) {
            alert('خطأ أثناء الحفظ');
        } finally {
            this.submitting = false;
        }
    }

    async deleteGrade(id: number) {
        if (!confirm('هل أنت متأكد من حذف هذا المستوى؟')) return;
        try {
            await this.gradeService.deleteGrade(id);
            this.grades = this.grades.filter(g => g.id !== id);
        } catch (err) {
            alert('خطأ أثناء الحذف');
        }
    }
}
