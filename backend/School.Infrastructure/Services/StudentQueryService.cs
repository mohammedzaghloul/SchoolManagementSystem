using Microsoft.EntityFrameworkCore;
using School.Application.DTOs.Students;
using School.Application.Interfaces;
using School.Infrastructure.Data;

namespace School.Infrastructure.Services;

public sealed class StudentQueryService : IStudentQueryService
{
    private const int DefaultSearchLimit = 10;
    private const int MaxSearchLimit = 20;
    private const int DashboardGradesLimit = 80;
    private const int DashboardAttendanceLimit = 40;
    private const int DashboardAssignmentsLimit = 30;

    private readonly SchoolDbContext _context;

    public StudentQueryService(SchoolDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<StudentSearchResultDto>> SearchStudentsAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = Math.Clamp(limit <= 0 ? DefaultSearchLimit : limit, 1, MaxSearchLimit);
        var trimmedQuery = query?.Trim();

        var students = _context.Students
            .AsNoTracking()
            .Include(student => student.ClassRoom)
            .ThenInclude(classRoom => classRoom!.GradeLevel)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(trimmedQuery))
        {
            var likeQuery = $"%{trimmedQuery}%";
            var isNumeric = int.TryParse(trimmedQuery, out var numericId);

            students = students.Where(student =>
                EF.Functions.Like(student.FullName ?? string.Empty, likeQuery) ||
                EF.Functions.Like(student.Email ?? string.Empty, likeQuery) ||
                EF.Functions.Like(student.Phone ?? string.Empty, likeQuery) ||
                EF.Functions.Like(student.QrCodeValue ?? string.Empty, likeQuery) ||
                (isNumeric && student.Id == numericId));
        }
        else
        {
            students = students.Where(student => student.IsActive);
        }

        return await students
            .OrderByDescending(student => student.IsActive)
            .ThenBy(student => student.FullName)
            .Take(normalizedLimit)
            .Select(student => new StudentSearchResultDto
            {
                Id = student.Id,
                FullName = student.FullName ?? "طالب",
                Email = student.Email,
                Phone = student.Phone,
                ClassRoomName = student.ClassRoom != null ? student.ClassRoom.Name : null,
                GradeLevelName = student.ClassRoom != null && student.ClassRoom.GradeLevel != null
                    ? student.ClassRoom.GradeLevel.Name
                    : null,
                Code = student.QrCodeValue ?? $"STD-{student.Id:0000}",
                IsActive = student.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<StudentDashboardDto?> GetStudentDashboardAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Include(item => item.Parent)
            .Include(item => item.ClassRoom)
            .ThenInclude(classRoom => classRoom!.GradeLevel)
            .FirstOrDefaultAsync(item => item.Id == studentId, cancellationToken);

        if (student == null)
        {
            return null;
        }

        var grades = (await GetStudentGradesAsync(studentId, cancellationToken) ?? []).ToList();
        var attendance = (await GetStudentAttendanceAsync(studentId, cancellationToken) ?? []).ToList();
        var assignments = await GetStudentAssignmentsAsync(studentId, student.ClassRoomId, cancellationToken);
        var gradeUploadStatus = await GetStudentGradeUploadStatusAsync(student.ClassRoomId, cancellationToken);

        var averageGrade = grades.Count == 0 ? 0 : Math.Round(grades.Average(grade => grade.Score), 1);
        var attendancePercent = attendance.Count == 0
            ? 0
            : Math.Round(attendance.Count(item => item.IsPresent) * 100.0 / attendance.Count, 1);
        var activityPercent = assignments.Count == 0
            ? 0
            : Math.Round(assignments.Count(item => item.IsSubmitted) * 100.0 / assignments.Count, 1);

        return new StudentDashboardDto
        {
            Id = student.Id,
            Name = student.FullName ?? "طالب",
            Email = student.Email,
            Phone = student.Phone,
            ParentName = student.Parent?.FullName,
            ClassRoomName = student.ClassRoom?.Name,
            GradeLevelName = student.ClassRoom?.GradeLevel?.Name,
            AcademicYear = student.ClassRoom?.AcademicYear,
            Code = student.QrCodeValue ?? $"STD-{student.Id:0000}",
            Avg = averageGrade,
            Attendance = attendancePercent,
            Activity = activityPercent,
            GradesCompleted = gradeUploadStatus.GradesCompleted,
            TotalSubjects = gradeUploadStatus.TotalSubjects,
            ApprovedSubjects = gradeUploadStatus.ApprovedSubjects,
            GradesStatus = gradeUploadStatus.GradesCompleted ? "COMPLETED" : "IN_PROGRESS",
            GradesStatusLabel = gradeUploadStatus.GradesCompleted ? "تم رفع الدرجات" : "جاري رفع الدرجات",
            LastAttendance = attendance.FirstOrDefault(),
            Grades = grades.Take(DashboardGradesLimit).ToList(),
            AttendanceRecords = attendance.Take(DashboardAttendanceLimit).ToList(),
            Assignments = assignments.Take(DashboardAssignmentsLimit).ToList(),
            Alerts = BuildAlerts(grades, attendancePercent, assignments)
        };
    }

    public async Task<IReadOnlyList<StudentGradeDto>?> GetStudentGradesAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _context.Students
            .AsNoTracking()
            .AnyAsync(student => student.Id == studentId, cancellationToken);

        if (!exists)
        {
            return null;
        }

        var gradeRows = await _context.GradeRecords
            .AsNoTracking()
            .Include(grade => grade.Subject)
            .ThenInclude(subject => subject!.Teacher)
            .Where(grade => grade.StudentId == studentId)
            .OrderByDescending(grade => grade.Date)
            .ThenBy(grade => grade.Subject!.Name)
            .Select(grade => new
            {
                grade.Id,
                grade.SubjectId,
                SubjectName = grade.Subject != null ? grade.Subject.Name : null,
                TeacherId = grade.Subject != null ? grade.Subject.TeacherId : null,
                TeacherName = grade.Subject != null && grade.Subject.Teacher != null ? grade.Subject.Teacher.FullName : null,
                grade.GradeType,
                grade.Score,
                grade.Date,
                grade.Notes
            })
            .ToListAsync(cancellationToken);

        var subjectIds = gradeRows.Select(grade => grade.SubjectId).Distinct().ToList();
        var confirmations = await _context.GradeUploadConfirmations
            .AsNoTracking()
            .Where(item => subjectIds.Contains(item.SubjectId))
            .ToListAsync(cancellationToken);
        var confirmationsByKey = confirmations
            .GroupBy(item => BuildConfirmationKey(item.SubjectId, item.TeacherId, item.GradeType, item.Date))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Id).First());

        return gradeRows.Select(grade =>
        {
            var key = BuildConfirmationKey(
                grade.SubjectId,
                grade.TeacherId ?? 0,
                grade.GradeType,
                grade.Date.Date);
            confirmationsByKey.TryGetValue(key, out var confirmation);
            var isApproved = confirmation?.IsConfirmed == true;

            return new StudentGradeDto
            {
                Id = grade.Id,
                SubjectId = grade.SubjectId,
                SubjectName = grade.SubjectName ?? "المادة",
                TeacherName = grade.TeacherName,
                GradeType = grade.GradeType,
                Score = grade.Score,
                Date = grade.Date,
                Notes = grade.Notes,
                IsApproved = isApproved,
                ApprovalStatus = isApproved ? "COMPLETED" : "IN_PROGRESS"
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<StudentAttendanceDto>?> GetStudentAttendanceAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _context.Students
            .AsNoTracking()
            .AnyAsync(student => student.Id == studentId, cancellationToken);

        if (!exists)
        {
            return null;
        }

        return await _context.Attendances
            .AsNoTracking()
            .Include(attendance => attendance.Session)
            .ThenInclude(session => session!.Subject)
            .Where(attendance => attendance.StudentId == studentId)
            .OrderByDescending(attendance => attendance.Time)
            .ThenByDescending(attendance => attendance.RecordedAt)
            .Select(attendance => new StudentAttendanceDto
            {
                Id = attendance.Id,
                Date = attendance.Time,
                Status = attendance.Status ?? (attendance.IsPresent ? "Present" : "Absent"),
                IsPresent = attendance.IsPresent,
                Method = attendance.Method ?? attendance.Session!.AttendanceType,
                SessionTitle = attendance.Session != null ? attendance.Session.Title : null,
                SubjectName = attendance.Session != null && attendance.Session.Subject != null
                    ? attendance.Session.Subject.Name
                    : null,
                RecordedAt = attendance.RecordedAt
            })
            .Take(120)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<StudentAssignmentDto>> GetStudentAssignmentsAsync(
        int studentId,
        int? classRoomId,
        CancellationToken cancellationToken)
    {
        if (!classRoomId.HasValue)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        return await _context.Assignments
            .AsNoTracking()
            .Include(assignment => assignment.Subject)
            .Include(assignment => assignment.Teacher)
            .Include(assignment => assignment.Submissions.Where(submission => submission.StudentId == studentId))
            .Where(assignment => assignment.ClassRoomId == classRoomId.Value)
            .OrderByDescending(assignment => assignment.DueDate)
            .Select(assignment => new StudentAssignmentDto
            {
                Id = assignment.Id,
                Title = assignment.Title,
                SubjectName = assignment.Subject != null ? assignment.Subject.Name : null,
                TeacherName = assignment.Teacher != null ? assignment.Teacher.FullName : null,
                DueDate = assignment.DueDate,
                IsSubmitted = assignment.Submissions.Any(submission => submission.StudentId == studentId),
                IsLate = !assignment.Submissions.Any(submission => submission.StudentId == studentId) && assignment.DueDate < now,
                Status = assignment.Submissions.Any(submission => submission.StudentId == studentId)
                    ? "SUBMITTED"
                    : assignment.DueDate < now ? "LATE" : "PENDING",
                StatusLabel = assignment.Submissions.Any(submission => submission.StudentId == studentId)
                    ? "تم"
                    : assignment.DueDate < now ? "متأخر" : "قيد التسليم",
                SubmittedAt = assignment.Submissions
                    .Where(submission => submission.StudentId == studentId)
                    .OrderByDescending(submission => submission.SubmissionDate)
                    .Select(submission => (DateTime?)submission.SubmissionDate)
                    .FirstOrDefault(),
                Grade = assignment.Submissions
                    .Where(submission => submission.StudentId == studentId)
                    .OrderByDescending(submission => submission.SubmissionDate)
                    .Select(submission => submission.Grade)
                    .FirstOrDefault(),
                TeacherFeedback = assignment.Submissions
                    .Where(submission => submission.StudentId == studentId)
                    .OrderByDescending(submission => submission.SubmissionDate)
                    .Select(submission => submission.TeacherFeedback)
                    .FirstOrDefault()
            })
            .Take(80)
            .ToListAsync(cancellationToken);
    }

    private async Task<GradeUploadStatus> GetStudentGradeUploadStatusAsync(
        int? classRoomId,
        CancellationToken cancellationToken)
    {
        if (!classRoomId.HasValue)
        {
            return new GradeUploadStatus(false, 0, 0);
        }

        var subjects = await _context.Subjects
            .AsNoTracking()
            .Where(subject => subject.ClassRoomId == classRoomId.Value)
            .Where(subject => subject.IsActive)
            .Where(subject => subject.TeacherId.HasValue)
            .Select(subject => new { subject.Id, TeacherId = subject.TeacherId!.Value })
            .ToListAsync(cancellationToken);

        if (subjects.Count == 0)
        {
            return new GradeUploadStatus(false, 0, 0);
        }

        var subjectIds = subjects.Select(subject => subject.Id).ToList();
        var latestConfirmations = await _context.GradeUploadConfirmations
            .AsNoTracking()
            .Where(item => subjectIds.Contains(item.SubjectId))
            .GroupBy(item => item.SubjectId)
            .Select(group => group
                .OrderByDescending(item => item.UpdatedAt)
                .ThenByDescending(item => item.Id)
                .First())
            .ToListAsync(cancellationToken);
        var confirmedSubjects = latestConfirmations.Count(item => item.IsConfirmed);

        return new GradeUploadStatus(
            confirmedSubjects == subjects.Count,
            subjects.Count,
            confirmedSubjects);
    }

    private static List<StudentAlertDto> BuildAlerts(
        IReadOnlyList<StudentGradeDto> grades,
        double attendancePercent,
        IReadOnlyList<StudentAssignmentDto> assignments)
    {
        var alerts = new List<StudentAlertDto>();
        var lowestSubject = grades
            .GroupBy(grade => grade.SubjectName)
            .Select(group => new { Subject = group.Key, Average = group.Average(item => item.Score) })
            .Where(item => item.Average < 65)
            .OrderBy(item => item.Average)
            .FirstOrDefault();

        if (lowestSubject != null)
        {
            alerts.Add(new StudentAlertDto
            {
                Type = "warning",
                Title = "تنبيه مستوى",
                Message = $"الطالب مستواه نازل في {lowestSubject.Subject}."
            });
        }

        if (attendancePercent > 0 && attendancePercent < 75)
        {
            alerts.Add(new StudentAlertDto
            {
                Type = "danger",
                Title = "تنبيه حضور",
                Message = "نسبة حضور الطالب أقل من المستوى المطلوب."
            });
        }

        var lateAssignments = assignments.Count(item => item.IsLate);
        if (lateAssignments > 0)
        {
            alerts.Add(new StudentAlertDto
            {
                Type = "warning",
                Title = "واجبات متأخرة",
                Message = $"يوجد {lateAssignments} واجب متأخر."
            });
        }

        return alerts;
    }

    private static string BuildConfirmationKey(int subjectId, int teacherId, string gradeType, DateTime date)
    {
        return $"{subjectId}:{teacherId}:{gradeType.Trim().ToLowerInvariant()}:{date:yyyy-MM-dd}";
    }

    private sealed record GradeUploadStatus(bool GradesCompleted, int TotalSubjects, int ApprovedSubjects);
}
