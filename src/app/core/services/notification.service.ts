import { Injectable } from '@angular/core';
import { ToastrService } from 'ngx-toastr';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  constructor(private toastr: ToastrService) { }

  success(message: string, title: string = 'نجاح') {
    this.toastr.success(message, title);
  }

  error(message: string, title: string = 'خطأ') {
    this.toastr.error(message, title);
  }

  warning(message: string, title: string = 'تنبيه') {
    this.toastr.warning(message, title);
  }

  info(message: string, title: string = 'معلومات') {
    this.toastr.info(message, title);
  }
}