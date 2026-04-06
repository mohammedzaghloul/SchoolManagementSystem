import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SessionService } from '../../../../core/services/session.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Session } from '../../../../core/models/session.model';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-teacher-timetable',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './teacher-timetable.component.html',
  styleUrl: './teacher-timetable.component.css'
})
export class TeacherTimetableComponent implements OnInit {
  sessions: Session[] = [];

  daysOfWeek = ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس'];
  timeSlots = ['08:00', '09:00', '10:00', '11:00', '12:00', '13:00'];

  // Matrix: day -> timeslot -> Session | null
  timetableMatrix: { [day: string]: { [time: string]: Session | null } } = {};

  loading = false;
  error = '';
  teacherName = '';

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
    this.teacherName = user?.fullName || 'المعلم';

    try {
      this.sessions = await this.sessionService.getAllSessions();
      this.buildMatrix();
    } catch (err) {
      this.error = 'فشل في تحميل الجدول الدراسي من قاعدة البيانات.';
      console.error(err);
    } finally {
      this.loading = false;
    }
  }

  buildMatrix() {
    const arabicDays = ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];

    this.sessions.forEach(session => {
      if (!session.startTime) return;
      const dateObj = new Date(session.startTime);
      const dayIndex = dateObj.getDay();
      const dayName = arabicDays[dayIndex];

      const hours = dateObj.getHours().toString().padStart(2, '0');
      const minutes = dateObj.getMinutes().toString().padStart(2, '0');
      const timeSlot = `${hours}:00`; // Simplify slotting

      if (this.daysOfWeek.includes(dayName) && this.timeSlots.includes(timeSlot)) {
        this.timetableMatrix[dayName][timeSlot] = session;
      }
    });
  }

  getSubjectColor(subjectName: string | undefined): string {
    if (!subjectName) return 'bg-gradient-blue';

    if (subjectName.includes('رياضيات')) return 'bg-gradient-blue';
    if (subjectName.includes('علوم')) return 'bg-gradient-green';
    if (subjectName.includes('عربية')) return 'bg-gradient-purple';
    if (subjectName.includes('إنجليزية') || subjectName.includes('English')) return 'bg-gradient-orange';
    
    return 'bg-gradient-rose'; // Default for others
  }
}
