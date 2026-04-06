using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Application.Features.Sessions.Commands;
using School.Infrastructure.Data;
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

    [HttpGet("teacher/{userId}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> GetTeacherSessions(string userId)
    {
        // Find teacher by matching their email from Identity OR by UserId
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var teacher = await _context.Teachers
            .FirstOrDefaultAsync(t => t.Email == userEmail || t.UserId == userId);

        if (teacher == null)
            return Ok(new List<object>()); // Return empty instead of 404

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        // Query sessions directly by TeacherId (Session has TeacherId FK)
        var sessions = await _context.Sessions
            .Include(s => s.Subject)
            .Include(s => s.ClassRoom)
                .ThenInclude(c => c.GradeLevel)
            .Include(s => s.ClassRoom)
                .ThenInclude(c => c.Students)
            .Include(s => s.Attendances)
            .Where(s => s.TeacherId == teacher.Id && s.SessionDate >= today && s.SessionDate < tomorrow)
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                SubjectName = s.Subject != null ? s.Subject.Name : "مادة",
                GradeName = s.ClassRoom != null && s.ClassRoom.GradeLevel != null ? s.ClassRoom.GradeLevel.Name : "—",
                ClassRoomName = s.ClassRoom != null ? s.ClassRoom.Name : "—",
                ClassRoomId = s.ClassRoomId,
                StartTime = s.SessionDate.Add(s.StartTime),
                EndTime = s.SessionDate.Add(s.EndTime),
                StudentCount = s.ClassRoom != null ? s.ClassRoom.Students.Count : 0,
                IsRecorded = s.Attendances.Any(),
                AttendanceCount = s.Attendances.Count(a => a.IsPresent)
            })
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSessions()
    {
        var today = DateTime.Today;
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
                IsActive = s.SessionDate.Add(s.StartTime) <= DateTime.Now && s.SessionDate.Add(s.EndTime) >= DateTime.Now
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

        var now = DateTime.Now;
        var today = DateTime.Today;
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

        var activeSession = todaySessions
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.SubjectName,
                s.TeacherName,
                s.ClassRoomName,
                s.StartTime,
                s.EndTime,
                s.AttendanceType,
                s.IsLive,
                s.IsActive,
                s.IsCompleted,
                s.AttendanceRecorded,
                s.AttendanceStatus,
                s.AttendanceMethod,
                CanMarkWithQr = s.IsActive
                    && !s.AttendanceRecorded
                    && string.Equals(s.AttendanceType ?? "QR", "QR", StringComparison.OrdinalIgnoreCase)
            })
            .FirstOrDefault(s => s.IsActive);

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
            nextSession,
            todaySessions = todaySessions.Select(s => new
            {
                s.Id,
                s.Title,
                s.SubjectName,
                s.TeacherName,
                s.ClassRoomName,
                s.StartTime,
                s.EndTime,
                s.AttendanceType,
                s.IsLive,
                s.IsActive,
                s.IsCompleted,
                s.AttendanceRecorded,
                s.AttendanceStatus,
                s.AttendanceMethod,
                CanMarkWithQr = s.IsActive
                    && !s.AttendanceRecorded
                    && string.Equals(s.AttendanceType ?? "QR", "QR", StringComparison.OrdinalIgnoreCase)
            })
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions()
    {
        var today = DateTime.Today;
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
                IsActive = s.SessionDate.Add(s.StartTime) <= DateTime.Now && s.SessionDate.Add(s.EndTime) >= DateTime.Now
            })
            .ToListAsync();

        return Ok(sessions);
    }
}
