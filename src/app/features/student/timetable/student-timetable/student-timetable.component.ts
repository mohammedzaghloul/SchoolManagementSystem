import { Component, OnInit, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SessionService } from '../../../../core/services/session.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Session } from '../../../../core/models/session.model';

@Component({
  selector: 'app-student-timetable',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './student-timetable.component.html',
  styleUrl: './student-timetable.component.css'
})
export class StudentTimetableComponent implements OnInit {
  sessions: Session[] = [];
  isFullScreen = false;
  zoomLevel = 1;
  @ViewChild('timetableContent') timetableContent!: ElementRef;

  toggleFullScreen() {
    this.isFullScreen = !this.isFullScreen;
  }

  zoomIn() {
    this.zoomLevel += 0.1;
  }

  zoomOut() {
    if (this.zoomLevel > 0.5) {
      this.zoomLevel -= 0.1;
    }
  }

  resetZoom() {
    this.zoomLevel = 1;
  }

  async downloadTimetable() {
    if (this.loading) return;
    
    // Inject html2canvas if not already present
    if (!(window as any).html2canvas) {
      const script = document.createElement('script');
      script.src = 'https://cdnjs.cloudflare.com/ajax/libs/html2canvas/1.4.1/html2canvas.min.js';
      document.body.appendChild(script);
      
      await new Promise((resolve) => {
        script.onload = resolve;
      });
    }

    try {
      const element = this.timetableContent.nativeElement;
      const canvas = await (window as any).html2canvas(element, {
        backgroundColor: '#121421',
        scale: 2,
        useCORS: true,
        logging: false
      });
      
      const dataUrl = canvas.toDataURL('image/png');
      const link = document.createElement('a');
      link.href = dataUrl;
      link.download = `جدول-${this.studentName}-${new Date().toLocaleDateString('ar-EG')}.png`;
      link.click();
    } catch (err) {
      console.error('Download failed:', err);
      this.error = 'فشل في تحميل الجدول كصورة. حاول مرة أخرى.';
    }
  }

  daysOfWeek = ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس'];
  timeSlots = ['08:00', '09:00', '10:00', '11:00', '12:00', '13:00'];

  // Matrix: day -> timeslot -> Session | null
  timetableMatrix: { [day: string]: { [time: string]: Session | null } } = {};

  loading = false;
  error = '';
  studentName = '';

  constructor(
    private sessionService: SessionService,
    private auth: AuthService
  ) {
    // Initialize matrix
    this.daysOfWeek.forEach(day => {
      this.timetableMatrix[day] = {};
      this.timeSlots.forEach(time => {
        this.timetableMatrix[day][time] = null;
      });
    });
  }

  async ngOnInit() {
    this.loading = true;
    const user = this.auth.getCurrentUser();
    this.studentName = user?.fullName || 'عمر خالد';

    try {
      this.sessions = await this.sessionService.getAllSessions();
      this.buildMatrix();
    } catch (err) {
      this.error = 'فشل في تحميل الجدول الدراسي من الخادم.';
      console.error(err);
    } finally {
      this.loading = false;
    }
  }

  buildMatrix() {
    // Determine the day name from date/startTime from the API.
    // Ensure day fits within "daysOfWeek"
    const arabicDays = ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];

    this.sessions.forEach(session => {
      if (!session.startTime) return;
      const dateObj = new Date(session.startTime);
      const dayName = arabicDays[dateObj.getDay()];

      const hours = dateObj.getHours().toString().padStart(2, '0');
      // Snap to nearest timeslot 08, 09, 10, 11, 12, 13
      const timeSlot = `${hours}:00`;

      if (this.daysOfWeek.includes(dayName) && this.timeSlots.includes(timeSlot)) {
        this.timetableMatrix[dayName][timeSlot] = session;
      }
    });
  }

  getSubjectColor(subjectName: string | undefined): string {
    if (!subjectName) return 'bg-dark';

    if (subjectName.includes('رياضيات')) return 'bg-blue';
    if (subjectName.includes('علوم')) return 'bg-green';
    if (subjectName.includes('عربية')) return 'bg-purple';
    if (subjectName.includes('إنجليزية')) return 'bg-teal';
    if (subjectName.includes('دراسات')) return 'bg-dark-gray';
    if (subjectName.includes('دين')) return 'bg-dark-gray';
    if (subjectName.includes('حاسب')) return 'bg-oxford';
    if (subjectName.includes('نشاط')) return 'bg-gray-blue';

    return 'bg-blue'; // default
  }

  getSubjectIcon(subjectName: string | undefined): string {
    if (!subjectName) return '';
    const name = subjectName.toLowerCase();
    
    if (name.includes('رياضيات')) return 'fa-calculator';
    if (name.includes('علوم')) return 'fa-flask';
    if (name.includes('عربية')) return 'fa-feather';
    if (name.includes('إنجليزية')) return 'fa-language';
    if (name.includes('دراسات')) return 'fa-globe-americas';
    if (name.includes('دين')) return 'fa-mosque';
    if (name.includes('حاسب')) return 'fa-laptop-code';
    
    return 'fa-book';
  }
}
