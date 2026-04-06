import { Component, OnInit } from '@angular/core';
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
}
