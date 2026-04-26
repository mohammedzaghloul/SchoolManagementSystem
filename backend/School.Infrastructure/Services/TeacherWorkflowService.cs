using Microsoft.EntityFrameworkCore;
using School.Application.DTOs.Teacher;
using School.Application.Interfaces;
using School.Domain.Entities;
using School.Infrastructure.Data;

namespace School.Infrastructure.Services;

public class TeacherWorkflowService : ITeacherWorkflowService
{
    private readonly SchoolDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public TeacherWorkflowService(SchoolDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<OperationResultDto> RecordGradeAsync(
        string teacherIdentityUserId,
        RecordGradeRequest request,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(item => item.Id == request.SubjectId, cancellationToken)
            ?? throw new KeyNotFoundException("Subject was not found.");

        var student = await _context.Students.FirstOrDefaultAsync(item => item.Id == request.StudentId, cancellationToken)
            ?? throw new KeyNotFoundException("Student was not found.");

        if (!isAdmin)
        {
            var teacher = await ResolveTeacherAsync(teacherIdentityUserId, cancellationToken);
            if (subject.TeacherId != teacher.Id)
            {
                throw new UnauthorizedAccessException("You can only record grades for your own subjects.");
            }
        }

        var isStudentEnrolled = await _context.StudentSubjects.AnyAsync(
            item => item.StudentId == request.StudentId && item.SubjectId == request.SubjectId,
            cancellationToken);

        if (!isStudentEnrolled)
        {
            throw new InvalidOperationException("Student is not enrolled in this subject.");
        }

        var recordedDate = (request.RecordedOnUtc ?? DateTime.UtcNow).Date;
        var normalizedGradeType = request.GradeType.Trim();

        var existingGrade = await _context.GradeRecords.FirstOrDefaultAsync(grade =>
            grade.StudentId == request.StudentId &&
            grade.SubjectId == request.SubjectId &&
            grade.GradeType == normalizedGradeType &&
            grade.Date.Date == recordedDate,
            cancellationToken);

        if (existingGrade == null)
        {
            existingGrade = new GradeRecord
            {
                StudentId = request.StudentId,
                SubjectId = request.SubjectId
            };

            await _unitOfWork.Repository<GradeRecord>().AddAsync(existingGrade);
        }

        existingGrade.Score = request.Score;
        existingGrade.GradeType = normalizedGradeType;
        existingGrade.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        existingGrade.Date = recordedDate;

        await _unitOfWork.CompleteAsync();

        return new OperationResultDto
        {
            Success = true,
            Message = "Grade saved successfully.",
            ProcessedCount = 1
        };
    }

    public async Task<OperationResultDto> RecordAttendanceAsync(
        string teacherIdentityUserId,
        RecordAttendanceRequest request,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.Sessions
            .Include(item => item.Subject)
            .FirstOrDefaultAsync(item => item.Id == request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException("Session was not found.");

        if (!isAdmin)
        {
            var teacher = await ResolveTeacherAsync(teacherIdentityUserId, cancellationToken);
            if (session.TeacherId != teacher.Id)
            {
                throw new UnauthorizedAccessException("You can only record attendance for your own sessions.");
            }
        }

        var studentIds = request.Students
            .Select(item => item.StudentId)
            .Distinct()
            .ToList();

        var validStudents = await _context.StudentSubjects
            .Where(item => item.SubjectId == session.SubjectId && studentIds.Contains(item.StudentId))
            .Select(item => item.StudentId)
            .ToListAsync(cancellationToken);

        if (validStudents.Count != studentIds.Count)
        {
            throw new InvalidOperationException("One or more students are not enrolled in the subject for this session.");
        }

        var existingAttendances = await _context.Attendances
            .Where(item => item.SessionId == request.SessionId && studentIds.Contains(item.StudentId))
            .ToListAsync(cancellationToken);

        var existingLookup = existingAttendances.ToDictionary(item => item.StudentId);
        var processedCount = 0;
        var capturedAt = DateTime.UtcNow;

        foreach (var item in request.Students)
        {
            if (!existingLookup.TryGetValue(item.StudentId, out var attendance))
            {
                attendance = new Attendance
                {
                    SessionId = request.SessionId,
                    StudentId = item.StudentId
                };

                await _unitOfWork.Repository<Attendance>().AddAsync(attendance);
                existingLookup[item.StudentId] = attendance;
            }

            attendance.IsPresent = item.IsPresent;
            attendance.Status = NormalizeAttendanceStatus(item.Status, item.IsPresent);
            attendance.Method = string.IsNullOrWhiteSpace(item.Method) ? "Manual" : item.Method.Trim();
            attendance.Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();
            attendance.Time = capturedAt;
            attendance.RecordedAt = capturedAt;
            processedCount++;
        }

        await _unitOfWork.CompleteAsync();

        return new OperationResultDto
        {
            Success = true,
            Message = "Attendance saved successfully.",
            ProcessedCount = processedCount
        };
    }

    private async Task<Teacher> ResolveTeacherAsync(string teacherIdentityUserId, CancellationToken cancellationToken)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(item => item.UserId == teacherIdentityUserId, cancellationToken);
        return teacher ?? throw new UnauthorizedAccessException("Teacher profile was not found for the current user.");
    }

    private static string NormalizeAttendanceStatus(string status, bool isPresent)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return isPresent ? "Present" : "Absent";
        }

        var normalized = status.Trim();
        if (string.Equals(normalized, "Present", StringComparison.OrdinalIgnoreCase))
        {
            return "Present";
        }

        if (string.Equals(normalized, "Absent", StringComparison.OrdinalIgnoreCase))
        {
            return "Absent";
        }

        if (string.Equals(normalized, "Late", StringComparison.OrdinalIgnoreCase))
        {
            return "Late";
        }

        return isPresent ? "Present" : "Absent";
    }
}
