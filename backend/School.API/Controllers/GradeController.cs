using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Domain.Entities;
using School.Infrastructure.Data;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
public class GradeController : BaseApiController
{
    private readonly SchoolDbContext _context;

    public GradeController(SchoolDbContext context)
    {
        _context = context;
    }

    [HttpGet("student")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyGrades()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var student = await _context.Students
            .Include(s => s.ClassRoom)
            .FirstOrDefaultAsync(s => s.Email == userEmail);
        if (student == null)
        {
            return NotFound("Student not found");
        }

        var grades = await _context.GradeRecords
            .Include(g => g.Subject)
            .Where(g => g.StudentId == student.Id)
            .OrderByDescending(g => g.Date)
            .Select(g => new
            {
                g.Id,
                StudentId = g.StudentId,
                SubjectId = g.SubjectId,
                Value = g.Score,
                Term = g.Subject != null ? g.Subject.Term : null,
                AcademicYear = student.ClassRoom != null ? student.ClassRoom.AcademicYear : null,
                Remarks = g.Notes,
                g.GradeType,
                g.Date,
                SubjectName = g.Subject != null ? g.Subject.Name : "المادة"
            })
            .ToListAsync();

        return Ok(grades);
    }

    [HttpGet("student/{studentId}")]
    [Authorize(Roles = "Admin,Teacher,Parent")]
    public async Task<IActionResult> GetStudentGrades(int studentId)
    {
        var grades = await _context.GradeRecords
            .AsNoTracking()
            .Include(g => g.Subject)
            .Where(g => g.StudentId == studentId)
            .OrderByDescending(g => g.Date)
            .Select(g => new
            {
                g.Id,
                StudentId = g.StudentId,
                SubjectId = g.SubjectId,
                Value = g.Score,
                Remarks = g.Notes,
                g.GradeType,
                g.Date,
                SubjectName = g.Subject != null ? g.Subject.Name : "المادة"
            })
            .ToListAsync();

        return Ok(grades);
    }

    [HttpGet("teacher/gradebook")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> GetTeacherGradebook([FromQuery] int subjectId, [FromQuery] string? gradeType, [FromQuery] DateTime? date)
    {
        var (subject, errorResult) = await ResolveManagedSubjectAsync(subjectId);
        if (errorResult != null)
        {
            return errorResult;
        }

        var effectiveDate = NormalizeDate(date);
        var normalizedGradeType = NormalizeGradeType(gradeType);

        var students = await _context.Students
            .AsNoTracking()
            .Where(student => student.ClassRoomId == subject!.ClassRoomId)
            .Where(student => student.IsActive)
            .OrderBy(student => student.Id)
            .Select(student => new
            {
                student.Id,
                student.FullName,
                student.Email
            })
            .ToListAsync();

        var existingGrades = await _context.GradeRecords
            .AsNoTracking()
            .Where(grade => grade.SubjectId == subject!.Id)
            .Where(grade => grade.Date.Date == effectiveDate)
            .Where(grade => grade.GradeType == normalizedGradeType)
            .OrderByDescending(grade => grade.Id)
            .Select(grade => new
            {
                grade.Id,
                grade.StudentId,
                grade.Score,
                grade.Notes,
                grade.Date
            })
            .ToListAsync();

        var gradesByStudentId = existingGrades
            .GroupBy(grade => grade.StudentId)
            .ToDictionary(group => group.Key, group => group.First());
        var studentIds = students.Select(student => student.Id).ToHashSet();
        var gradedStudentIds = existingGrades
            .Where(grade => studentIds.Contains(grade.StudentId))
            .Select(grade => grade.StudentId)
            .Distinct()
            .ToHashSet();
        var missingGradesCount = Math.Max(students.Count - gradedStudentIds.Count, 0);
        var confirmation = await _context.GradeUploadConfirmations
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.SubjectId == subject!.Id &&
                item.TeacherId == subject.TeacherId &&
                item.GradeType == normalizedGradeType &&
                item.Date == effectiveDate);

        return Ok(new
        {
            subjectId = subject!.Id,
            subjectName = subject.Name ?? "المادة",
            classRoomId = subject.ClassRoomId,
            classRoomName = subject.ClassRoom?.Name ?? "غير محدد",
            gradeType = normalizedGradeType,
            date = effectiveDate,
            isConfirmed = confirmation?.IsConfirmed == true,
            confirmedAt = confirmation?.ConfirmedAt,
            missingGradesCount,
            status = confirmation?.IsConfirmed == true ? "COMPLETED" : "IN_PROGRESS",
            statusLabel = confirmation?.IsConfirmed == true ? "تم رفع الدرجات" : "قيد رصد الدرجات",
            students = students.Select(student =>
            {
                gradesByStudentId.TryGetValue(student.Id, out var existingGrade);

                return new
                {
                    id = student.Id,
                    fullName = student.FullName ?? "طالب",
                    email = student.Email,
                    existingGradeId = existingGrade?.Id,
                    score = existingGrade?.Score,
                    notes = existingGrade?.Notes,
                    lastUpdatedAt = existingGrade?.Date
                };
            })
        });
    }

    [HttpPost("teacher/gradebook")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> SaveTeacherGradebook([FromBody] TeacherGradebookSaveRequest request)
    {
        if (request.SubjectId <= 0)
        {
            return BadRequest(new { message = "يرجى اختيار المادة أولًا." });
        }

        if (request.Grades == null || request.Grades.Count == 0)
        {
            return BadRequest(new { message = "لا توجد درجات جاهزة للحفظ." });
        }

        var (subject, errorResult) = await ResolveManagedSubjectAsync(request.SubjectId);
        if (errorResult != null)
        {
            return errorResult;
        }

        var effectiveDate = NormalizeDate(request.Date);
        var normalizedGradeType = NormalizeGradeType(request.GradeType);

        var classStudentIds = (await _context.Students
            .AsNoTracking()
            .Where(student => student.ClassRoomId == subject!.ClassRoomId)
            .Select(student => student.Id)
            .ToListAsync())
            .ToHashSet();

        var payloadGrades = request.Grades
            .Where(item => item.Score.HasValue)
            .ToList();

        if (payloadGrades.Count == 0)
        {
            return BadRequest(new { message = "أدخل درجة واحدة على الأقل قبل الحفظ." });
        }

        foreach (var item in payloadGrades)
        {
            if (!classStudentIds.Contains(item.StudentId))
            {
                return BadRequest(new { message = "يوجد طالب غير تابع لفصل هذه المادة." });
            }

            if (item.Score < 0 || item.Score > 100)
            {
                return BadRequest(new { message = "الدرجة يجب أن تكون بين 0 و100." });
            }
        }

        var gradeIds = payloadGrades
            .Where(item => item.Id.HasValue && item.Id.Value > 0)
            .Select(item => item.Id!.Value)
            .Distinct()
            .ToList();

        var existingGrades = gradeIds.Count == 0
            ? new List<GradeRecord>()
            : await _context.GradeRecords
                .Where(grade => gradeIds.Contains(grade.Id))
                .Where(grade => grade.SubjectId == subject!.Id)
                .ToListAsync();

        var existingById = existingGrades.ToDictionary(grade => grade.Id);
        var savedCount = 0;

        foreach (var item in payloadGrades)
        {
            GradeRecord? existingGrade = null;

            if (item.Id.HasValue && existingById.TryGetValue(item.Id.Value, out var byId))
            {
                existingGrade = byId;
            }
            else
            {
                existingGrade = await _context.GradeRecords.FirstOrDefaultAsync(grade =>
                    grade.SubjectId == subject!.Id &&
                    grade.StudentId == item.StudentId &&
                    grade.GradeType == normalizedGradeType &&
                    grade.Date.Date == effectiveDate);
            }

            if (existingGrade == null)
            {
                existingGrade = new GradeRecord
                {
                    StudentId = item.StudentId,
                    SubjectId = subject!.Id
                };

                _context.GradeRecords.Add(existingGrade);
            }

            existingGrade.Score = item.Score!.Value;
            existingGrade.GradeType = normalizedGradeType;
            existingGrade.Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();
            existingGrade.Date = effectiveDate;

            savedCount++;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            savedCount,
            message = $"تم حفظ {savedCount} درجة بنجاح."
        });
    }

    [HttpPost("teacher/gradebook/confirm")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> ConfirmTeacherGradebook([FromBody] TeacherGradebookConfirmRequest request)
    {
        if (request.SubjectId <= 0)
        {
            return BadRequest(new { message = "يرجى اختيار المادة أولًا." });
        }

        var (subject, errorResult) = await ResolveManagedSubjectAsync(request.SubjectId);
        if (errorResult != null)
        {
            return errorResult;
        }

        if (!subject!.TeacherId.HasValue)
        {
            return BadRequest(new { message = "لا يوجد مدرس مسؤول عن هذه المادة." });
        }

        var effectiveDate = NormalizeDate(request.Date);
        var normalizedGradeType = NormalizeGradeType(request.GradeType);
        var coverage = await GetGradeCoverageAsync(subject.Id, subject.ClassRoomId!.Value, normalizedGradeType, effectiveDate);

        if (request.IsConfirmed && coverage.TotalStudents == 0)
        {
            return BadRequest(new { message = "لا يوجد طلاب في هذا الفصل لاعتماد رفع الدرجات." });
        }

        if (request.IsConfirmed && coverage.MissingGradesCount > 0)
        {
            return BadRequest(new
            {
                message = $"لا يمكن اعتماد الرفع قبل إدخال درجات كل الطلاب. المتبقي: {coverage.MissingGradesCount}.",
                coverage.MissingGradesCount
            });
        }

        var confirmation = await _context.GradeUploadConfirmations.FirstOrDefaultAsync(item =>
            item.SubjectId == subject.Id &&
            item.TeacherId == subject.TeacherId.Value &&
            item.GradeType == normalizedGradeType &&
            item.Date == effectiveDate);

        if (confirmation == null)
        {
            confirmation = new GradeUploadConfirmation
            {
                SubjectId = subject.Id,
                TeacherId = subject.TeacherId.Value,
                GradeType = normalizedGradeType,
                Date = effectiveDate
            };

            _context.GradeUploadConfirmations.Add(confirmation);
        }

        confirmation.IsConfirmed = request.IsConfirmed;
        confirmation.ConfirmedAt = request.IsConfirmed ? DateTime.UtcNow : null;
        confirmation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            isConfirmed = confirmation.IsConfirmed,
            confirmedAt = confirmation.ConfirmedAt,
            coverage.TotalStudents,
            coverage.GradedStudents,
            coverage.MissingGradesCount,
            status = confirmation.IsConfirmed ? "COMPLETED" : "IN_PROGRESS",
            statusLabel = confirmation.IsConfirmed ? "تم رفع الدرجات" : "قيد رصد الدرجات"
        });
    }

    [HttpGet("upload-status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetGradeUploadStatus([FromQuery] string? gradeType, [FromQuery] DateTime? date)
    {
        var effectiveDate = NormalizeDate(date);
        var normalizedGradeType = NormalizeGradeType(gradeType);

        var subjects = await _context.Subjects
            .AsNoTracking()
            .Include(subject => subject.Teacher)
            .Include(subject => subject.ClassRoom)
            .Where(subject => subject.IsActive)
            .Where(subject => subject.TeacherId.HasValue)
            .Where(subject => subject.ClassRoomId.HasValue)
            .OrderBy(subject => subject.Teacher!.FullName)
            .ThenBy(subject => subject.ClassRoom!.Name)
            .ThenBy(subject => subject.Name)
            .Select(subject => new
            {
                subject.Id,
                SubjectName = subject.Name,
                subject.TeacherId,
                TeacherName = subject.Teacher!.FullName,
                TeacherEmail = subject.Teacher.Email,
                subject.ClassRoomId,
                ClassRoomName = subject.ClassRoom!.Name
            })
            .ToListAsync();

        var subjectIds = subjects.Select(subject => subject.Id).ToList();
        var classRoomIds = subjects
            .Select(subject => subject.ClassRoomId!.Value)
            .Distinct()
            .ToList();

        var studentCounts = await _context.Students
            .AsNoTracking()
            .Where(student => student.IsActive)
            .Where(student => student.ClassRoomId.HasValue && classRoomIds.Contains(student.ClassRoomId.Value))
            .GroupBy(student => student.ClassRoomId!.Value)
            .Select(group => new { ClassRoomId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ClassRoomId, item => item.Count);

        var gradeCounts = await _context.GradeRecords
            .AsNoTracking()
            .Where(grade => subjectIds.Contains(grade.SubjectId))
            .Where(grade => grade.GradeType == normalizedGradeType)
            .Where(grade => grade.Date.Date == effectiveDate)
            .GroupBy(grade => grade.SubjectId)
            .Select(group => new
            {
                SubjectId = group.Key,
                Count = group.Select(grade => grade.StudentId).Distinct().Count()
            })
            .ToDictionaryAsync(item => item.SubjectId, item => item.Count);

        var confirmations = await _context.GradeUploadConfirmations
            .AsNoTracking()
            .Where(item => subjectIds.Contains(item.SubjectId))
            .Where(item => item.GradeType == normalizedGradeType)
            .Where(item => item.Date == effectiveDate)
            .ToListAsync();
        var confirmationsBySubject = confirmations
            .GroupBy(item => item.SubjectId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Id).First());

        var subjectStatuses = subjects.Select(subject =>
        {
            studentCounts.TryGetValue(subject.ClassRoomId!.Value, out var totalStudents);
            gradeCounts.TryGetValue(subject.Id, out var gradedStudents);
            confirmationsBySubject.TryGetValue(subject.Id, out var confirmation);
            var isConfirmed = confirmation?.IsConfirmed == true;

            return new GradeUploadSubjectStatus(
                subject.Id,
                subject.SubjectName ?? "المادة",
                subject.TeacherId!.Value,
                subject.TeacherName ?? "مدرس",
                subject.TeacherEmail,
                subject.ClassRoomId.Value,
                subject.ClassRoomName ?? "غير محدد",
                totalStudents,
                gradedStudents,
                Math.Max(totalStudents - gradedStudents, 0),
                isConfirmed,
                confirmation?.ConfirmedAt,
                confirmation?.UpdatedAt);
        }).ToList();

        var teacherStatuses = subjectStatuses
            .GroupBy(item => new { item.TeacherId, item.TeacherName, item.TeacherEmail })
            .Select(group =>
            {
                var subjectsForTeacher = group
                    .OrderBy(item => item.ClassRoomName)
                    .ThenBy(item => item.SubjectName)
                    .ToList();
                var totalSubjects = subjectsForTeacher.Count;
                var confirmedSubjects = subjectsForTeacher.Count(item => item.IsConfirmed);
                var isComplete = totalSubjects > 0 && confirmedSubjects == totalSubjects;

                return new
                {
                    group.Key.TeacherId,
                    group.Key.TeacherName,
                    group.Key.TeacherEmail,
                    totalSubjects,
                    confirmedSubjects,
                    pendingSubjects = Math.Max(totalSubjects - confirmedSubjects, 0),
                    isComplete,
                    status = isComplete ? "COMPLETED" : "IN_PROGRESS",
                    statusLabel = isComplete ? "تم رفع درجات المدرس" : "قيد الرصد",
                    subjects = subjectsForTeacher.Select(item => new
                    {
                        item.SubjectId,
                        item.SubjectName,
                        item.ClassRoomId,
                        item.ClassRoomName,
                        item.TotalStudents,
                        item.GradedStudents,
                        item.MissingGradesCount,
                        item.IsConfirmed,
                        item.ConfirmedAt,
                        item.UpdatedAt,
                        status = item.IsConfirmed ? "COMPLETED" : "IN_PROGRESS",
                        statusLabel = item.IsConfirmed ? "تم رفع الدرجات" : "قيد الرصد"
                    })
                };
            })
            .OrderBy(item => item.isComplete)
            .ThenBy(item => item.TeacherName)
            .ToList();

        var totalTeachers = teacherStatuses.Count;
        var completeTeachers = teacherStatuses.Count(item => item.isComplete);
        var totalSubjectsCount = subjectStatuses.Count;
        var confirmedSubjectsCount = subjectStatuses.Count(item => item.IsConfirmed);
        var allConfirmed = totalSubjectsCount > 0 && confirmedSubjectsCount == totalSubjectsCount;

        return Ok(new
        {
            gradeType = normalizedGradeType,
            date = effectiveDate,
            status = allConfirmed ? "COMPLETED" : "IN_PROGRESS",
            statusLabel = allConfirmed ? "تم رفع درجات" : "prog",
            totalTeachers,
            completeTeachers,
            pendingTeachers = Math.Max(totalTeachers - completeTeachers, 0),
            totalSubjects = totalSubjectsCount,
            confirmedSubjects = confirmedSubjectsCount,
            pendingSubjects = Math.Max(totalSubjectsCount - confirmedSubjectsCount, 0),
            completionPercent = totalSubjectsCount == 0 ? 0 : (int)Math.Round(confirmedSubjectsCount * 100.0 / totalSubjectsCount),
            teachers = teacherStatuses
        });
    }

    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> UpsertGrade([FromBody] GradeRecord grade)
    {
        if (grade.StudentId <= 0 || grade.SubjectId <= 0)
        {
            return BadRequest(new { message = "بيانات الطالب أو المادة غير مكتملة." });
        }

        if (grade.Score < 0 || grade.Score > 100)
        {
            return BadRequest(new { message = "الدرجة يجب أن تكون بين 0 و100." });
        }

        var (subject, errorResult) = await ResolveManagedSubjectAsync(grade.SubjectId);
        if (errorResult != null)
        {
            return errorResult;
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(item => item.Id == grade.StudentId && item.ClassRoomId == subject!.ClassRoomId);

        if (student == null)
        {
            return BadRequest(new { message = "الطالب غير تابع لفصل هذه المادة." });
        }

        var normalizedGradeType = NormalizeGradeType(grade.GradeType);
        var effectiveDate = NormalizeDate(grade.Date);

        if (grade.Id == 0)
        {
            grade.GradeType = normalizedGradeType;
            grade.Date = effectiveDate;
            grade.Notes = string.IsNullOrWhiteSpace(grade.Notes) ? null : grade.Notes.Trim();
            _context.GradeRecords.Add(grade);
        }
        else
        {
            var existingGrade = await _context.GradeRecords.FirstOrDefaultAsync(item => item.Id == grade.Id);
            if (existingGrade == null)
            {
                return NotFound(new { message = "سجل الدرجة غير موجود." });
            }

            existingGrade.StudentId = grade.StudentId;
            existingGrade.SubjectId = grade.SubjectId;
            existingGrade.Score = grade.Score;
            existingGrade.GradeType = normalizedGradeType;
            existingGrade.Notes = string.IsNullOrWhiteSpace(grade.Notes) ? null : grade.Notes.Trim();
            existingGrade.Date = effectiveDate;
        }

        await _context.SaveChangesAsync();
        return Ok(grade);
    }

    private async Task<(Subject? Subject, ActionResult? ErrorResult)> ResolveManagedSubjectAsync(int subjectId)
    {
        if (subjectId <= 0)
        {
            return (null, BadRequest(new { message = "يرجى اختيار المادة أولًا." }));
        }

        var subject = await _context.Subjects
            .Include(item => item.ClassRoom)
            .FirstOrDefaultAsync(item => item.Id == subjectId);

        if (subject == null)
        {
            return (null, NotFound(new { message = "المادة غير موجودة." }));
        }

        if (!subject.ClassRoomId.HasValue)
        {
            return (null, BadRequest(new { message = "المادة غير مرتبطة بفصل دراسي." }));
        }

        if (!User.IsInRole("Admin"))
        {
            var currentTeacher = await GetCurrentTeacherAsync();
            if (currentTeacher == null)
            {
                return (null, Forbid());
            }

            var hasDirectSubjectAccess = subject.TeacherId == currentTeacher.Id;
            var teacherHasDirectSubjects = await _context.Subjects.AsNoTracking().AnyAsync(item =>
                item.TeacherId == currentTeacher.Id &&
                item.IsActive);
            var hasScheduledSubjectAccess = !teacherHasDirectSubjects && await _context.Sessions.AsNoTracking().AnyAsync(session =>
                session.TeacherId == currentTeacher.Id &&
                session.SubjectId == subject.Id);

            if (!hasDirectSubjectAccess && !hasScheduledSubjectAccess)
            {
                return (null, Forbid());
            }
        }

        return (subject, null);
    }

    private async Task<Teacher?> GetCurrentTeacherAsync()
    {
        if (!User.IsInRole("Teacher"))
        {
            return null;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return await _context.Teachers
            .AsNoTracking()
            .FirstOrDefaultAsync(teacher => teacher.UserId == userId || teacher.Email == email);
    }

    private async Task<GradeCoverage> GetGradeCoverageAsync(
        int subjectId,
        int classRoomId,
        string gradeType,
        DateTime date)
    {
        var activeStudents = _context.Students
            .AsNoTracking()
            .Where(student => student.IsActive)
            .Where(student => student.ClassRoomId == classRoomId);

        var totalStudents = await activeStudents.CountAsync();
        var gradedStudents = await _context.GradeRecords
            .AsNoTracking()
            .Where(grade => grade.SubjectId == subjectId)
            .Where(grade => grade.GradeType == gradeType)
            .Where(grade => grade.Date.Date == date)
            .Join(
                activeStudents,
                grade => grade.StudentId,
                student => student.Id,
                (grade, _) => grade.StudentId)
            .Distinct()
            .CountAsync();

        return new GradeCoverage(
            totalStudents,
            gradedStudents,
            Math.Max(totalStudents - gradedStudents, 0));
    }

    private static string NormalizeGradeType(string? gradeType)
    {
        return string.IsNullOrWhiteSpace(gradeType) ? "واجب" : gradeType.Trim();
    }

    private static DateTime NormalizeDate(DateTime? date)
    {
        return (date ?? DateTime.UtcNow).Date;
    }

    public sealed class TeacherGradebookSaveRequest
    {
        public int SubjectId { get; set; }
        public string? GradeType { get; set; }
        public DateTime? Date { get; set; }
        public List<TeacherGradebookGradeItem> Grades { get; set; } = [];
    }

    public sealed class TeacherGradebookConfirmRequest
    {
        public int SubjectId { get; set; }
        public string? GradeType { get; set; }
        public DateTime? Date { get; set; }
        public bool IsConfirmed { get; set; } = true;
    }

    public sealed class TeacherGradebookGradeItem
    {
        public int? Id { get; set; }
        public int StudentId { get; set; }
        public double? Score { get; set; }
        public string? Notes { get; set; }
    }

    private sealed record GradeCoverage(
        int TotalStudents,
        int GradedStudents,
        int MissingGradesCount);

    private sealed record GradeUploadSubjectStatus(
        int SubjectId,
        string SubjectName,
        int TeacherId,
        string TeacherName,
        string? TeacherEmail,
        int ClassRoomId,
        string ClassRoomName,
        int TotalStudents,
        int GradedStudents,
        int MissingGradesCount,
        bool IsConfirmed,
        DateTime? ConfirmedAt,
        DateTime? UpdatedAt);
}
