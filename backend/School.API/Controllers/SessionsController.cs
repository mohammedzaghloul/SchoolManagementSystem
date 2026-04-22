using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.API.Infrastructure;
using School.Application.Features.Sessions.Commands;
using School.Infrastructure.Data;
using System.Globalization;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
public class SessionsController : BaseApiController
{
    private readonly SchoolDbContext _context;

    public SessionsController(SchoolDbContext context)
    {
        _context = context;
    }

    private async Task<School.Domain.Entities.Student?> GetCurrentStudentAsync()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return null;
        }

        return await _context.Students
            .Include(s => s.ClassRoom)
                .ThenInclude(c => c!.GradeLevel)
            .FirstOrDefaultAsync(s => s.Email == userEmail);
    }

    [HttpPost("start")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<int>> StartSession(StartSessionCommand command)
    {
        var sessionId = await Mediator.Send(command);
        return Ok(sessionId);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSession([FromBody] CreateAdminSessionRequest? request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "بيانات الحصة غير مكتملة." });
        }

        if (request.TeacherId <= 0 || request.ClassRoomId <= 0 || request.SubjectId <= 0)
        {
            return BadRequest(new { message = "يُرجى تحديد المدرس والمادة والفصل قبل إنشاء الحصة." });
        }

        if (!TryParseSessionTime(request.StartTime, out var startTime) || !TryParseSessionTime(request.EndTime, out var endTime))
        {
            return BadRequest(new { message = "تعذر قراءة توقيت الحصة المحدد من القائمة." });
        }

        if (endTime <= startTime)
        {
            return BadRequest(new { message = "وقت نهاية الحصة يجب أن يكون بعد وقت البداية." });
        }

        var attendanceType = NormalizeAttendanceType(request.AttendanceType);
        if (attendanceType == null)
        {
            return BadRequest(new { message = "نوع الرصد غير مدعوم. اختر QR أو Face أو Manual." });
        }

        var sessionDate = request.SessionDate.Date;

        var teacher = await _context.Teachers
            .AsNoTracking()
            .FirstOrDefaultAsync(currentTeacher => currentTeacher.Id == request.TeacherId && currentTeacher.IsActive);

        if (teacher == null)
        {
            return NotFound(new { message = "المدرس المحدد غير موجود أو غير نشط." });
        }

        var classRoom = await _context.ClassRooms
            .AsNoTracking()
            .Include(currentClassRoom => currentClassRoom.GradeLevel)
            .FirstOrDefaultAsync(currentClassRoom => currentClassRoom.Id == request.ClassRoomId);

        if (classRoom == null)
        {
            return NotFound(new { message = "الفصل المحدد غير موجود." });
        }

        var subject = await _context.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(currentSubject => currentSubject.Id == request.SubjectId && currentSubject.IsActive);

        if (subject == null)
        {
            return NotFound(new { message = "المادة المحددة غير موجودة أو غير مفعلة." });
        }

        if (subject.TeacherId.HasValue && subject.TeacherId.Value != teacher.Id)
        {
            return BadRequest(new { message = "هذه المادة ليست مرتبطة بالمدرس المختار." });
        }

        if (subject.ClassRoomId.HasValue && subject.ClassRoomId.Value != classRoom.Id)
        {
            return BadRequest(new { message = "هذه المادة ليست مرتبطة بالفصل المختار." });
        }

        var hasConflict = await _context.Sessions.AnyAsync(session =>
            session.SessionDate == sessionDate &&
            (session.TeacherId == teacher.Id || session.ClassRoomId == classRoom.Id) &&
            session.StartTime < endTime &&
            startTime < session.EndTime);

        if (hasConflict)
        {
            return Conflict(new { message = "يوجد تعارض في هذا الموعد مع حصة أخرى للمدرس أو للفصل." });
        }

        var session = new School.Domain.Entities.Session
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? $"حصة {subject.Name}" : request.Title.Trim(),
            SessionDate = sessionDate,
            StartTime = startTime,
            EndTime = endTime,
            AttendanceType = attendanceType,
            TeacherId = teacher.Id,
            ClassRoomId = classRoom.Id,
            SubjectId = subject.Id
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            session.Id,
            message = "تم إنشاء الحصة بنجاح.",
            sessionDate = session.SessionDate,
            startTime = session.SessionDate.Add(session.StartTime),
            endTime = session.SessionDate.Add(session.EndTime),
            teacherName = teacher.FullName ?? "غير محدد",
            classRoomName = classRoom.Name ?? "غير محدد",
            subjectName = subject.Name ?? "حصة دراسية",
            attendanceType = session.AttendanceType ?? "QR"
        });
    }

    [HttpGet("admin/schedule-overview")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetScheduleOverview([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string? term)
    {
        var rangeStart = (startDate ?? SchoolClock.Today).Date;
        var rangeEnd = (endDate ?? rangeStart.AddDays(42)).Date;
        var normalizedTerm = NormalizeTerm(term);

        if (rangeEnd < rangeStart)
        {
            return BadRequest(new { message = "End date must be on or after start date." });
        }

        var items = await _context.Sessions
            .Include(s => s.Subject)
            .Include(s => s.Teacher)
            .Include(s => s.ClassRoom)
                .ThenInclude(c => c.GradeLevel)
            .Include(s => s.ClassRoom)
                .ThenInclude(c => c.Students)
            .Include(s => s.Attendances)
            .Where(s => s.SessionDate >= rangeStart && s.SessionDate <= rangeEnd)
            .Where(s => normalizedTerm == null || (s.Subject != null && s.Subject.Term == normalizedTerm))
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                s.TeacherId,
                s.ClassRoomId,
                SessionDate = s.SessionDate,
                Term = s.Subject != null ? s.Subject.Term : null,
                SubjectName = s.Subject != null ? s.Subject.Name : "حصة دراسية",
                TeacherName = s.Teacher != null ? s.Teacher.FullName : "غير محدد",
                ClassRoomName = s.ClassRoom != null ? s.ClassRoom.Name : "غير محدد",
                GradeName = s.ClassRoom != null && s.ClassRoom.GradeLevel != null ? s.ClassRoom.GradeLevel.Name : "—",
                StartTime = s.SessionDate.Add(s.StartTime),
                EndTime = s.SessionDate.Add(s.EndTime),
                AttendanceType = s.AttendanceType ?? "QR",
                StudentCount = s.ClassRoom != null ? s.ClassRoom.Students.Count : 0,
                AttendanceCount = s.Attendances.Count(a => a.IsPresent)
            })
            .ToListAsync();

        return Ok(new
        {
            StartDate = rangeStart,
            EndDate = rangeEnd,
            Term = normalizedTerm ?? "all",
            TotalSessions = items.Count,
            TotalTeachers = items.Select(item => item.TeacherId).Distinct().Count(),
            TotalClasses = items.Select(item => item.ClassRoomId).Distinct().Count(),
            ScheduledToday = items.Count(item => item.SessionDate.Date == SchoolClock.Today),
            Items = items
        });
    }

    [HttpPost("admin/generate-term-schedule")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateTermSchedule([FromBody] GenerateTermScheduleRequest? request)
    {
        var rangeStart = (request?.StartDate ?? SchoolClock.Today).Date;
        var rangeEnd = (request?.EndDate ?? rangeStart.AddDays(42)).Date;
        var normalizedTerm = NormalizeTerm(request?.Term);

        if (rangeEnd < rangeStart)
        {
            return BadRequest(new { message = "End date must be on or after start date." });
        }

        var subjectsQuery = _context.Subjects
            .Where(subject => subject.IsActive && subject.TeacherId.HasValue && subject.ClassRoomId.HasValue)
            .AsQueryable();

        if (normalizedTerm != null)
        {
            subjectsQuery = subjectsQuery.Where(subject => subject.Term == normalizedTerm);
        }

        var subjects = await subjectsQuery.ToListAsync();

        if (subjects.Count == 0)
        {
            return BadRequest(new { message = "No active subjects are linked to teachers and classes yet." });
        }

        var existingSessions = await _context.Sessions
            .Where(session => session.SessionDate >= rangeStart && session.SessionDate <= rangeEnd)
            .ToListAsync();

        var totalPlannedSlots = SessionScheduleGenerator.CountPlannedSlots(subjects, rangeStart, rangeEnd);
        var generatedSessions = SessionScheduleGenerator.BuildMissingSessions(subjects, existingSessions, rangeStart, rangeEnd);

        if (generatedSessions.Count > 0)
        {
            _context.Sessions.AddRange(generatedSessions);
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            StartDate = rangeStart,
            EndDate = rangeEnd,
            Term = normalizedTerm ?? "all",
            PlannedSlots = totalPlannedSlots,
            CreatedCount = generatedSessions.Count,
            ExistingCount = Math.Max(totalPlannedSlots - generatedSessions.Count, 0),
            Message = generatedSessions.Count > 0
                ? "Schedule generated successfully."
                : "The selected date range is already fully scheduled."
        });
    }

    [HttpGet("teacher/{userId}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> GetTeacherSessions(string userId, [FromQuery] DateTime? date)
    {
        // Find teacher by matching their email from Identity OR by UserId
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var teacher = await _context.Teachers
            .FirstOrDefaultAsync(t => t.Email == userEmail || t.UserId == userId);

        if (teacher == null)
            return Ok(new List<object>()); // Return empty instead of 404

        var targetDate = (date ?? SchoolClock.Today).Date;
        var nextDay = targetDate.AddDays(1);
        var now = SchoolClock.Now;

        var sessionRows = await _context.Sessions
            .Include(s => s.Subject)
            .Include(s => s.ClassRoom)
                .ThenInclude(c => c.GradeLevel)
            .Include(s => s.ClassRoom)
                .ThenInclude(c => c.Students)
            .Include(s => s.Attendances)
            .Where(s => s.TeacherId == teacher.Id && s.SessionDate >= targetDate && s.SessionDate < nextDay)
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                SessionDate = s.SessionDate,
                SubjectName = s.Subject != null ? s.Subject.Name : "مادة",
                GradeName = s.ClassRoom != null && s.ClassRoom.GradeLevel != null ? s.ClassRoom.GradeLevel.Name : "—",
                ClassRoomName = s.ClassRoom != null ? s.ClassRoom.Name : "—",
                ClassRoomId = s.ClassRoomId,
                StartTime = s.SessionDate.Add(s.StartTime),
                EndTime = s.SessionDate.Add(s.EndTime),
                AttendanceType = s.AttendanceType ?? "QR",
                StudentCount = s.ClassRoom != null ? s.ClassRoom.Students.Count : 0,
                IsRecorded = s.Attendances.Any(),
                AttendanceCount = s.Attendances.Count()
            })
            .ToListAsync();

        var sessions = sessionRows.Select(session =>
        {
            var window = DescribeAttendanceWindow(session.StartTime, session.EndTime, now);
            var needsAttention = session.StudentCount > 0
                ? session.AttendanceCount < session.StudentCount
                : !session.IsRecorded;

            return new
            {
                session.Id,
                session.SessionDate,
                session.SubjectName,
                session.GradeName,
                session.ClassRoomName,
                session.ClassRoomId,
                session.StartTime,
                session.EndTime,
                session.AttendanceType,
                session.StudentCount,
                session.IsRecorded,
                session.AttendanceCount,
                AttendanceWindowStatus = window.Status,
                AttendanceWindowLabel = window.Label,
                CanRecordAttendance = window.CanRecord,
                NeedsAttention = window.CanRecord && needsAttention
            };
        });

        return Ok(sessions);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSessions()
    {
        var today = SchoolClock.Today;
        var tomorrow = today.AddDays(1);

        var sessions = await _context.Sessions
            .Include(s => s.Subject)
            .Include(s => s.ClassRoom)
            .Include(s => s.Teacher)
            .Where(s => s.SessionDate >= today && s.SessionDate < tomorrow)
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                SubjectName = s.Subject != null ? s.Subject.Name : "مادة",
                ClassRoomName = s.ClassRoom != null ? s.ClassRoom.Name : "—",
                TeacherName = s.Teacher != null ? s.Teacher.FullName : "—",
                StartTime = s.SessionDate.Add(s.StartTime),
                EndTime = s.SessionDate.Add(s.EndTime),
                IsActive = s.SessionDate.Add(s.StartTime) <= SchoolClock.Now && s.SessionDate.Add(s.EndTime) >= SchoolClock.Now
            })
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpGet("me/attendance-context")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyAttendanceContext()
    {
        var student = await GetCurrentStudentAsync();
        if (student == null)
        {
            return Unauthorized("Student not found");
        }

        var now = SchoolClock.Now;
        var today = SchoolClock.Today;
        var tomorrow = today.AddDays(1);

        var todaySessions = await _context.Sessions
            .Include(s => s.Subject)
            .Include(s => s.Teacher)
            .Include(s => s.ClassRoom)
            .Include(s => s.Attendances)
            .Where(s => s.ClassRoomId == student.ClassRoomId && s.SessionDate >= today && s.SessionDate < tomorrow)
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                Title = s.Title ?? (s.Subject != null ? s.Subject.Name : "حصة دراسية"),
                SubjectName = s.Subject != null ? s.Subject.Name : "حصة دراسية",
                TeacherName = s.Teacher != null ? s.Teacher.FullName : "غير محدد",
                ClassRoomName = s.ClassRoom != null ? s.ClassRoom.Name : "غير محدد",
                StartTime = s.SessionDate.Add(s.StartTime),
                EndTime = s.SessionDate.Add(s.EndTime),
                AttendanceType = s.AttendanceType ?? "QR",
                s.IsLive,
                IsActive = s.SessionDate.Add(s.StartTime) <= now && s.SessionDate.Add(s.EndTime) >= now,
                IsCompleted = s.SessionDate.Add(s.EndTime) < now,
                AttendanceRecorded = s.Attendances.Any(a => a.StudentId == student.Id),
                AttendanceStatus = s.Attendances
                    .Where(a => a.StudentId == student.Id)
                    .Select(a => a.Status)
                    .FirstOrDefault(),
                AttendanceMethod = s.Attendances
                    .Where(a => a.StudentId == student.Id)
                    .Select(a => a.Method)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var todaySessionDescriptors = todaySessions
            .Select(session =>
            {
                var window = DescribeAttendanceWindow(session.StartTime, session.EndTime, now);
                var canMarkWithQr = window.CanRecord
                    && !session.AttendanceRecorded
                    && string.Equals(session.AttendanceType ?? "QR", "QR", StringComparison.OrdinalIgnoreCase);

                return new
                {
                    session.Id,
                    session.Title,
                    session.SubjectName,
                    session.TeacherName,
                    session.ClassRoomName,
                    session.StartTime,
                    session.EndTime,
                    session.AttendanceType,
                    session.IsLive,
                    session.IsActive,
                    session.IsCompleted,
                    session.AttendanceRecorded,
                    session.AttendanceStatus,
                    session.AttendanceMethod,
                    AttendanceWindowStatus = window.Status,
                    AttendanceWindowLabel = window.Label,
                    CanMarkWithQr = canMarkWithQr
                };
            })
            .ToList();

        var activeSession = todaySessionDescriptors.FirstOrDefault(session => session.IsActive);
        var scanSession = todaySessionDescriptors
            .Where(session => session.CanMarkWithQr)
            .OrderByDescending(session => session.IsActive)
            .ThenByDescending(session => session.StartTime)
            .FirstOrDefault();

        var nextSession = await _context.Sessions
            .Include(s => s.Subject)
            .Include(s => s.Teacher)
            .Include(s => s.ClassRoom)
            .Where(s => s.ClassRoomId == student.ClassRoomId && (s.SessionDate > today || (s.SessionDate == today && s.StartTime > now.TimeOfDay)))
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                Title = s.Title ?? (s.Subject != null ? s.Subject.Name : "حصة دراسية"),
                SubjectName = s.Subject != null ? s.Subject.Name : "حصة دراسية",
                TeacherName = s.Teacher != null ? s.Teacher.FullName : "غير محدد",
                ClassRoomName = s.ClassRoom != null ? s.ClassRoom.Name : "غير محدد",
                StartTime = s.SessionDate.Add(s.StartTime),
                EndTime = s.SessionDate.Add(s.EndTime),
                AttendanceType = s.AttendanceType ?? "QR",
                s.IsLive
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            className = student.ClassRoom?.Name ?? "غير محدد",
            gradeLevel = student.ClassRoom?.GradeLevel?.Name ?? "غير محدد",
            activeSession,
            scanSession,
            nextSession,
            todaySessions = todaySessionDescriptors
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions()
    {
        var today = SchoolClock.Today;
        var tomorrow = today.AddDays(1);

        var sessions = await _context.Sessions
            .Include(s => s.Subject)
            .Include(s => s.ClassRoom)
            .Include(s => s.Teacher)
            .Where(s => s.SessionDate >= today && s.SessionDate < tomorrow)
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                SubjectName = s.Subject != null ? s.Subject.Name : "مادة",
                ClassRoomName = s.ClassRoom != null ? s.ClassRoom.Name : "—",
                TeacherName = s.Teacher != null ? s.Teacher.FullName : "—",
                StartTime = s.SessionDate.Add(s.StartTime),
                EndTime = s.SessionDate.Add(s.EndTime),
                DayOfWeek = (int)s.SessionDate.DayOfWeek,
                IsActive = s.SessionDate.Add(s.StartTime) <= SchoolClock.Now && s.SessionDate.Add(s.EndTime) >= SchoolClock.Now
            })
            .ToListAsync();

        return Ok(sessions);
    }

    private static string? NormalizeTerm(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        var normalized = term.Trim();
        if (string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "الكل", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized;
    }

    private static bool TryParseSessionTime(string? value, out TimeSpan result)
    {
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        return TimeSpan.TryParse(value, out result);
    }

    private static string? NormalizeAttendanceType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "QR";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "qr" => "QR",
            "face" => "Face",
            "manual" => "Manual",
            _ => null
        };
    }

    private static AttendanceWindowDescriptor DescribeAttendanceWindow(DateTime startTime, DateTime endTime, DateTime now)
    {
        var windowStart = startTime.AddMinutes(-30);
        var windowEnd = endTime.AddHours(3);

        if (now < windowStart)
        {
            return new AttendanceWindowDescriptor("upcoming", $"متاح من {windowStart:hh:mm tt}", false);
        }

        if (now > windowEnd)
        {
            return new AttendanceWindowDescriptor("closed", $"أغلقت نافذة الرصد {windowEnd:hh:mm tt}", false);
        }

        return new AttendanceWindowDescriptor("open", $"متاح حتى {windowEnd:hh:mm tt}", true);
    }
}

sealed record AttendanceWindowDescriptor(string Status, string Label, bool CanRecord);

public class GenerateTermScheduleRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Term { get; set; }
}

public class CreateAdminSessionRequest
{
    public string? Title { get; set; }
    public DateTime SessionDate { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int TeacherId { get; set; }
    public int ClassRoomId { get; set; }
    public int SubjectId { get; set; }
    public string? AttendanceType { get; set; }
}
