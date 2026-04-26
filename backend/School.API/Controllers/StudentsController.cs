using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using School.Application.DTOs.Students;
using Microsoft.EntityFrameworkCore;
using School.Application.Features.Students.Commands;
using School.Application.Features.Students.Queries;
using School.Application.Interfaces;
using School.Domain.Entities;
using School.Infrastructure.Data;
using School.Infrastructure.Identity;

namespace School.API.Controllers;

[Authorize]
public class StudentsController : BaseApiController
{
    private readonly SchoolDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IStudentQueryService _studentQueryService;

    public StudentsController(
        SchoolDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IStudentQueryService studentQueryService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _studentQueryService = studentQueryService;
    }

    [HttpGet("search")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<StudentSearchResultDto>>> SearchStudents(
        [FromQuery] string? q,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _studentQueryService.SearchStudentsAsync(q, limit, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<List<StudentDto>>> GetStudents([FromQuery] int? classRoomId)
    {
        var query = new GetStudentsQuery { ClassRoomId = classRoomId };
        var result = await Mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id:int}/dashboard")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StudentDashboardDto>> GetStudentDashboard(
        int id,
        CancellationToken cancellationToken = default)
    {
        var result = await _studentQueryService.GetStudentDashboardAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(new { message = "الطالب غير موجود." });
        }

        return Ok(result);
    }

    [HttpGet("{id:int}/grades")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<StudentGradeDto>>> GetStudentGradesForDashboard(
        int id,
        CancellationToken cancellationToken = default)
    {
        var result = await _studentQueryService.GetStudentGradesAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(new { message = "الطالب غير موجود." });
        }

        return Ok(result);
    }

    [HttpGet("{id:int}/attendance")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<StudentAttendanceDto>>> GetStudentAttendanceForDashboard(
        int id,
        CancellationToken cancellationToken = default)
    {
        var result = await _studentQueryService.GetStudentAttendanceAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(new { message = "الطالب غير موجود." });
        }

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentDto>> GetStudent(int id)
    {
        var result = await Mediator.Send(new GetStudentByIdQuery { Id = id });
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<ActionResult<int>> CreateStudent([FromBody] CreateStudentCommand command)
    {
        var fullName = Normalize(command.FullName);
        var email = NormalizeEmail(command.Email);
        var phone = Normalize(command.Phone) ?? string.Empty;
        var password = string.IsNullOrWhiteSpace(command.Password) ? "Student@123" : command.Password.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "يرجى إدخال اسم الطالب والبريد الإلكتروني." });
        }

        if (await _context.Students.AnyAsync(student => student.Email == email) || await _userManager.FindByEmailAsync(email) != null)
        {
            return BadRequest(new { message = "يوجد حساب طالب أو مستخدم آخر مسجل بهذا البريد الإلكتروني." });
        }

        await EnsureRoleExistsAsync("Student");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            PhoneNumber = phone,
            DeviceId = Guid.NewGuid().ToString()
        };

        var createUserResult = await _userManager.CreateAsync(user, password);
        if (!createUserResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "تعذر إنشاء حساب دخول الطالب.",
                errors = createUserResult.Errors.Select(error => error.Description)
            });
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, "Student");
        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return BadRequest(new
            {
                message = "تم إنشاء المستخدم لكن تعذر منحه صلاحية الطالب.",
                errors = addRoleResult.Errors.Select(error => error.Description)
            });
        }

        var student = new Student
        {
            UserId = user.Id,
            FullName = fullName,
            Email = email,
            Phone = phone,
            ClassRoomId = command.ClassRoomId,
            ParentId = command.ParentId,
            BirthDate = DateTime.UtcNow.AddYears(-10),
            QrCodeValue = Guid.NewGuid().ToString(),
            IsActive = command.IsActive ?? true
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        user.StudentId = student.Id;
        var updateUserResult = await _userManager.UpdateAsync(user);
        if (!updateUserResult.Succeeded)
        {
            _context.Students.Remove(student);
            await _context.SaveChangesAsync();
            await _userManager.DeleteAsync(user);

            return BadRequest(new
            {
                message = "تم إنشاء الطالب لكن تعذر ربط حساب الدخول به.",
                errors = updateUserResult.Errors.Select(error => error.Description)
            });
        }

        return Ok(student.Id);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<ActionResult<bool>> UpdateStudent(int id, [FromBody] UpdateStudentCommand command)
    {
        if (id != command.Id) return BadRequest();

        var student = await _context.Students.FirstOrDefaultAsync(item => item.Id == id);
        if (student == null)
        {
            return NotFound(new { message = "الطالب غير موجود." });
        }

        var fullName = Normalize(command.FullName) ?? student.FullName ?? string.Empty;
        var email = NormalizeEmail(command.Email) ?? student.Email ?? string.Empty;
        var phone = Normalize(command.Phone) ?? string.Empty;

        var emailExists = await _context.Students.AnyAsync(item => item.Id != student.Id && item.Email == email);
        if (emailExists)
        {
            return BadRequest(new { message = "يوجد طالب آخر مسجل بنفس البريد الإلكتروني." });
        }

        var linkedUser = await FindLinkedStudentUserAsync(student);
        var otherIdentityUser = await _userManager.FindByEmailAsync(email);
        if (linkedUser == null && otherIdentityUser != null)
        {
            linkedUser = otherIdentityUser;
        }
        else if (otherIdentityUser != null && linkedUser != null && otherIdentityUser.Id != linkedUser.Id)
        {
            return BadRequest(new { message = "يوجد مستخدم آخر مسجل بنفس البريد الإلكتروني." });
        }

        if (linkedUser == null)
        {
            try
            {
                linkedUser = await ProvisionStudentIdentityUserAsync(student, email, fullName, phone);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        linkedUser.UserName = email;
        linkedUser.Email = email;
        linkedUser.FullName = fullName;
        linkedUser.PhoneNumber = phone;
        linkedUser.StudentId = student.Id;
        linkedUser.DeviceId ??= Guid.NewGuid().ToString();

        var userUpdateResult = await _userManager.UpdateAsync(linkedUser);
        if (!userUpdateResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "تعذر تحديث حساب دخول الطالب.",
                errors = userUpdateResult.Errors.Select(error => error.Description)
            });
        }

        student.UserId = linkedUser.Id;
        student.FullName = fullName;
        student.Email = email;
        student.Phone = phone;
        student.ClassRoomId = command.ClassRoomId;
        student.ParentId = command.ParentId;
        student.IsActive = command.IsActive ?? student.IsActive;
        student.QrCodeValue ??= Guid.NewGuid().ToString();

        await _context.SaveChangesAsync();
        return Ok(true);
    }

    [HttpPatch("{id}/toggle-status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> ToggleStudentStatus(int id)
    {
        var student = await _context.Students.FindAsync(id);
        if (student == null)
        {
            return NotFound(new { message = "الطالب غير موجود." });
        }

        student.IsActive = !student.IsActive;
        await _context.SaveChangesAsync();

        return Ok(new { student.Id, student.IsActive });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<bool>> DeleteStudent(int id)
    {
        var student = await _context.Students.FirstOrDefaultAsync(item => item.Id == id);
        if (student == null)
        {
            return NotFound(new { message = "الطالب غير موجود." });
        }

        var linkedUser = await FindLinkedStudentUserAsync(student);

        _context.Students.Remove(student);
        await _context.SaveChangesAsync();

        if (linkedUser != null)
        {
            await _userManager.DeleteAsync(linkedUser);
        }

        return Ok(true);
    }

    private async Task<ApplicationUser?> FindLinkedStudentUserAsync(Student student)
    {
        if (!string.IsNullOrWhiteSpace(student.UserId))
        {
            var byUserId = await _userManager.FindByIdAsync(student.UserId);
            if (byUserId != null)
            {
                return byUserId;
            }
        }

        if (!string.IsNullOrWhiteSpace(student.Email))
        {
            var byEmail = await _userManager.FindByEmailAsync(student.Email);
            if (byEmail != null)
            {
                return byEmail;
            }
        }

        return null;
    }

    private async Task<ApplicationUser> ProvisionStudentIdentityUserAsync(Student student, string email, string fullName, string phone)
    {
        await EnsureRoleExistsAsync("Student");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            PhoneNumber = phone,
            StudentId = student.Id,
            DeviceId = Guid.NewGuid().ToString()
        };

        var createUserResult = await _userManager.CreateAsync(user, "Student@123");
        if (!createUserResult.Succeeded)
        {
            throw new InvalidOperationException("تعذر استكمال حساب دخول الطالب الحالي.");
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, "Student");
        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            throw new InvalidOperationException("تعذر استكمال صلاحية حساب الطالب الحالي.");
        }

        return user;
    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeEmail(string? value)
    {
        var normalized = Normalize(value);
        return normalized?.ToLowerInvariant();
    }
}
