import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-support-center',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './support-center.component.html',
  styleUrls: ['./support-center.component.css']
})
export class SupportCenterComponent {
  currentRole = '';
  openFaqIndex: number | null = null;
  showGuide = false;

  constructor(private authService: AuthService) {
    this.currentRole = this.authService.getCurrentUser()?.role || '';
  }

  startGuide(): void {
    this.showGuide = true;
  }

  closeGuide(): void {
    this.showGuide = false;
  }

  faqs = [
    {
      question: 'كيف يمكنني تسجيل حضور الطالب؟',
      answer: 'من واجهة الطالب، يمكن مسح كود QR أو تأكيد الحضور من خلال الكاميرا حسب إعدادات الحصة.'
    },
    {
      question: 'كيف أتواصل مع المعلمين؟',
      answer: 'استخدم أيقونة "التواصل" الموجودة في القائمة الجانبية أو السفلية لفتح المحادثات المباشرة، أو اضغط زر "كلم المدرسين" في الرئيسية.'
    },
    {
      question: 'أين أجد سجل المصروفات؟',
      answer: 'بالنسبة لأولياء الأمور، يمكنك الضغط على قسم "المصروفات" من لوحة التحكم لمراجعة الفواتير وسجل المدفوعات.'
    },
    {
      question: 'كيف أغير كلمة المرور؟',
      answer: 'من صفحة الملف الشخصي ستجد قسمًا خاصًا بتغيير كلمة المرور. أدخل كلمة المرور الحالية ثم الجديدة، ويمكنك فقط إظهار ما تكتبه أثناء الإدخال، وليس عرض كلمة المرور المحفوظة نفسها.'
    }
  ];

  toggleFaq(index: number): void {
    this.openFaqIndex = this.openFaqIndex === index ? null : index;
  }
}
