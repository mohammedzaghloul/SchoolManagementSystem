import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SubjectService } from '../../../../core/services/subject.service';
import { ClassRoomService } from '../../../../core/services/classroom.service';
import { Subject } from '../../../../core/models/subject.model';

import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';

@Component({
    selector: 'app-subject-management',
    standalone: true,
    imports: [CommonModule, FormsModule, MatDialogModule],
    templateUrl: './subject-management.component.html',
    styleUrls: ['./subject-management.component.css']
})
export class SubjectManagementComponent implements OnInit {
    subjects: Subject[] = [];
    classRooms: any[] = [];
    loading = false;
    submitting = false;
    showModal = false;
    isEditMode = false;
    currentSubject: any = { name: '', code: '', description: '', classRoomId: null, term: 'الترم الأول', isActive: true };
    searchTerm: string = '';
    selectedTerm: string = 'all';
    readonly terms = ['الترم الأول', 'الترم الثاني'];
    filteredSubjects: Subject[] = [];

    constructor(
        private subjectService: SubjectService,
        private classRoomService: ClassRoomService,
        private dialog: MatDialog
    ) { }

    async ngOnInit() {
        await Promise.all([
            this.loadSubjects(),
            this.loadClassRooms()
        ]);
    }

    async loadClassRooms() {
        try {
            this.classRooms = await this.classRoomService.getAll();
        } catch { }
    }

    getClassRoomName(id: number | undefined): string {
        if (id === undefined || id === null) return 'عام / أساسي';
        return this.classRooms.find(c => c.id === id)?.name || 'عام / أساسي';
    }

    async loadSubjects() {
        this.loading = true;
        try {
            const data = await this.subjectService.getAll();
            this.subjects = data.map(subject => ({
                ...subject,
                term: subject.term || 'الترم الأول',
                isActive: subject.isActive !== false
            }));
            this.applyFilter();
        } catch { }
        finally { this.loading = false; }
    }

    applyFilter() {
        let filtered = [...this.subjects];

        if (this.selectedTerm !== 'all') {
            filtered = filtered.filter(s => (s.term || 'الترم الأول') === this.selectedTerm);
        }

        if (this.searchTerm.trim()) {
            const term = this.searchTerm.toLowerCase().trim();
            filtered = filtered.filter(s => 
                s.name.toLowerCase().includes(term) || 
                (s.code && s.code.toLowerCase().includes(term))
            );
        }

        this.filteredSubjects = filtered;
    }

    openAddModal() {
        this.isEditMode = false;
        this.currentSubject = { name: '', code: '', description: '', classRoomId: null, term: 'الترم الأول', isActive: true };
        this.showModal = true;
    }

    openEditModal(item: Subject) {
        this.isEditMode = true;
        this.currentSubject = { ...item };
        this.showModal = true;
    }

    async saveSubject() {
        if (!this.currentSubject.name || !this.currentSubject.code) {
            alert('يرجى إدخال اسم المادة وكود المادة');
            return;
        }
        this.submitting = true;
        try {
            if (this.isEditMode) {
                await this.subjectService.update(this.currentSubject.id, this.currentSubject);
            } else {
                await this.subjectService.create(this.currentSubject);
            }
            await this.loadSubjects();
            this.showModal = false;
        } catch {
            alert('خطأ في الحفظ');
        } finally {
            this.submitting = false;
        }
    }

    deleteSubject(id: number) {
        const dialogRef = this.dialog.open(ConfirmDialogComponent, {
            width: '400px',
            data: {
                title: 'تأكيد الحذف',
                message: 'هل أنت متأكد من حذف هذه المادة؟ قد يكون هناك معلمين وجداول مرتبطة بها.',
                confirmText: 'حذف المادة',
                cancelText: 'إلغاء',
                color: 'warn'
            } as ConfirmDialogData
        });

        dialogRef.afterClosed().subscribe(async (result) => {
            if (result) {
                try {
                    await this.subjectService.delete(id);
                    this.subjects = this.subjects.filter(s => s.id !== id);
                } catch (err: any) {
                    alert('عفواً، لا يمكن حذف هذه المادة لأنها مرتبطة بجداول دراسية أو معلمين. يجب حذف الارتباط أولاً.');
                }
            }
        });
    }
}
