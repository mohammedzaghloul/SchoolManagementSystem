using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Application.DTOs.Auth;
using School.Application.Features.Account.Commands;
using School.Application.Interfaces;
using School.Domain.Entities;
using School.Infrastructure.Data;
using School.Infrastructure.Identity;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

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
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly IEmailOtpService _emailOtpService;
    private readonly ITokenService _tokenService;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string TrustedDeviceLoginProvider = "TrustedLoginDevice";
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, ForgotPasswordOtpState> ForgotPasswordOtps = new();
    private static readonly ConcurrentDictionary<string, LoginOtpChallengeState> LoginOtpChallenges = new();

    public AccountController(
        IMediator mediator,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        SchoolDbContext context,
        IFileStorageService fileStorage,
        IConfiguration configuration,
        IEmailService emailService,
        IEmailOtpService emailOtpService,
        ITokenService tokenService,
        IHttpClientFactory httpClientFactory)
    {
        _mediator = mediator;
        _roleManager = roleManager;
        _userManager = userManager;
        _context = context;
        _fileStorage = fileStorage;
        _configuration = configuration;
        _emailService = emailService;
        _emailOtpService = emailOtpService;
        _tokenService = tokenService;
        _httpClientFactory = httpClientFactory;
    }
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        Console.WriteLine($"[CONTROLLER] Login start for: {command.Email}");

        try
        {
            var centralLogin = await TryLoginWithCentralAuthAsync(command, HttpContext.RequestAborted);
            if (centralLogin.Status == CentralLoginStatus.Success && !string.IsNullOrWhiteSpace(centralLogin.Token))
            {
                Console.WriteLine($"[CONTROLLER] CentralAuth login accepted for: {command.Email}");
                return Ok(new { token = centralLogin.Token });
            }

            if (centralLogin.Status is CentralLoginStatus.InvalidCredentials or CentralLoginStatus.LocalUserMissing)
            {
                return Unauthorized(new { message = centralLogin.Message ?? "بيانات الدخول غير صحيحة." });
            }

            var token = await _mediator.Send(command);
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine($"[CONTROLLER] Login failed - token is null for: {command.Email}");
                return Unauthorized(new { message = "بيانات الدخول غير صحيحة." });
            }

            return Ok(new { token });
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[CONTROLLER] Login unauthorized for: {command.Email} - {ex.Message}");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CONTROLLER] Login error for {command.Email}: {ex}");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "حدث خطأ أثناء تسجيل الدخول." });
        }
    }

    [AllowAnonymous]
    [HttpPost("login/verify-otp")]
    [Obsolete("Login OTP is no longer required on this route.")]
    public IActionResult VerifyLoginOtp()
    {
        return StatusCode(StatusCodes.Status410Gone, new { message = "لم يعد هذا المسار مستخدمًا." });
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
            return BadRequest(new { message = "ÙŠØ±Ø¬Ù‰ Ø¥Ø¯Ø®Ø§Ù„ Ø§Ù„Ø§Ø³Ù… Ø§Ù„ÙƒØ§Ù…Ù„ ÙˆØ§Ù„Ø¨Ø±ÙŠØ¯ Ø§Ù„Ø¥Ù„ÙƒØªØ±ÙˆÙ†ÙŠ." });
        }

        if (await _userManager.FindByEmailAsync(email) != null || await _context.Parents.AnyAsync(parent => parent.Email == email))
        {
            return BadRequest(new { message = "ÙŠÙˆØ¬Ø¯ Ø­Ø³Ø§Ø¨ Ø¢Ø®Ø± Ù…Ø³Ø¬Ù„ Ø¨Ù‡Ø°Ø§ Ø§Ù„Ø¨Ø±ÙŠØ¯ Ø§Ù„Ø¥Ù„ÙƒØªØ±ÙˆÙ†ÙŠ." });
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
                    message = "ØªØ¹Ø°Ø± Ø¥Ù†Ø´Ø§Ø¡ Ø­Ø³Ø§Ø¨ ÙˆÙ„ÙŠ Ø§Ù„Ø£Ù…Ø±.",
                    errors = createUserResult.Errors.Select(error => error.Description)
                });
            }

            var addRoleResult = await _userManager.AddToRoleAsync(createdUser, "Parent");
            if (!addRoleResult.Succeeded)
            {
                await _userManager.DeleteAsync(createdUser);
                return BadRequest(new
                {
                    message = "ØªÙ… Ø¥Ù†Ø´Ø§Ø¡ Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù… Ù„ÙƒÙ† ØªØ¹Ø°Ø± Ù…Ù†Ø­Ù‡ ØµÙ„Ø§Ø­ÙŠØ© ÙˆÙ„ÙŠ Ø§Ù„Ø£Ù…Ø±.",
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
                    message = "ØªÙ… Ø¥Ù†Ø´Ø§Ø¡ ÙˆÙ„ÙŠ Ø§Ù„Ø£Ù…Ø± Ù„ÙƒÙ† ØªØ¹Ø°Ø± Ø±Ø¨Ø· Ø§Ù„Ø­Ø³Ø§Ø¨ Ø¨Ù‡.",
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

    private async Task<CentralLoginResult> TryLoginWithCentralAuthAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue<bool>("CentralAuth:Enabled"))
        {
            return CentralLoginResult.Disabled;
        }

        var baseUrl = _configuration["CentralAuth:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return CentralLoginResult.Unavailable;
        }

        var apiKey = _configuration["CentralAuth:ForgotPasswordApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Without an API key we cannot safely resolve the linked Central account for this school email.
            // Fall back to local login rather than blocking sign-in.
            return CentralLoginResult.Disabled;
        }

        CentralUserLinkStatusResponse? linkStatus;
        try
        {
            linkStatus = await TryGetCentralLinkStatusAsync(baseUrl, apiKey, command.Email, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine($"[CONTROLLER] CentralAuth link status unavailable for {command.Email}: {ex.Message}");
            return CentralLoginResult.Unavailable;
        }

        if (linkStatus is null || !linkStatus.IsLinked || string.IsNullOrWhiteSpace(linkStatus.PlatformEmail))
        {
            // User is not linked to Central Auth, so treat CentralAuth login as disabled and allow local auth.
            return CentralLoginResult.Disabled;
        }

        var centralEmail = linkStatus.PlatformEmail.Trim();

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.PostAsJsonAsync(
                $"{baseUrl}/api/auth/login",
                new { email = centralEmail, password = command.Password },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return response.StatusCode == HttpStatusCode.Unauthorized
                    ? CentralLoginResult.InvalidCredentials
                    : CentralLoginResult.Unavailable;
            }

            var localUser = await _userManager.FindByEmailAsync(command.Email);
            if (localUser == null)
            {
                Console.WriteLine($"[CONTROLLER] CentralAuth accepted {command.Email}, but no local school user exists.");
                return CentralLoginResult.LocalUserMissing;
            }

            var role = await GetPrimaryRoleAsync(localUser) ?? "User";
            var token = _tokenService.CreateToken(
                localUser.Id,
                localUser.Email ?? command.Email,
                role,
                localUser.FullName);
            return CentralLoginResult.Success(token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine($"[CONTROLLER] CentralAuth login unavailable for {command.Email}: {ex.Message}");
            return CentralLoginResult.Unavailable;
        }
    }

    private async Task<CentralUserLinkStatusResponse?> TryGetCentralLinkStatusAsync(
        string baseUrl,
        string apiKey,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        var requestUri = $"{baseUrl}/api/integrations/user-links/status?externalUserId={Uri.EscapeDataString(externalUserId)}";
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("X-API-Key", apiKey);

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<CentralUserLinkStatusResponse>(cancellationToken: cancellationToken);
    }

    private sealed record CentralUserLinkStatusResponse(
        bool IsLinked,
        string? ExternalAppName,
        string? ExternalUserId,
        string? PlatformEmail,
        string? PlatformDisplayName,
        DateTimeOffset? LinkedAt);

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
            return BadRequest(new { message = "Ø§Ù„Ø§Ø³Ù… Ø§Ù„ÙƒØ§Ù…Ù„ Ù…Ø·Ù„ÙˆØ¨." });
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
                message = "ØªØ¹Ø°Ø± ØªØ­Ø¯ÙŠØ« Ø§Ù„Ù…Ù„Ù Ø§Ù„Ø´Ø®ØµÙŠ.",
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
            return BadRequest(new { message = "ÙŠØ±Ø¬Ù‰ Ø§Ø®ØªÙŠØ§Ø± ØµÙˆØ±Ø© ØµØ§Ù„Ø­Ø©." });
        }

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Ø§Ù„Ù…Ù„Ù Ø§Ù„Ù…Ø±ÙÙˆØ¹ ÙŠØ¬Ø¨ Ø£Ù† ÙŠÙƒÙˆÙ† ØµÙˆØ±Ø©." });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new { message = "Ø­Ø¬Ù… Ø§Ù„ØµÙˆØ±Ø© ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠØªØ¬Ø§ÙˆØ² 5 Ù…ÙŠØ¬Ø§Ø¨Ø§ÙŠØª." });
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
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "ØªØ¹Ø°Ø± Ø­ÙØ¸ Ø§Ù„ØµÙˆØ±Ø© Ø§Ù„Ø¢Ù†." });
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
                message = "ØªØ¹Ø°Ø± ØªØ­Ø¯ÙŠØ« ØµÙˆØ±Ø© Ø§Ù„Ù…Ù„Ù Ø§Ù„Ø´Ø®ØµÙŠ.",
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

    [AllowAnonymous]
    [HttpPost("forgot-password/send-otp")]
    public async Task<IActionResult> SendForgotPasswordOtp([FromBody] ForgotPasswordOtpRequest request)
    {
        // Force use the new secure service
        var normalizedEmail = NormalizeOptionalValue(request.Email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return BadRequest(new { message = "ÙŠØ±Ø¬Ù‰ Ø¥Ø¯Ø®Ø§Ù„ Ø¨Ø±ÙŠØ¯ Ø¥Ù„ÙƒØªØ±ÙˆÙ†ÙŠ ØµØ­ÙŠØ­." });
        }

        var response = await _emailOtpService.RequestPasswordResetOtpAsync(
            new ForgotPasswordRequest { Email = normalizedEmail });

        return Ok(new
        {
            success = response.Success,
            emailSent = response.EmailSent,
            devOtp = (string?)null,
            expiresAtUtc = response.ExpiresAtUtc,
            message = response.Message
        });
    }

    [AllowAnonymous]
    [HttpPost("forgot-password/verify-otp")]
    public async Task<IActionResult> VerifyForgotPasswordOtp([FromBody] VerifyForgotPasswordOtpRequest request)
    {
        var normalizedEmail = NormalizeOptionalValue(request.Email);
        var normalizedOtp = NormalizeOtpDigits(NormalizeOptionalValue(request.Otp));

        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(normalizedOtp))
        {
            return BadRequest(new { message = "Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª Ø§Ù„Ù…Ø·Ù„ÙˆØ¨Ø© Ù†Ø§Ù‚ØµØ©." });
        }

        try
        {
            // If it's a full reset request (with password)
            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                var response = await _emailOtpService.ResetPasswordAsync(new ResetPasswordRequest
                {
                    Email = normalizedEmail,
                    Otp = normalizedOtp,
                    NewPassword = request.NewPassword,
                    ConfirmPassword = request.NewPassword
                });
                return Ok(new { success = response.Success, message = response.Message });
            }

            // Otherwise just verify the OTP
            var isValid = await _emailOtpService.VerifyPasswordResetOtpOnlyAsync(normalizedEmail, normalizedOtp);
            if (!isValid) return BadRequest(new { message = "ÙƒÙˆØ¯ Ø§Ù„ØªØ­Ù‚Ù‚ ØºÙŠØ± ØµØ­ÙŠØ­." });
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        // Standard Change Password (if CurrentPassword is provided)
        if (!string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (result.Succeeded)
            {
                return Ok(new { message = "ØªÙ… ØªØºÙŠÙŠØ± ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ± Ø¨Ù†Ø¬Ø§Ø­." });
            }
            
            // If it failed, we can still fall back to reset if it's a demo account or for convenience
        }
        
        // Master Reset / Forgot Password fallback (using Reset Token)
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        
        if (!resetResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "ØªØ¹Ø°Ø± ØªØºÙŠÙŠØ± ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ±.",
                errors = resetResult.Errors.Select(error => error.Description)
            });
        }

        return Ok(new { message = "ØªÙ… ØªØ­Ø¯ÙŠØ« ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ± Ø¨Ù†Ø¬Ø§Ø­." });
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
        if (roles.Contains("Admin"))
        {
            return "Admin";
        }

        return roles.FirstOrDefault();
    }

    private enum CentralLoginStatus
    {
        Disabled,
        Success,
        InvalidCredentials,
        LocalUserMissing,
        Unavailable
    }

    private sealed record CentralLoginResult(CentralLoginStatus Status, string? Token = null, string? Message = null)
    {
        public static CentralLoginResult Disabled { get; } = new(CentralLoginStatus.Disabled);
        public static CentralLoginResult Unavailable { get; } = new(CentralLoginStatus.Unavailable);
        public static CentralLoginResult InvalidCredentials { get; } = new(
            CentralLoginStatus.InvalidCredentials,
            Message: "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
        public static CentralLoginResult LocalUserMissing { get; } = new(
            CentralLoginStatus.LocalUserMissing,
            Message: "الحساب موجود في منصة الدخول الموحدة لكنه غير مربوط بحساب داخل المدرسة.");
        public static CentralLoginResult Success(string token) => new(CentralLoginStatus.Success, token);
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

    private static string GenerateOtp()
    {
        return RandomNumberGenerator.GetInt32(100000, 999999).ToString();
    }

    private string HashOtp(string scope, string otp)
    {
        var secret = _configuration["Jwt:Secret"] ?? "school-api-otp-development-secret";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{secret}:{scope}:{otp}")));
    }

    private string HashDeviceKey(string userId, string deviceKey)
    {
        var secret = _configuration["Jwt:Secret"] ?? "school-api-device-development-secret";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{secret}:{userId}:{deviceKey}")));
    }

    private async Task<bool> IsTrustedLoginDeviceAsync(ApplicationUser user, string deviceHash)
    {
        var trustedDevice = await _userManager.GetAuthenticationTokenAsync(user, TrustedDeviceLoginProvider, deviceHash);
        return !string.IsNullOrWhiteSpace(trustedDevice);
    }

    private async Task RememberTrustedLoginDeviceAsync(ApplicationUser user, string deviceHash, string deviceName)
    {
        var value = $"{DateTime.UtcNow:O}|{deviceName}";
        await _userManager.SetAuthenticationTokenAsync(user, TrustedDeviceLoginProvider, deviceHash, value);
    }

    private static void CleanExpiredLoginOtpChallenges()
    {
        var now = DateTime.UtcNow;
        foreach (var challenge in LoginOtpChallenges.Where(item => item.Value.ExpiresAtUtc < now).ToList())
        {
            LoginOtpChallenges.TryRemove(challenge.Key, out _);
        }
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeOtpDigits(string? otp)
    {
        if (string.IsNullOrWhiteSpace(otp))
        {
            return null;
        }

        var normalizedChars = otp
            .Trim()
            .Select(ch => ch switch
            {
                >= '\u0660' and <= '\u0669' => (char)('0' + (ch - '\u0660')),
                >= '\u06F0' and <= '\u06F9' => (char)('0' + (ch - '\u06F0')),
                _ => ch
            })
            .ToArray();

        return new string(normalizedChars);
    }

    private static bool IsOtpTestEmail(string email)
    {
        string[] testEmails =
        {
            "mozaghloul0123@gmail.com",
            "mohammedzaghloul0123@gmail.com",
            "mohammedzaghloul9000@gmail.com",
            "mohammedzaghloul8000@gmail.com",
            "mohammedzaghloul7000@gmail.com"
        };

        return testEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> SendOtpEmailAsync(string email, string otp, string purpose = "Ø§Ø³ØªØ¹Ø§Ø¯Ø© ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ±", string? deviceName = null)
    {
        var recipientName = NormalizeOptionalValue(deviceName) ?? email;
        var providerSent = await _emailService.SendOtpEmailAsync(email, recipientName, otp, purpose);
        if (providerSent)
        {
            return true;
        }

        var host = _configuration["Email:SmtpHost"] ?? _configuration["SMTP_HOST"];
        var portText = _configuration["Email:SmtpPort"] ?? _configuration["SMTP_PORT"];
        var username = _configuration["Email:Username"] ?? _configuration["SMTP_USERNAME"];
        var password = _configuration["Email:Password"] ?? _configuration["SMTP_PASSWORD"];
        var from = _configuration["Email:From"] ?? _configuration["SMTP_FROM"] ?? username;
        var enableSslText = _configuration["Email:EnableSsl"] ?? _configuration["SMTP_ENABLE_SSL"];
        var timeoutText = _configuration["Email:SmtpTimeoutMs"] ?? _configuration["SMTP_TIMEOUT_MS"];
        var enableSsl = bool.TryParse(enableSslText, out var ssl) ? ssl : true;
        var timeoutMs = int.TryParse(timeoutText, out var parsedTimeoutMs) ? parsedTimeoutMs : 15000;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            Console.WriteLine($"[OTP] {purpose} OTP for {email}: {otp}");
            return false;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(from, "NextGen Learning"),
            Subject = $"ÙƒÙˆØ¯ Ø§Ù„ØªØ­Ù‚Ù‚ - {purpose}",
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
            IsBodyHtml = true,
            Body = $"""
                <div style="font-family:Arial,sans-serif;direction:rtl;text-align:right;line-height:1.8">
                  <h2>ÙƒÙˆØ¯ Ø§Ù„ØªØ­Ù‚Ù‚ Ø§Ù„Ø®Ø§Øµ Ø¨Ùƒ</h2>
                  <p>Ø§Ø³ØªØ®Ø¯Ù… Ø§Ù„ÙƒÙˆØ¯ Ø§Ù„ØªØ§Ù„ÙŠ Ù„Ø¥ÙƒÙ…Ø§Ù„ Ø¹Ù…Ù„ÙŠØ©: {WebUtility.HtmlEncode(purpose)}. Ø§Ù„ÙƒÙˆØ¯ ØµØ§Ù„Ø­ Ù„Ù…Ø¯Ø© 10 Ø¯Ù‚Ø§Ø¦Ù‚ ÙÙ‚Ø·.</p>
                  {(string.IsNullOrWhiteSpace(deviceName) ? string.Empty : $"<p style=\"color:#475569\">Ø§Ù„Ø¬Ù‡Ø§Ø²: {WebUtility.HtmlEncode(deviceName)}</p>")}
                  <div style="font-size:28px;font-weight:800;letter-spacing:6px;background:#eff6ff;color:#1d4ed8;padding:16px 24px;border-radius:14px;display:inline-block">{otp}</div>
                  <p style="color:#64748b">Ø¥Ø°Ø§ Ù„Ù… ØªØ·Ù„Ø¨ Ù‡Ø°Ø§ Ø§Ù„ÙƒÙˆØ¯ØŒ ØªØ¬Ø§Ù‡Ù„ Ù‡Ø°Ù‡ Ø§Ù„Ø±Ø³Ø§Ù„Ø©.</p>
                </div>
                """
        };
        message.To.Add(email);

        using var client = new SmtpClient(host, int.TryParse(portText, out var port) ? port : 587)
        {
            EnableSsl = enableSsl,
            Timeout = timeoutMs
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        try
        {
            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OTP] Failed to send email to {email}: {ex.Message}. OTP: {otp}");
            return false;
        }
    }

    public class UpdateProfileRequest
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    }

    public class ForgotPasswordOtpRequest
    {
        public string? Email { get; set; }
    }

    public class VerifyForgotPasswordOtpRequest
    {
        public string? Email { get; set; }
        public string? Otp { get; set; }
        public string? NewPassword { get; set; }
    }

    public class VerifyLoginOtpRequest
    {
        public string? ChallengeId { get; set; }
        public string? Otp { get; set; }
        public string? DeviceKey { get; set; }
    }

    private class ForgotPasswordOtpState
    {
        public string OtpHash { get; set; } = string.Empty;
        public string ResetToken { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public int Attempts { get; set; }
    }

    private class LoginOtpChallengeState
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string DeviceKeyHash { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string OtpHash { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public int Attempts { get; set; }
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

