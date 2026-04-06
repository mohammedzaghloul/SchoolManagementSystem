using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Application.Features.Account.Commands;
using School.Application.Interfaces;
using School.Domain.Entities;
using School.Infrastructure.Data;
using School.Infrastructure.Identity;

namespace School.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SchoolDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public AccountController(
        IMediator mediator,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        SchoolDbContext context,
        IFileStorageService fileStorage)
    {
        _mediator = mediator;
        _roleManager = roleManager;
        _userManager = userManager;
        _context = context;
        _fileStorage = fileStorage;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command)
    {
        Console.WriteLine($"[CONTROLLER] Login start for: {command.Email}");
        try
        {
            var token = await _mediator.Send(command);
            if (token == null) 
            {
                Console.WriteLine($"[CONTROLLER] Login failed - token is null for: {command.Email}");
                return Unauthorized(new { message = "بيانات الدخول غير صحيحة." });
            }
            Console.WriteLine($"[CONTROLLER] Login success - returning token for: {command.Email}");
            return Ok(new { token });
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"[CONTROLLER] Login blocked for inactive account: {command.Email}");
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPost("create-roles")]
    public async Task<IActionResult> CreateRoles()
    {
        string[] roles = { "Admin", "Teacher", "Student", "Parent" };
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }
        return Ok();
    }

    [HttpPost("add-admin")]
    public async Task<IActionResult> AddAdmin(RegisterAdminCommand command)
    {
        var result = await _mediator.Send(command);
        if (result) return Ok();
        return BadRequest();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("add-parent")]
    public async Task<ActionResult<object>> AddParent([FromBody] AddParentRequest request)
    {
        var fullName = NormalizeOptionalValue(request.FullName);
        var email = NormalizeOptionalValue(request.Email)?.ToLowerInvariant();
        var phone = NormalizeOptionalValue(request.Phone) ?? string.Empty;
        var address = NormalizeOptionalValue(request.Address);
        var password = string.IsNullOrWhiteSpace(request.Password) ? "Parent@123" : request.Password.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "يرجى إدخال الاسم الكامل والبريد الإلكتروني." });
        }

        if (await _userManager.FindByEmailAsync(email) != null || await _context.Parents.AnyAsync(parent => parent.Email == email))
        {
            return BadRequest(new { message = "يوجد حساب آخر مسجل بهذا البريد الإلكتروني." });
        }

        if (!await _roleManager.RoleExistsAsync("Parent"))
        {
            await _roleManager.CreateAsync(new IdentityRole("Parent"));
        }

        ApplicationUser? createdUser = null;

        try
        {
            createdUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                PhoneNumber = phone
            };

            var createUserResult = await _userManager.CreateAsync(createdUser, password);
            if (!createUserResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = "تعذر إنشاء حساب ولي الأمر.",
                    errors = createUserResult.Errors.Select(error => error.Description)
                });
            }

            var addRoleResult = await _userManager.AddToRoleAsync(createdUser, "Parent");
            if (!addRoleResult.Succeeded)
            {
                await _userManager.DeleteAsync(createdUser);
                return BadRequest(new
                {
                    message = "تم إنشاء المستخدم لكن تعذر منحه صلاحية ولي الأمر.",
                    errors = addRoleResult.Errors.Select(error => error.Description)
                });
            }

            var parent = new Parent
            {
                UserId = createdUser.Id,
                FullName = fullName,
                Email = email,
                Phone = phone,
                Address = address
            };

            _context.Parents.Add(parent);
            await _context.SaveChangesAsync();

            createdUser.ParentId = parent.Id;
            var updateUserResult = await _userManager.UpdateAsync(createdUser);
            if (!updateUserResult.Succeeded)
            {
                _context.Parents.Remove(parent);
                await _context.SaveChangesAsync();
                await _userManager.DeleteAsync(createdUser);

                return BadRequest(new
                {
                    message = "تم إنشاء ولي الأمر لكن تعذر ربط الحساب به.",
                    errors = updateUserResult.Errors.Select(error => error.Description)
                });
            }

            return Ok(new
            {
                parent.Id,
                userId = parent.UserId,
                fullName = parent.FullName,
                parent.Email,
                parent.Phone,
                parent.Address,
                childrenCount = 0
            });
        }
        catch
        {
            if (createdUser != null)
            {
                var user = await _userManager.FindByIdAsync(createdUser.Id);
                if (user != null)
                {
                    await _userManager.DeleteAsync(user);
                }
            }

            throw;
        }
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<ActionResult<UserProfileResponse>> GetProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(await BuildProfileResponseAsync(user));
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var fullName = NormalizeOptionalValue(request.FullName);
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest(new { message = "الاسم الكامل مطلوب." });
        }

        var role = await GetPrimaryRoleAsync(user);
        user.FullName = fullName;
        user.PhoneNumber = NormalizeOptionalValue(request.Phone);

        if (role == "Student")
        {
            var student = await FindStudentAsync(user, track: true);
            if (student != null)
            {
                student.FullName = user.FullName;
                student.Phone = user.PhoneNumber;
            }
        }
        else if (role == "Teacher")
        {
            var teacher = await FindTeacherAsync(user, track: true);
            if (teacher != null)
            {
                teacher.FullName = user.FullName;
                teacher.Phone = user.PhoneNumber;
            }
        }
        else if (role == "Parent")
        {
            var parent = await FindParentAsync(user, track: true);
            if (parent != null)
            {
                parent.FullName = user.FullName;
                parent.Phone = user.PhoneNumber ?? string.Empty;

                if (request.Address != null)
                {
                    parent.Address = NormalizeOptionalValue(request.Address);
                }
            }
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "تعذر تحديث الملف الشخصي.",
                errors = result.Errors.Select(error => error.Description)
            });
        }

        await _context.SaveChangesAsync();

        return Ok(await BuildProfileResponseAsync(user, role));
    }

    [Authorize]
    [HttpPost("avatar")]
    public async Task<ActionResult<object>> UploadAvatar([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "يرجى اختيار صورة صالحة." });
        }

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "الملف المرفوع يجب أن يكون صورة." });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new { message = "حجم الصورة يجب ألا يتجاوز 5 ميجابايت." });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var role = await GetPrimaryRoleAsync(user);
        var previousAvatar = user.ProfilePictureUrl;
        var uploadedPath = await _fileStorage.UploadFileAsync(file, "profiles");

        if (string.IsNullOrWhiteSpace(uploadedPath))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "تعذر حفظ الصورة الآن." });
        }

        user.ProfilePictureUrl = uploadedPath;

        if (role == "Student")
        {
            var student = await FindStudentAsync(user, track: true);
            if (student != null)
            {
                student.ProfilePictureUrl = uploadedPath;
            }
        }
        else if (role == "Teacher")
        {
            var teacher = await FindTeacherAsync(user, track: true);
            if (teacher != null)
            {
                teacher.ProfilePictureUrl = uploadedPath;
            }
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "تعذر تحديث صورة الملف الشخصي.",
                errors = result.Errors.Select(error => error.Description)
            });
        }

        await _context.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(previousAvatar) && !string.Equals(previousAvatar, uploadedPath, StringComparison.OrdinalIgnoreCase))
        {
            await _fileStorage.DeleteFileAsync(previousAvatar);
        }

        var avatarUrl = BuildPublicUrl(uploadedPath);
        return Ok(new
        {
            avatar = avatarUrl,
            url = avatarUrl
        });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "يرجى إدخال كلمة المرور الحالية والجديدة." });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        // Master Override for Demo: Let Mohamed Ali use Student@12345 to reset his password even if he forgot the old one
        if (user.Email == "mohamed@school.com" && request.CurrentPassword == "Student@12345")
        {
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
            if (resetResult.Succeeded)
            {
                return Ok(new { message = "تم تغيير كلمة المرور بنجاح (وضع العرض التوضيحي)." });
            }
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "تعذر تغيير كلمة المرور.",
                errors = result.Errors.Select(error => error.Description)
            });
        }

        return Ok(new { message = "تم تغيير كلمة المرور بنجاح." });
    }

    private async Task<UserProfileResponse> BuildProfileResponseAsync(ApplicationUser user, string? role = null)
    {
        role ??= await GetPrimaryRoleAsync(user);

        string fullName = user.FullName;
        string email = user.Email ?? string.Empty;
        string? phone = user.PhoneNumber;
        string? address = null;
        string? avatar = user.ProfilePictureUrl;

        if (role == "Student")
        {
            var student = await FindStudentAsync(user, track: false);
            if (student != null)
            {
                fullName = student.FullName ?? fullName;
                phone = string.IsNullOrWhiteSpace(student.Phone) ? phone : student.Phone;
                avatar = string.IsNullOrWhiteSpace(student.ProfilePictureUrl) ? avatar : student.ProfilePictureUrl;
            }
        }
        else if (role == "Teacher")
        {
            var teacher = await FindTeacherAsync(user, track: false);
            if (teacher != null)
            {
                fullName = teacher.FullName ?? fullName;
                phone = string.IsNullOrWhiteSpace(teacher.Phone) ? phone : teacher.Phone;
                avatar = string.IsNullOrWhiteSpace(teacher.ProfilePictureUrl) ? avatar : teacher.ProfilePictureUrl;
            }
        }
        else if (role == "Parent")
        {
            var parent = await FindParentAsync(user, track: false);
            if (parent != null)
            {
                fullName = parent.FullName;
                phone = string.IsNullOrWhiteSpace(parent.Phone) ? phone : parent.Phone;
                address = parent.Address;
            }
        }

        return new UserProfileResponse
        {
            Id = user.Id,
            FullName = fullName,
            Email = email,
            Phone = phone,
            Address = address,
            Avatar = BuildPublicUrl(avatar),
            Role = role ?? string.Empty
        };
    }

    private async Task<string?> GetPrimaryRoleAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.FirstOrDefault();
    }

    private async Task<Student?> FindStudentAsync(ApplicationUser user, bool track)
    {
        IQueryable<Student> query = _context.Students;
        if (!track)
        {
            query = query.AsNoTracking();
        }

        if (user.StudentId.HasValue)
        {
            var byId = await query.FirstOrDefaultAsync(student => student.Id == user.StudentId.Value);
            if (byId != null)
            {
                return byId;
            }
        }

        return await query.FirstOrDefaultAsync(student => student.UserId == user.Id || student.Email == user.Email);
    }

    private async Task<Teacher?> FindTeacherAsync(ApplicationUser user, bool track)
    {
        IQueryable<Teacher> query = _context.Teachers;
        if (!track)
        {
            query = query.AsNoTracking();
        }

        if (user.TeacherId.HasValue)
        {
            var byId = await query.FirstOrDefaultAsync(teacher => teacher.Id == user.TeacherId.Value);
            if (byId != null)
            {
                return byId;
            }
        }

        return await query.FirstOrDefaultAsync(teacher => teacher.UserId == user.Id || teacher.Email == user.Email);
    }

    private async Task<Parent?> FindParentAsync(ApplicationUser user, bool track)
    {
        IQueryable<Parent> query = _context.Parents;
        if (!track)
        {
            query = query.AsNoTracking();
        }

        if (user.ParentId.HasValue)
        {
            var byId = await query.FirstOrDefaultAsync(parent => parent.Id == user.ParentId.Value);
            if (byId != null)
            {
                return byId;
            }
        }

        return await query.FirstOrDefaultAsync(parent => parent.UserId == user.Id || parent.Email == user.Email);
    }

    private string? BuildPublicUrl(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return null;
        }

        if (Uri.TryCreate(fileUrl, UriKind.Absolute, out _))
        {
            return fileUrl;
        }

        return $"{Request.Scheme}://{Request.Host}{fileUrl}";
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class UpdateProfileRequest
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class AddParentRequest
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Password { get; set; }
    }

    public class UserProfileResponse
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Avatar { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
