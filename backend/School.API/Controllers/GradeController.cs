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
            .OrderBy(student => student.FullName)
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

        return Ok(new
        {
            subjectId = subject!.Id,
            subjectName = subject.Name ?? "المادة",
            classRoomId = subject.ClassRoomId,
            classRoomName = subject.ClassRoom?.Name ?? "غير محدد",
            gradeType = normalizedGradeType,
            date = effectiveDate,
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

        var classStudentIds = await _context.Students
            .AsNoTracking()
            .Where(student => student.ClassRoomId == subject!.ClassRoomId)
            .Select(student => student.Id)
            .ToHashSetAsync();

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
            if (currentTeacher == null || subject.TeacherId != currentTeacher.Id)
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

        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return await _context.Teachers.AsNoTracking().FirstOrDefaultAsync(teacher => teacher.Email == email);
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

    public sealed class TeacherGradebookGradeItem
    {
        public int? Id { get; set; }
        public int StudentId { get; set; }
        public double? Score { get; set; }
        public string? Notes { get; set; }
    }
}
