using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
public class GradeController : BaseApiController
{
    private readonly School.Infrastructure.Data.SchoolDbContext _context;

    public GradeController(School.Infrastructure.Data.SchoolDbContext context)
    {
        _context = context;
    }

    [HttpGet("student")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyGrades()
    {
        var userEmail = User.FindFirstValue(System.Security.Claims.ClaimTypes.Email);
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == userEmail);
        if (student == null) return NotFound("Student not found");

        var grades = await _context.GradeRecords
            .Include(g => g.Subject)
            .Where(g => g.StudentId == student.Id)
            .Select(g => new {
                g.Id,
                Value = g.Score,
                g.GradeType,
                g.Date,
                SubjectName = g.Subject.Name
            })
            .ToListAsync();

        return Ok(grades);
    }

    [HttpGet("student/{studentId}")]
    [Authorize(Roles = "Admin,Teacher,Parent")]
    public async Task<IActionResult> GetStudentGrades(int studentId)
    {
        var grades = await _context.GradeRecords
            .Include(g => g.Subject)
            .Where(g => g.StudentId == studentId)
            .ToListAsync();

        return Ok(grades);
    }

    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> UpsertGrade([FromBody] School.Domain.Entities.GradeRecord grade)
    {
        if (grade.Id == 0)
        {
            grade.Date = DateTime.UtcNow;
            _context.GradeRecords.Add(grade);
        }
        else
        {
            _context.GradeRecords.Update(grade);
        }

        await _context.SaveChangesAsync();
        return Ok(grade);
    }
}
