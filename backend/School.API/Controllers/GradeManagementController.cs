using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Application.DTOs.GradeManagement;
using School.Application.Interfaces;
using School.Infrastructure.Data;

namespace School.API.Controllers;

[Authorize]
public class GradeManagementController : BaseApiController
{
    private readonly IGradeManagementService _gradeManagementService;
    private readonly SchoolDbContext _context;

    public GradeManagementController(
        IGradeManagementService gradeManagementService,
        SchoolDbContext context)
    {
        _gradeManagementService = gradeManagementService;
        _context = context;
    }

    [HttpPost("sessions/publish")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PublishGradeSessionsResultDto>> PublishSessions(
        [FromBody] PublishGradeSessionsRequest request,
        CancellationToken cancellationToken = default)
    {
        return await HandleAsync(() => _gradeManagementService.PublishSessionsAsync(request, cancellationToken));
    }

    [HttpGet("admin/dashboard")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminGradeSessionsDashboardDto>> GetAdminDashboard(
        [FromQuery] string? type,
        [FromQuery] DateTime? date,
        CancellationToken cancellationToken = default)
    {
        var result = await _gradeManagementService.GetAdminDashboardAsync(type, date, cancellationToken);
        return Ok(result);
    }

    [HttpGet("teacher/sessions")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IReadOnlyList<TeacherGradeSessionOptionDto>>> GetTeacherSessions(
        [FromQuery] int? teacherId,
        CancellationToken cancellationToken = default)
    {
        var effectiveTeacherId = await ResolveTeacherIdAsync(teacherId, cancellationToken);
        if (effectiveTeacherId <= 0)
        {
            return Ok(Array.Empty<TeacherGradeSessionOptionDto>());
        }

        var result = await _gradeManagementService.GetTeacherSessionsAsync(effectiveTeacherId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("teacher/sessions/{sessionId:int}/gradebook")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<TeacherSessionGradebookDto>> GetTeacherGradebook(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        var teacherId = await GetCurrentTeacherIdAsync(cancellationToken);
        try
        {
            var result = await _gradeManagementService.GetTeacherGradebookAsync(
                sessionId,
                teacherId,
                User.IsInRole("Admin"),
                cancellationToken);

            if (result == null)
            {
                return NotFound(new { message = "Grade session not found." });
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("teacher/grades")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<GradeOperationResultDto>> SaveTeacherGrades(
        [FromBody] SaveTeacherSessionGradesRequest request,
        CancellationToken cancellationToken = default)
    {
        var teacherId = await GetCurrentTeacherIdAsync(cancellationToken);
        return await HandleAsync(() => _gradeManagementService.SaveTeacherGradesAsync(
            request,
            teacherId,
            User.IsInRole("Admin"),
            cancellationToken));
    }

    [HttpPost("teacher/sessions/{sessionId:int}/approve")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<GradeOperationResultDto>> ApproveTeacherUpload(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        var teacherId = await GetCurrentTeacherIdAsync(cancellationToken);
        return await HandleAsync(() => _gradeManagementService.ApproveTeacherUploadAsync(
            sessionId,
            teacherId,
            User.IsInRole("Admin"),
            cancellationToken));
    }

    private async Task<ActionResult<T>> HandleAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<int> ResolveTeacherIdAsync(int? teacherId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin"))
        {
            return teacherId.GetValueOrDefault();
        }

        return await GetCurrentTeacherIdAsync(cancellationToken) ?? 0;
    }

    private async Task<int?> GetCurrentTeacherIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return await _context.Teachers
            .AsNoTracking()
            .Where(teacher => teacher.UserId == userId || teacher.Email == email)
            .Select(teacher => (int?)teacher.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
