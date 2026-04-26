using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using School.Application.DTOs.Admin;
using School.Application.DTOs.Teacher;
using School.Application.Interfaces;

namespace School.API.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : BaseApiController
{
    private const string SystemOwnerAdminEmail = "mohammedzaghloul0123@gmail.com";

    private readonly IAdministrationService _administrationService;

    public AdminController(IAdministrationService administrationService)
    {
        _administrationService = administrationService;
    }

    [HttpPost("admins")]
    public async Task<ActionResult<UserSummaryDto>> CreateAdmin(
        [FromBody] CreateAdminRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsSystemOwnerAdmin())
        {
            return StatusCode(403, new { message = "إنشاء مدير جديد متاح فقط لمالك النظام." });
        }

        try
        {
            var response = await _administrationService.CreateAdminAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("permissions")]
    public ActionResult<object> GetPermissions()
    {
        return Ok(new
        {
            canCreateAdmins = IsSystemOwnerAdmin(),
            ownerAdminEmail = SystemOwnerAdminEmail
        });
    }

    [HttpPost("teachers")]
    public async Task<ActionResult<UserSummaryDto>> CreateTeacher(
        [FromBody] CreateTeacherRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _administrationService.CreateTeacherAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("students")]
    public async Task<ActionResult<UserSummaryDto>> CreateStudent(
        [FromBody] CreateStudentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _administrationService.CreateStudentAsync(request, cancellationToken);
            return Ok(response);
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

    [HttpPut("users/{userId}/role")]
    public async Task<ActionResult<UserSummaryDto>> AssignRole(
        string userId,
        [FromBody] AssignRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.Role, "Admin", StringComparison.OrdinalIgnoreCase) && !IsSystemOwnerAdmin())
        {
            return StatusCode(403, new { message = "ترقية أي حساب إلى مدير النظام متاحة فقط لمالك النظام." });
        }

        try
        {
            var response = await _administrationService.AssignRoleAsync(userId, request, cancellationToken);
            return Ok(response);
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

    [HttpPut("students/{studentId:int}/subjects")]
    public async Task<ActionResult<OperationResultDto>> AssignStudentSubjects(
        int studentId,
        [FromBody] AssignStudentSubjectsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _administrationService.AssignStudentSubjectsAsync(studentId, request, cancellationToken);
            return Ok(response);
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

    [HttpPost("schedules")]
    public async Task<ActionResult<ScheduleDto>> CreateSchedule(
        [FromBody] CreateScheduleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _administrationService.CreateScheduleAsync(request, cancellationToken);
            return Ok(response);
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

    private bool IsSystemOwnerAdmin()
    {
        var email =
            User.FindFirstValue(ClaimTypes.Email) ??
            User.FindFirstValue("email") ??
            User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress") ??
            User.Identity?.Name;

        return string.Equals(email, SystemOwnerAdminEmail, StringComparison.OrdinalIgnoreCase);
    }
}
