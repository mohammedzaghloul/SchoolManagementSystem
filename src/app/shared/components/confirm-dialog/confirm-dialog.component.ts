import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  color?: 'primary' | 'accent' | 'warn';
}

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule],
  template: `
    <section class="confirm-dialog" dir="rtl" [class.confirm-dialog--warn]="data.color === 'warn'">
      <div class="confirm-icon" aria-hidden="true">
        <i class="fas" [class.fa-triangle-exclamation]="data.color === 'warn'" [class.fa-circle-question]="data.color !== 'warn'"></i>
      </div>

      <div class="confirm-copy">
        <h2>{{ data.title }}</h2>
        <p>{{ data.message }}</p>
      </div>

      <div class="confirm-actions">
        <button type="button" class="dialog-btn dialog-btn--ghost" (click)="onCancel()">
          {{ data.cancelText || 'إلغاء' }}
        </button>
        <button type="button" class="dialog-btn" [class.dialog-btn--danger]="data.color === 'warn'" [class.dialog-btn--primary]="data.color !== 'warn'" (click)="onConfirm()">
          {{ data.confirmText || 'تأكيد' }}
        </button>
      </div>
    </section>
  `,
  styles: [`
    :host {
      display: block;
      width: min(440px, calc(100vw - 32px));
      direction: rtl;
    }

    .confirm-dialog {
      display: grid;
      justify-items: center;
      gap: 1rem;
      padding: 1.35rem;
      text-align: center;
      background: #fff;
      color: #0f172a;
    }

    .confirm-icon {
      width: 56px;
      height: 56px;
      display: grid;
      place-items: center;
      border: 1px solid #bfdbfe;
      border-radius: 16px;
      background: #eff6ff;
      color: #2563eb;
      font-size: 1.35rem;
    }

    .confirm-dialog--warn .confirm-icon {
      border-color: #fecaca;
      background: #fef2f2;
      color: #dc2626;
    }

    .confirm-copy {
      display: grid;
      gap: 0.5rem;
      max-width: 360px;
    }

    .confirm-copy h2 {
      margin: 0;
      color: #0f172a;
      font-size: 1.12rem;
      font-weight: 900;
      line-height: 1.55;
    }

    .confirm-copy p {
      margin: 0;
      color: #475569;
      font-size: 0.92rem;
      font-weight: 700;
      line-height: 1.85;
    }

    .confirm-actions {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 0.65rem;
      width: 100%;
      margin-top: 0.15rem;
    }

    .dialog-btn {
      min-height: 42px;
      border-radius: 10px;
      padding: 0 1rem;
      border: 1px solid transparent;
      font: inherit;
      font-weight: 900;
      cursor: pointer;
      transition: transform 0.16s ease, background 0.16s ease, border-color 0.16s ease;
    }

    .dialog-btn:hover {
      transform: translateY(-1px);
    }

    .dialog-btn--ghost {
      border-color: #cbd5e1;
      background: #fff;
      color: #334155;
    }

    .dialog-btn--primary {
      background: #2563eb;
      color: #fff;
      box-shadow: 0 10px 20px rgba(37, 99, 235, 0.16);
    }

    .dialog-btn--danger {
      background: #dc2626;
      color: #fff;
      box-shadow: 0 10px 20px rgba(220, 38, 38, 0.16);
    }

    @media (max-width: 520px) {
      .confirm-dialog {
        padding: 1rem;
      }

      .confirm-actions {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class ConfirmDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<ConfirmDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ConfirmDialogData
  ) { }

  onConfirm(): void {
    this.dialogRef.close(true);
  }

  onCancel(): void {
    this.dialogRef.close(false);
  }
}
