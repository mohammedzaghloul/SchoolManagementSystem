using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Application.Features.Teachers.Commands;
using School.Application.Features.Teachers.Queries;
using School.Infrastructure.Data;
using School.Infrastructure.Identity;

namespace School.API.Controllers;

[Authorize]
public class TeachersController : BaseApiController
{
    private readonly SchoolDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    
    public TeachersController(
        SchoolDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<TeacherDto>>> GetTeachers()
    {
        var result = await Mediator.Send(new GetTeachersQuery());
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TeacherDto>> GetTeacher(int id)
    {
        var result = await Mediator.Send(new GetTeacherByIdQuery { Id = id });
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<int>> CreateTeacher([FromBody] CreateTeacherCommand command)
    {
        var fullName = Normalize(command.FullName);
        var email = NormalizeEmail(command.Email);
        var phone = Normalize(command.Phone) ?? string.Empty;
        var password = string.IsNullOrWhiteSpace(command.Password) ? "Teacher@123" : command.Password.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "يرجى إدخال اسم المعلم والبريد الإلكتروني." });
        }

        if (await _context.Teachers.AnyAsync(teacher => teacher.Email == email) || await _userManager.FindByEmailAsync(email) != null)
        {
            return BadRequest(new { message = "يوجد حساب معلم أو مستخدم آخر مسجل بهذا البريد الإلكتروني." });
        }

        await EnsureRoleExistsAsync("Teacher");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            PhoneNumber = phone
        };

        var createUserResult = await _userManager.CreateAsync(user, password);
        if (!createUserResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "تعذر إنشاء حساب دخول المعلم.",
                errors = createUserResult.Errors.Select(error => error.Description)
            });
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, "Teacher");
        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return BadRequest(new
            {
                message = "تم إنشاء المستخدم لكن تعذر منحه صلاحية المعلم.",
                errors = addRoleResult.Errors.Select(error => error.Description)
            });
        }

        var teacher = new School.Domain.Entities.Teacher
        {
            UserId = user.Id,
            FullName = fullName,
            Email = email,
            Phone = phone,
            IsActive = command.IsActive
        };

        if (command.SubjectId.HasValue)
        {
            var subject = await _context.Subjects.FindAsync(command.SubjectId.Value);
            if (subject != null)
            {
                teacher.Subjects.Add(subject);
            }
        }

        _context.Teachers.Add(teacher);
        await _context.SaveChangesAsync();

        user.TeacherId = teacher.Id;
        var updateUserResult = await _userManager.UpdateAsync(user);
        if (!updateUserResult.Succeeded)
        {
            _context.Teachers.Remove(teacher);
            await _context.SaveChangesAsync();
            await _userManager.DeleteAsync(user);

            return BadRequest(new
            {
                message = "تم إنشاء المعلم لكن تعذر ربط حساب الدخول به.",
                errors = updateUserResult.Errors.Select(error => error.Description)
            });
        }

        return Ok(teacher.Id);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<bool>> UpdateTeacher(int id, [FromBody] UpdateTeacherCommand command)
    {
        if (id != command.Id) return BadRequest();

        var teacher = await _context.Teachers
            .Include(t => t.Subjects)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (teacher == null)
        {
            return NotFound(new { message = "المعلم غير موجود." });
        }

        var fullName = Normalize(command.FullName) ?? teacher.FullName ?? string.Empty;
        var email = NormalizeEmail(command.Email) ?? teacher.Email ?? string.Empty;
        var phone = Normalize(command.Phone) ?? string.Empty;

        var emailExists = await _context.Teachers.AnyAsync(item => item.Id != teacher.Id && item.Email == email);
        if (emailExists)
        {
            return BadRequest(new { message = "يوجد معلم آخر مسجل بنفس البريد الإلكتروني." });
        }

        var linkedUser = await FindLinkedTeacherUserAsync(teacher);
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
                linkedUser = await ProvisionTeacherIdentityUserAsync(teacher, email, fullName, phone);
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
        linkedUser.TeacherId = teacher.Id;

        var userUpdateResult = await _userManager.UpdateAsync(linkedUser);
        if (!userUpdateResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "تعذر تحديث حساب دخول المعلم.",
                errors = userUpdateResult.Errors.Select(error => error.Description)
            });
        }

        teacher.UserId = linkedUser.Id;
        teacher.FullName = fullName;
        teacher.Email = email;
        teacher.Phone = phone;
        teacher.IsActive = command.IsActive;

        if (command.SubjectId.HasValue)
        {
            teacher.Subjects.Clear();
            var subject = await _context.Subjects.FindAsync(command.SubjectId.Value);
            if (subject != null)
            {
                teacher.Subjects.Add(subject);
            }
        }
        else
        {
            teacher.Subjects.Clear();
        }

        await _context.SaveChangesAsync();
        return Ok(true);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<bool>> DeleteTeacher(int id)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(item => item.Id == id);
        if (teacher == null)
        {
            return NotFound(new { message = "المعلم غير موجود." });
        }

        var linkedUser = await FindLinkedTeacherUserAsync(teacher);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            await _context.ClassRooms
                .Where(classRoom => classRoom.TeacherId == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(classRoom => classRoom.TeacherId, (int?)null));

            await _context.Subjects
                .Where(subject => subject.TeacherId == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(subject => subject.TeacherId, (int?)null));

            await _context.Videos
                .Where(video => video.TeacherId == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(video => video.TeacherId, (int?)null));

            await _context.Schedules
                .Where(schedule => schedule.TeacherId == id)
                .ExecuteDeleteAsync();

            await _context.Sessions
                .Where(session => session.TeacherId == id)
                .ExecuteDeleteAsync();

            await _context.Assignments
                .Where(assignment => assignment.TeacherId == id)
                .ExecuteDeleteAsync();

            await _context.Exams
                .Where(exam => exam.TeacherId == id)
                .ExecuteDeleteAsync();

            _context.Teachers.Remove(teacher);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return Conflict(new
            {
                message = "لا يمكن حذف المعلم الآن لأنه مرتبط ببيانات مدرسية أخرى. راجع الجداول أو المواد أو الاختبارات المرتبطة به أولاً."
            });
        }

        if (linkedUser != null)
        {
            await _userManager.DeleteAsync(linkedUser);
        }

        return Ok(true);
    }

    [HttpPatch("{id}/toggle-status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> ToggleTeacherStatus(int id)
    {
        var teacher = await _context.Teachers.FindAsync(id);
        if (teacher == null) return NotFound();
        
        teacher.IsActive = !teacher.IsActive;
        await _context.SaveChangesAsync();
        
        return Ok(new { teacher.Id, teacher.IsActive });
    }

    [HttpPost("activate-all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> ActivateAllTeachers()
    {
        var teachers = await _context.Teachers.Where(t => !t.IsActive).ToListAsync();
        foreach (var t in teachers) t.IsActive = true;
        await _context.SaveChangesAsync();
        return Ok(new { updated = teachers.Count });
    }

    private async Task<ApplicationUser?> FindLinkedTeacherUserAsync(School.Domain.Entities.Teacher teacher)
    {
        if (!string.IsNullOrWhiteSpace(teacher.UserId))
        {
            var byUserId = await _userManager.FindByIdAsync(teacher.UserId);
            if (byUserId != null)
            {
                return byUserId;
            }
        }

        if (!string.IsNullOrWhiteSpace(teacher.Email))
        {
            var byEmail = await _userManager.FindByEmailAsync(teacher.Email);
            if (byEmail != null)
            {
                return byEmail;
            }
        }

        return null;
    }

    private async Task<ApplicationUser> ProvisionTeacherIdentityUserAsync(School.Domain.Entities.Teacher teacher, string email, string fullName, string phone)
    {
        await EnsureRoleExistsAsync("Teacher");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            PhoneNumber = phone,
            TeacherId = teacher.Id
        };

        var createUserResult = await _userManager.CreateAsync(user, "Teacher@123");
        if (!createUserResult.Succeeded)
        {
            throw new InvalidOperationException("تعذر استكمال حساب دخول المعلم الحالي.");
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, "Teacher");
        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            throw new InvalidOperationException("تعذر استكمال صلاحية حساب المعلم الحالي.");
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
