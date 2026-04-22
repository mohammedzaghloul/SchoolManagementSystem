using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Infrastructure.Data;
using School.Infrastructure.Identity;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
public class DashboardsController : BaseApiController
{
    private readonly SchoolDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardsController(SchoolDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> GetAdminDashboard()
    {
        var totalStudents = await _context.Students.CountAsync();
        var totalTeachers = await _context.Teachers.CountAsync();
        var totalClasses = await _context.ClassRooms.CountAsync();
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        var totalAttendances = await _context.Attendances.CountAsync();
        var presentAttendances = await _context.Attendances.CountAsync(a => a.IsPresent);
        var tuitionInvoices = await _context.TuitionInvoices.AsNoTracking().ToListAsync();

        double attendanceRate = totalAttendances > 0
            ? Math.Round((double)presentAttendances / totalAttendances * 100, 1)
            : 0;

        var monthlyRevenue = tuitionInvoices
            .Where(invoice => invoice.PaidAt.HasValue && invoice.PaidAt.Value >= monthStart && invoice.PaidAt.Value < nextMonthStart)
            .Sum(invoice => invoice.AmountPaid);

        var pendingFees = tuitionInvoices
            .Where(invoice => invoice.AmountPaid < invoice.Amount && invoice.DueDate.Date < DateTime.Today)
            .Sum(invoice => invoice.Amount - invoice.AmountPaid);

        var recentStudents = await _context.Students
            .Include(s => s.ClassRoom)
                .ThenInclude(c => c!.GradeLevel)
            .Include(s => s.Parent)
            .OrderByDescending(s => s.Id)
            .Take(5)
            .Select(s => new
            {
                s.Id,
                s.FullName,
                s.Email,
                ClassRoomName = s.ClassRoom != null ? s.ClassRoom.Name : "N/A",
                GradeName = s.ClassRoom != null && s.ClassRoom.GradeLevel != null ? s.ClassRoom.GradeLevel.Name : "N/A",
                ParentName = s.Parent != null ? s.Parent.FullName : "N/A"
            })
            .ToListAsync();

        return Ok(new
        {
            totalStudents,
            totalTeachers,
            totalClasses,
            attendanceRate = $"{attendanceRate}%",
            monthlyRevenue = Math.Round(monthlyRevenue, 2),
            pendingFees = Math.Round(pendingFees, 2),
            recentStudents
        });
    }

    [HttpGet("teacher")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<object>> GetTeacherDashboard()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var teacher = await _context.Teachers
            .Include(t => t.ClassRooms)
            .FirstOrDefaultAsync(t => t.Email == userEmail);

        if (teacher == null)
        {
            return NotFound("Teacher not found");
        }

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var todaySessions = await _context.Sessions
            .Include(s => s.ClassRoom)
                .ThenInclude(c => c!.Students)
            .Include(s => s.Subject)
            .Include(s => s.Attendances)
            .Where(s => s.TeacherId == teacher.Id && s.SessionDate >= today && s.SessionDate < tomorrow)
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                s.Title,
                SubjectName = s.Subject != null ? s.Subject.Name : "N/A",
                ClassRoomName = s.ClassRoom != null ? s.ClassRoom.Name : "N/A",
                StartTime = s.SessionDate.Add(s.StartTime),
                EndTime = s.SessionDate.Add(s.EndTime),
                Type = s.AttendanceType,
                s.IsLive,
                StudentCount = s.ClassRoom != null ? s.ClassRoom.Students.Count : 0,
                AttendanceCount = s.Attendances.Count
            })
            .ToListAsync();

        var totalAttendances = await _context.Attendances
            .Include(a => a.Session)
            .CountAsync(a => a.Session.TeacherId == teacher.Id);

        var presentAttendances = await _context.Attendances
            .Include(a => a.Session)
            .CountAsync(a => a.Session.TeacherId == teacher.Id && a.IsPresent);

        double attendanceRate = totalAttendances > 0
            ? Math.Round((double)presentAttendances / totalAttendances * 100, 1)
            : 0;

        var studentCount = await _context.Sessions
            .Where(s => s.TeacherId == teacher.Id)
            .Include(s => s.ClassRoom)
                .ThenInclude(c => c!.Students)
            .SelectMany(s => s.ClassRoom!.Students)
            .Select(s => s.Id)
            .Distinct()
            .CountAsync();

        return Ok(new
        {
            totalStudents = studentCount,
            todayClasses = todaySessions.Count,
            attendanceAvg = $"{attendanceRate}%",
            todaySessions
        });
    }

    [HttpGet("student")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<object>> GetStudentDashboard()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var student = await _context.Students
            .Include(s => s.ClassRoom)
            .FirstOrDefaultAsync(s => s.Email == userEmail);

        if (student == null)
        {
            return NotFound("Student not found");
        }

        var today = DateTime.Today;

        var nextSession = await _context.Sessions
            .Include(s => s.Subject)
            .Where(s => s.ClassRoomId == student.ClassRoomId && (s.SessionDate > today || (s.SessionDate == today && s.StartTime >= DateTime.Now.TimeOfDay)))
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                s.Title,
                SubjectName = s.Subject.Name,
                StartTime = s.SessionDate.Add(s.StartTime),
                EndTime = s.SessionDate.Add(s.EndTime),
                Type = s.AttendanceType,
                s.IsLive
            })
            .FirstOrDefaultAsync();

        var totalAttendances = await _context.Attendances.CountAsync(a => a.StudentId == student.Id);
        var presentAttendances = await _context.Attendances.CountAsync(a => a.StudentId == student.Id && a.IsPresent);

        double attendanceRate = totalAttendances > 0
            ? Math.Round((double)presentAttendances / totalAttendances * 100, 1)
            : 0;

        var upcomingExams = await _context.Exams
            .Include(e => e.Subject)
            .Where(e => e.ClassRoomId == student.ClassRoomId && e.StartTime >= today)
            .OrderBy(e => e.StartTime)
            .Take(3)
            .Select(e => new
            {
                e.Id,
                e.Title,
                SubjectName = e.Subject.Name,
                Date = e.StartTime,
                DurationMinutes = (int)(e.EndTime - e.StartTime).TotalMinutes
            })
            .ToListAsync();

        var recentGrades = await _context.GradeRecords
            .Include(g => g.Subject)
            .Where(g => g.StudentId == student.Id)
            .OrderByDescending(g => g.Date)
            .Take(4)
            .Select(g => new
            {
                Subject = g.Subject.Name,
                Score = g.Score,
                MaxScore = 100
            })
            .ToListAsync();

        var todaySessions = await _context.Sessions
            .Include(s => s.Subject)
            .Include(s => s.Teacher)
            .Where(s => s.ClassRoomId == student.ClassRoomId && s.SessionDate == today)
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                Subject = s.Subject.Name,
                TeacherName = s.Teacher.FullName,
                StartTime = s.StartTime.ToString(@"hh\:mm"),
                EndTime = s.EndTime.ToString(@"hh\:mm"),
                Completed = s.SessionDate.Add(s.EndTime) < DateTime.Now,
                IsLive = s.IsLive
            })
            .ToListAsync();

        return Ok(new
        {
            attendanceRate = $"{attendanceRate}%",
            nextSession,
            upcomingExams,
            upcomingAssignmentsCount = 0,
            className = student.ClassRoom?.Name ?? "N/A",
            gradeLevel = student.ClassRoom?.GradeLevel?.Name ?? "N/A",
            todaySessions,
            recentGrades
        });
    }

    [HttpGet("parent")]
    [Authorize(Roles = "Parent")]
    public async Task<ActionResult<object>> GetParentDashboard()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var parent = await _context.Parents
            .Include(p => p.Children)
                .ThenInclude(c => c.ClassRoom)
                    .ThenInclude(cr => cr!.GradeLevel)
            .FirstOrDefaultAsync(p => p.Email == userEmail);

        if (parent == null)
        {
            return NotFound("Parent not found");
        }

        var childIds = parent.Children.Select(c => c.Id).ToList();
        var tuitionInvoices = await _context.TuitionInvoices
            .Where(t => childIds.Contains(t.StudentId))
            .ToListAsync();
        var childrenData = new List<object>();
        var attendanceRates = new List<double>();
        var averages = new List<double>();
        var totalAbsences = 0;

        foreach (var child in parent.Children)
        {
            var attendances = await _context.Attendances
                .Include(a => a.Session)
                    .ThenInclude(s => s!.Subject)
                .Where(a => a.StudentId == child.Id)
                .OrderByDescending(a => a.RecordedAt)
                .ToListAsync();

            var totalAttendances = attendances.Count;
            var presentAttendances = attendances.Count(a => a.IsPresent);
            var absences = attendances.Count(a => !a.IsPresent);

            double attendanceRate = totalAttendances > 0
                ? Math.Round((double)presentAttendances / totalAttendances * 100, 1)
                : 100;

            var grades = await _context.GradeRecords
                .Include(g => g.Subject)
                .Where(g => g.StudentId == child.Id)
                .ToListAsync();

            double average = grades.Any() ? Math.Round(grades.Average(g => g.Score), 1) : 0;
            var latestAttendance = attendances.FirstOrDefault();
            var pendingBalance = tuitionInvoices
                .Where(t => t.StudentId == child.Id)
                .Sum(t => t.Amount - t.AmountPaid);

            attendanceRates.Add(attendanceRate);
            averages.Add(average);
            totalAbsences += absences;

            childrenData.Add(new
            {
                child.Id,
                child.FullName,
                avatar = child.ProfilePictureUrl,
                classRoomName = child.ClassRoom != null ? child.ClassRoom.Name : "N/A",
                gradeLevel = child.ClassRoom != null && child.ClassRoom.GradeLevel != null ? child.ClassRoom.GradeLevel.Name : "N/A",
                attendanceRate,
                absences,
                average,
                pendingBalance = Math.Round(pendingBalance, 2),
                latestAttendanceStatus = latestAttendance == null
                    ? "NoData"
                    : latestAttendance.IsPresent
                        ? "Present"
                        : latestAttendance.Status ?? "Absent",
                latestAttendanceAt = latestAttendance?.RecordedAt,
                recentGrades = grades
                    .OrderByDescending(g => g.Date)
                    .Select(g => new
                    {
                        subject = g.Subject.Name,
                        score = g.Score
                    })
            });
        }

        var pendingPaymentsAmount = tuitionInvoices.Sum(t => t.Amount - t.AmountPaid);
        var pendingInvoicesCount = tuitionInvoices.Count(t => t.AmountPaid < t.Amount);

        var recentAbsenceActivities = await _context.Attendances
            .Include(a => a.Student)
            .Include(a => a.Session)
                .ThenInclude(s => s!.Subject)
            .Where(a => childIds.Contains(a.StudentId) && !a.IsPresent)
            .OrderByDescending(a => a.RecordedAt)
            .Take(4)
            .Select(a => new
            {
                type = "attendance",
                title = $"تم تسجيل غياب للطالب {a.Student!.FullName}",
                description = a.Session!.Subject != null
                    ? $"في حصة {a.Session.Subject.Name}"
                    : "في إحدى الحصص الدراسية",
                createdAt = a.RecordedAt
            })
            .ToListAsync();

        var recentGradeActivities = await _context.GradeRecords
            .Include(g => g.Student)
            .Include(g => g.Subject)
            .Where(g => childIds.Contains(g.StudentId))
            .OrderByDescending(g => g.Date)
            .Take(4)
            .Select(g => new
            {
                type = "grade",
                title = $"تم تحديث درجة {g.Student!.FullName}",
                description = $"{g.Subject!.Name}: {g.Score:0.#}%",
                createdAt = g.Date
            })
            .ToListAsync();

        var recentActivity = recentAbsenceActivities
            .Concat(recentGradeActivities)
            .OrderByDescending(item => item.createdAt)
            .Take(6)
            .ToList();

        return Ok(new
        {
            parentName = parent.FullName,
            parentEmail = parent.Email,
            parentPhone = parent.Phone,
            parentAddress = parent.Address,
            totalChildren = parent.Children.Count,
            summary = new
            {
                totalChildren = parent.Children.Count,
                averageAttendanceRate = attendanceRates.Any() ? Math.Round(attendanceRates.Average(), 1) : 0,
                averageScore = averages.Any() ? Math.Round(averages.Average(), 1) : 0,
                totalAbsences,
                pendingPaymentsAmount = Math.Round(pendingPaymentsAmount, 2),
                pendingInvoicesCount
            },
            recentActivity,
            children = childrenData
        });
    }

    [HttpGet("reports-stats")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> GetReportsStats()
    {
        var totalSessions = await _context.Sessions.CountAsync();
        var totalAttendances = await _context.Attendances.CountAsync();
        var absentDays = await _context.Attendances.CountAsync(a => !a.IsPresent);

        double attendanceRate = totalAttendances > 0
            ? Math.Round((double)(totalAttendances - absentDays) / totalAttendances * 100, 1)
            : 100;

        var gradesDistribution = await _context.GradeLevels
            .Include(g => g.ClassRooms)
                .ThenInclude(c => c.Students)
            .Select(g => new
            {
                name = g.Name,
                count = g.ClassRooms.SelectMany(c => c.Students).Count()
            })
            .ToListAsync();

        int totalStudents = gradesDistribution.Sum(x => x.count);
        var dist = gradesDistribution.Select(g => new
        {
            name = g.name,
            value = totalStudents > 0 ? Math.Round((double)g.count / totalStudents * 100, 0) : 0,
            color = GetColorForIndex(gradesDistribution.IndexOf(g))
        }).ToList();

        var weeklyAttendance = new List<int>();
        for (int i = 5; i >= 0; i--)
        {
            var date = DateTime.Today.AddDays(-i);
            var dayAttendances = await _context.Attendances
                .Include(a => a.Session)
                .Where(a => a.Session.SessionDate == date)
                .CountAsync();
            var dayPresent = await _context.Attendances
                .Include(a => a.Session)
                .Where(a => a.Session.SessionDate == date && a.IsPresent)
                .CountAsync();

            weeklyAttendance.Add(dayAttendances > 0 ? (int)Math.Round((double)dayPresent / dayAttendances * 100) : 0);
        }

        return Ok(new
        {
            totalSessions,
            absentDays,
            attendanceRate,
            gradesDistribution = dist,
            weeklyAttendance
        });
    }

    private string GetColorForIndex(int index)
    {
        string[] colors = { "#6366f1", "#10b981", "#f59e0b", "#ef4444", "#8b5cf6", "#ec4899" };
        return colors[index % colors.Length];
    }
}
