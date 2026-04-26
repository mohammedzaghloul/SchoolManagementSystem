import { Component, EventEmitter, Input, Output, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { animate, style, transition, trigger } from '@angular/animations';

export type ModalType = 'success' | 'error' | 'warning' | 'info' | 'confirm';

@Component({
  selector: 'app-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './modal.component.html',
  styleUrls: ['./modal.component.css'],
  animations: [
    trigger('backdropAnimation', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('250ms ease-out', style({ opacity: 1 }))
      ]),
      transition(':leave', [
        animate('200ms ease-in', style({ opacity: 0 }))
      ])
    ]),
    trigger('modalAnimation', [
      transition(':enter', [
        style({ opacity: 0, transform: 'scale(0.9) translateY(15px)' }),
        animate('300ms cubic-bezier(0.175, 0.885, 0.32, 1.275)', style({ opacity: 1, transform: 'scale(1) translateY(0)' }))
      ]),
      transition(':leave', [
        animate('200ms ease-in', style({ opacity: 0, transform: 'scale(0.95) translateY(10px)' }))
      ])
    ])
  ]
})
export class ModalComponent {
  @Input() isOpen = false;
  @Input() title = '';
  @Input() message = '';
  @Input() type: ModalType = 'info';
  @Input() confirmText = 'تأكيد';
  @Input() cancelText = 'إلغاء';
  @Input() showCancel = false;

  @Output() confirm = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  get iconClass(): string {
    switch (this.type) {
      case 'success': return 'fas fa-check text-emerald-500';
      case 'error': return 'fas fa-xmark text-rose-500';
      case 'warning': return 'fas fa-exclamation text-amber-500';
      case 'confirm': return 'fas fa-question text-blue-500';
      default: return 'fas fa-info text-sky-500';
    }
  }

  get iconBgClass(): string {
    switch (this.type) {
      case 'success': return 'bg-emerald-100';
      case 'error': return 'bg-rose-100';
      case 'warning': return 'bg-amber-100';
      case 'confirm': return 'bg-blue-100';
      default: return 'bg-sky-100';
    }
  }

  get confirmBtnClass(): string {
    switch (this.type) {
      case 'success': return 'btn-success';
      case 'error': return 'btn-danger';
      case 'warning': return 'btn-warning';
      default: return 'btn-primary';
    }
  }

  close() {
    this.isOpen = false;
    this.closed.emit();
  }

  onConfirm() {
    this.confirm.emit();
    this.close();
  }

  onCancel() {
    this.cancel.emit();
    this.close();
  }

  @HostListener('document:keydown.escape', ['$event'])
  onEscapeKey() {
    if (this.isOpen) {
      this.onCancel();
    }
  }
}
