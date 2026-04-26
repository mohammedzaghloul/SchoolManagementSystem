using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using School.Application.Interfaces;

namespace School.API.Controllers;

[Authorize]
public class AssignmentController : BaseApiController
{
    private readonly School.Infrastructure.Data.SchoolDbContext _context;
    private readonly IFileStorageService _storage;

    public AssignmentController(School.Infrastructure.Data.SchoolDbContext context, IFileStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    [HttpGet]
    public async Task<IActionResult> GetAssignments()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (role == "Student")
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == userEmail);
            if (student == null) return NotFound();

            var assignments = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Teacher)
                .Where(a => a.ClassRoomId == student.ClassRoomId)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    SubjectName = a.Subject!.Name,
                    TeacherName = a.Teacher!.FullName,
                    a.DueDate,
                    a.AttachmentUrl,
                    IsSubmitted = _context.AssignmentSubmissions.Any(s => s.AssignmentId == a.Id && s.StudentId == student.Id)
                })
                .ToListAsync();

            return Ok(assignments);
        }
        else if (role == "Teacher")
        {
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Email == userEmail);
            if (teacher == null) return NotFound();

            var assignments = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.ClassRoom)
                .Where(a => a.TeacherId == teacher.Id)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    SubjectName = a.Subject!.Name,
                    ClassRoomName = a.ClassRoom!.Name,
                    a.DueDate,
                    SubmissionCount = a.Submissions.Count
                })
                .ToListAsync();

            return Ok(assignments);
        }

        return BadRequest("Invalid role");
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> CreateAssignment([FromBody] School.Domain.Entities.Assignment assignment)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Email == userEmail);
        if (teacher == null) return Unauthorized();

        var subject = await _context.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(currentSubject =>
                currentSubject.Id == assignment.SubjectId &&
                currentSubject.ClassRoomId == assignment.ClassRoomId &&
                currentSubject.TeacherId == teacher.Id &&
                currentSubject.IsActive);

        if (subject == null)
        {
            return Forbid();
        }

        assignment.TeacherId = teacher.Id;
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();
        
        return Ok(assignment);
    }

    [HttpPost("submit")]
    [Authorize(Roles = "Student")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitAssignment([FromForm] SubmissionInput input)
    {
        if (input.File == null || input.File.Length == 0)
            return BadRequest("يجب اختيار ملف للتسليم");

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == userEmail);
        if (student == null) return NotFound();

        // 1. Upload the file
        string folderName = $"Assignment_{input.AssignmentId}";
        string fileUrl = await _storage.UploadFileAsync(input.File, folderName);

        // 2. Record the submission
        var submission = new School.Domain.Entities.AssignmentSubmission
        {
            AssignmentId = input.AssignmentId,
            StudentId = student.Id,
            SubmissionDate = DateTime.UtcNow,
            FileUrl = fileUrl,
            StudentNotes = input.StudentNotes ?? "تم تسليم الواجب بنجاح"
        };
        
        _context.AssignmentSubmissions.Add(submission);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, fileUrl, message = "تم تسليم الواجب بنجاح" });
    }

    public class SubmissionInput
    {
        public int AssignmentId { get; set; }
        public string? StudentNotes { get; set; }
        public IFormFile File { get; set; } = null!;
    }

    [HttpGet("{id}/submissions")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> GetSubmissions(int id)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Email == userEmail);
        if (teacher == null) return Unauthorized();

        var ownsAssignment = await _context.Assignments.AnyAsync(assignment => assignment.Id == id && assignment.TeacherId == teacher.Id);
        if (!ownsAssignment)
        {
            return Forbid();
        }

        var submissions = await _context.AssignmentSubmissions
            .Include(s => s.Student)
            .Where(s => s.AssignmentId == id)
            .Select(s => new {
                s.Id,
                StudentName = s.Student!.FullName,
                s.SubmissionDate,
                s.FileUrl,
                s.StudentNotes,
                s.Grade,
                s.TeacherFeedback
            })
            .ToListAsync();

        return Ok(submissions);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteAssignment(int id)
    {
        var assignment = await _context.Assignments.FindAsync(id);
        if (assignment == null) return NotFound();

        if (User.IsInRole("Teacher"))
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Email == userEmail);
            if (teacher == null || assignment.TeacherId != teacher.Id)
            {
                return Forbid();
            }
        }

        // Remove related submissions first
        var submissions = _context.AssignmentSubmissions.Where(s => s.AssignmentId == id);
        _context.AssignmentSubmissions.RemoveRange(submissions);

        _context.Assignments.Remove(assignment);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }
}
