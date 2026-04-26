import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { CreateParentRequest, Parent } from '../../../../core/models/parent.model';
import { ParentService } from '../../../../core/services/parent.service';

@Component({
  selector: 'app-parent-management',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, MatDialogModule],
  templateUrl: './parent-management.component.html',
  styleUrls: ['./parent-management.component.css']
})
export class ParentManagementComponent implements OnInit, OnDestroy {
  parents: Parent[] = [];
  filteredParents: Parent[] = [];
  loading = false;
  submitting = false;
  error = '';
  searchTerm = '';

  showModal = false;
  isEditMode = false;
  currentParent: Partial<Parent> & { password?: string } = this.createEmptyParent();

  constructor(
    private parentService: ParentService,
    private dialog: MatDialog
  ) {}

  async ngOnInit(): Promise<void> {
    await this.loadParents();
  }

  ngOnDestroy(): void {
    document.body.classList.remove('modal-open-fix');
  }

  async loadParents(): Promise<void> {
    this.loading = true;
    this.error = '';

    try {
      const data = await this.parentService.getParents();
      // Default sorting by ID descending
      this.parents = data.sort((a, b) => b.id - a.id);
      this.applyFilter();
    } catch (err: any) {
      this.error = err?.message || 'حدث خطأ في تحميل أولياء الأمور.';
    } finally {
      this.loading = false;
    }
  }

  applyFilter(): void {
    const term = this.searchTerm.trim().toLowerCase();

    if (!term) {
      this.filteredParents = [...this.parents];
      return;
    }

    this.filteredParents = this.parents.filter(parent =>
      (parent.fullName || '').toLowerCase().includes(term) ||
      (parent.email || '').toLowerCase().includes(term) ||
      (parent.phone || '').toLowerCase().includes(term)
    );
  }

  openAddModal(): void {
    this.isEditMode = false;
    this.currentParent = this.createEmptyParent();
    this.showModal = true;
    document.body.classList.add('modal-open-fix');
  }

  openEditModal(parent: Parent): void {
    this.isEditMode = true;
    this.currentParent = {
      id: parent.id,
      fullName: parent.fullName,
      email: parent.email,
      phone: parent.phone,
      address: parent.address,
      childrenCount: parent.childrenCount
    };
    this.showModal = true;
    document.body.classList.add('modal-open-fix');
  }

  closeModal(): void {
    this.showModal = false;
    document.body.classList.remove('modal-open-fix');
  }

  async saveParent(): Promise<void> {
    if (!this.currentParent.fullName || !this.currentParent.email) {
      alert('يرجى إدخال اسم ولي الأمر والبريد الإلكتروني.');
      return;
    }

    this.submitting = true;

    try {
      if (this.isEditMode && this.currentParent.id) {
        await this.parentService.updateParent(this.currentParent.id, {
          fullName: this.currentParent.fullName,
          phone: this.currentParent.phone,
          address: this.currentParent.address
        });
      } else {
        const payload: CreateParentRequest = {
          fullName: this.currentParent.fullName,
          email: this.currentParent.email,
          phone: this.currentParent.phone,
          address: this.currentParent.address,
          password: this.currentParent.password || 'Parent@123'
        };

        await this.parentService.createParent(payload);
      }

      await this.loadParents();
      this.closeModal();
    } catch (err: any) {
      alert('خطأ في الحفظ: ' + (err?.error?.message || err?.message || 'تعذر حفظ بيانات ولي الأمر.'));
    } finally {
      this.submitting = false;
    }
  }

  deleteParent(parent: Parent): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'تأكيد الحذف',
        message: `هل أنت متأكد من حذف ولي الأمر ${parent.fullName}؟`,
        confirmText: 'حذف',
        cancelText: 'إلغاء',
        color: 'warn'
      } as ConfirmDialogData
    });

    dialogRef.afterClosed().subscribe(async (result) => {
      if (!result) {
        return;
      }

      try {
        await this.parentService.deleteParent(parent.id);
        this.parents = this.parents.filter(item => item.id !== parent.id);
        this.applyFilter();
      } catch (err: any) {
        alert('تعذر حذف ولي الأمر: ' + (err?.error?.message || err?.message || 'يرجى المحاولة مرة أخرى.'));
      }
    });
  }

  private createEmptyParent(): Partial<Parent> & { password?: string } {
    return {
      fullName: '',
      email: '',
      phone: '',
      address: '',
      password: 'Parent@123'
    };
  }
}
