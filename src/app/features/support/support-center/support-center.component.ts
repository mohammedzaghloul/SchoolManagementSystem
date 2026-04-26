import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

interface GuideStep {
  title: string;
  description: string;
  icon: string;
}

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
  currentStepIndex = 0;

  readonly guideSteps: GuideStep[] = [
    {
      title: 'مرحباً بك في النظام!',
      description: 'هذه الجولة ستأخذك في رحلة سريعة للتعرف على أهم ميزات نظام إدارة المدرسة.',
      icon: 'fa-school'
    },
    {
      title: 'لوحة التحكم',
      description: 'من لوحة التحكم ترى ملخصاً كاملاً لكل شيء: الحضور، المصروفات، الجدول، والإشعارات.',
      icon: 'fa-gauge-high'
    },
    {
      title: 'تسجيل الحضور',
      description: 'من قسم الحضور يمكن تسجيل الطلاب عبر QR أو Face ID أو يدوياً حسب إعدادات كل حصة.',
      icon: 'fa-qrcode'
    },
    {
      title: 'إدارة المصروفات',
      description: 'يمكن للأدمن رفع الفواتير على الطلاب ومتابعة التحصيل ومراقعة المتأخرات من شاشة واحدة.',
      icon: 'fa-coins'
    },
    {
      title: 'جدول الحصص',
      description: 'أنشئ الحصص يدوياً أو اضغط "تجهيز حصص الترم" ليقوم النظام بتوليد الجدول تلقائياً بدون تعارضات.',
      icon: 'fa-calendar-days'
    },
    {
      title: 'أنت الآن جاهز! 🎉',
      description: 'تعرفت على أهم ميزات النظام. يمكنك العودة لهذا الدليل في أي وقت عبر قسم "الدليل والمساعدة".',
      icon: 'fa-circle-check'
    }
  ];

  constructor(private authService: AuthService) {
    this.currentRole = this.authService.getCurrentUser()?.role || '';
  }

  get currentStep(): GuideStep {
    return this.guideSteps[this.currentStepIndex];
  }

  get isLastStep(): boolean {
    return this.currentStepIndex === this.guideSteps.length - 1;
  }

  get isFirstStep(): boolean {
    return this.currentStepIndex === 0;
  }

  get progressPercent(): number {
    return Math.round(((this.currentStepIndex + 1) / this.guideSteps.length) * 100);
  }

  startGuide(): void {
    this.currentStepIndex = 0;
    this.showGuide = true;
  }

  nextStep(): void {
    if (this.isLastStep) {
      this.closeGuide();
    } else {
      this.currentStepIndex++;
    }
  }

  prevStep(): void {
    if (!this.isFirstStep) {
      this.currentStepIndex--;
    }
  }

  closeGuide(): void {
    this.showGuide = false;
    this.currentStepIndex = 0;
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
