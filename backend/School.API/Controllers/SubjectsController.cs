using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Application.Features.Subjects.Commands;
using School.Infrastructure.Data;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
[Route("api/Subjects")]
[Route("api/Subject")]
public class SubjectsController : BaseApiController
{
    private readonly SchoolDbContext _context;

    public SubjectsController(SchoolDbContext context)
    {
        _context = context;
    }

    [HttpGet("teacher/{teacherId?}")]
    public async Task<ActionResult> GetTeacherSubjects(int teacherId = 0, [FromQuery] string? term = null)
    {
        var currentTeacher = await GetCurrentTeacherAsync();
        var isAdmin = User.IsInRole("Admin");

        IQueryable<School.Domain.Entities.Subject> query = _context.Subjects.AsNoTracking();

        if (currentTeacher != null)
        {
            query = query.Where(subject => subject.TeacherId == currentTeacher.Id);
        }
        else if (isAdmin && teacherId > 0)
        {
            query = query.Where(subject => subject.TeacherId == teacherId);
        }

        query = ApplySubjectFilters(query, term, includeInactive: isAdmin);

        var subjects = await query
            .OrderBy(subject => subject.Name)
            .Select(subject => new
            {
                subject.Id,
                subject.Name,
                subject.Code,
                subject.Description,
                subject.TeacherId,
                subject.ClassRoomId,
                subject.Term,
                subject.IsActive
            })
            .ToListAsync();

        return Ok(subjects);
    }

    [HttpGet]
    public async Task<ActionResult> GetSubjects([FromQuery] string? term = null)
    {
        var isAdmin = User.IsInRole("Admin");
        var query = ApplySubjectFilters(_context.Subjects.AsNoTracking(), term, includeInactive: isAdmin);

        var subjects = await query
            .OrderBy(subject => subject.Name)
            .Select(subject => new
            {
                subject.Id,
                subject.Name,
                subject.Code,
                subject.Description,
                subject.TeacherId,
                subject.ClassRoomId,
                subject.Term,
                subject.IsActive
            })
            .ToListAsync();

        return Ok(subjects);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetSubject(int id)
    {
        var isAdmin = User.IsInRole("Admin");
        var subject = await _context.Subjects
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Where(item => isAdmin || item.IsActive)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Code,
                item.Description,
                item.TeacherId,
                item.ClassRoomId,
                item.Term,
                item.IsActive
            })
            .FirstOrDefaultAsync();

        if (subject == null)
        {
            return NotFound(new { message = "المادة غير موجودة." });
        }

        return Ok(subject);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<int>> CreateSubject([FromBody] CreateSubjectCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || string.IsNullOrWhiteSpace(command.Code))
        {
            return BadRequest(new { message = "يرجى إدخال اسم المادة وكود المادة." });
        }

        var subject = new School.Domain.Entities.Subject
        {
            Name = command.Name.Trim(),
            Code = command.Code.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            ClassRoomId = command.ClassRoomId,
            TeacherId = command.TeacherId,
            Term = NormalizeTermOrDefault(command.Term),
            IsActive = command.IsActive
        };

        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();

        return Ok(subject.Id);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<bool>> UpdateSubject(int id, [FromBody] UpdateSubjectCommand command)
    {
        if (id != command.Id) return BadRequest();

        var subject = await _context.Subjects.FirstOrDefaultAsync(item => item.Id == id);
        if (subject == null)
        {
            return NotFound(new { message = "المادة غير موجودة." });
        }

        subject.Name = string.IsNullOrWhiteSpace(command.Name) ? subject.Name : command.Name.Trim();
        subject.Code = string.IsNullOrWhiteSpace(command.Code) ? subject.Code : command.Code.Trim();
        subject.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        subject.TeacherId = command.TeacherId;
        subject.ClassRoomId = command.ClassRoomId;
        subject.Term = NormalizeTerm(command.Term) ?? subject.Term ?? "الترم الأول";
        subject.IsActive = command.IsActive;

        await _context.SaveChangesAsync();
        return Ok(true);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<bool>> DeleteSubject(int id)
    {
        var subject = await _context.Subjects.FindAsync(id);
        if (subject == null)
        {
            return Ok(true);
        }

        _context.Subjects.Remove(subject);
        await _context.SaveChangesAsync();
        return Ok(true);
    }

    private IQueryable<School.Domain.Entities.Subject> ApplySubjectFilters(
        IQueryable<School.Domain.Entities.Subject> query,
        string? term,
        bool includeInactive)
    {
        if (!includeInactive)
        {
            query = query.Where(subject => subject.IsActive);
        }

        var normalizedTerm = NormalizeTerm(term);
        if (!string.IsNullOrWhiteSpace(normalizedTerm) && normalizedTerm != "الكل")
        {
            query = query.Where(subject => subject.Term == normalizedTerm);
        }

        return query;
    }

    private async Task<School.Domain.Entities.Teacher?> GetCurrentTeacherAsync()
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

    private static string? NormalizeTerm(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        return term.Trim();
    }

    private static string NormalizeTermOrDefault(string? term)
    {
        return NormalizeTerm(term) ?? "الترم الأول";
    }
}
