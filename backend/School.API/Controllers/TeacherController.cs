using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using School.Application.DTOs.Teacher;
using School.Application.Interfaces;

namespace School.API.Controllers;

[Authorize(Roles = "Teacher,Admin")]
[Route("api/teacher")]
public class TeacherController : BaseApiController
{
    private readonly ITeacherWorkflowService _teacherWorkflowService;

    public TeacherController(ITeacherWorkflowService teacherWorkflowService)
    {
        _teacherWorkflowService = teacherWorkflowService;
    }

    [HttpPost("grades")]
    public async Task<ActionResult<OperationResultDto>> RecordGrade(
        [FromBody] RecordGradeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _teacherWorkflowService.RecordGradeAsync(
                GetCurrentUserId(),
                request,
                User.IsInRole("Admin"),
                cancellationToken);

            return Ok(response);
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

    [HttpPost("attendance")]
    public async Task<ActionResult<OperationResultDto>> RecordAttendance(
        [FromBody] RecordAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _teacherWorkflowService.RecordAttendanceAsync(
                GetCurrentUserId(),
                request,
                User.IsInRole("Admin"),
                cancellationToken);

            return Ok(response);
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

    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Current user identifier is missing.");
    }
}
