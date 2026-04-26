using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using School.Application.DTOs.Auth;
using School.Application.Interfaces;
using School.Domain.Entities;
using School.Infrastructure.Data;
using School.Infrastructure.Identity;

namespace School.Infrastructure.Services;

public class EmailOtpService : IEmailOtpService
{
    private const string LoginPurpose = "Login";
    private const string PasswordResetPurpose = "PasswordReset";
    private const int OtpLength = 6;
    private const int OtpLifetimeMinutes = 10;
    private const int OtpCooldownMinutes = 2;
    private const int MaxFailedAttempts = 5;

    private readonly SchoolDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ITokenService _tokenService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<EmailOtpService> _logger;

    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

    public EmailOtpService(
        SchoolDbContext context,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ITokenService tokenService,
        UserManager<ApplicationUser> userManager,
        ILogger<EmailOtpService> logger,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _tokenService = tokenService;
        _userManager = userManager;
        _logger = logger;
        _env = env;
    }

    private bool ShouldExposeDevOtp(string email)
    {
        return string.Equals(_env.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private async Task CheckCooldownAsync(string email, string purpose, CancellationToken cancellationToken)
    {
        var latestOtp = await _context.EmailOtps
            .OrderByDescending(otp => otp.RequestedAtUtc)
            .FirstOrDefaultAsync(otp => otp.Email == email && otp.Purpose == purpose, cancellationToken);

        if (latestOtp != null && latestOtp.RequestedAtUtc > DateTime.UtcNow.AddMinutes(-2))
        {
            var remainingSeconds = (int)(latestOtp.RequestedAtUtc.AddMinutes(2) - DateTime.UtcNow).TotalSeconds;
            throw new InvalidOperationException($"الرجاء الانتظار {remainingSeconds} ثانية قبل طلب كود جديد.");
        }
    }

    private async Task<EmailOtp?> GetActiveCooldownOtpAsync(string email, string purpose, CancellationToken cancellationToken)
    {
        var latestOtp = await _context.EmailOtps
            .OrderByDescending(otp => otp.RequestedAtUtc)
            .FirstOrDefaultAsync(
                otp => otp.Email == email &&
                    otp.Purpose == purpose &&
                    !otp.IsUsed,
                cancellationToken);

        if (latestOtp != null && latestOtp.RequestedAtUtc > DateTime.UtcNow.AddMinutes(-OtpCooldownMinutes))
        {
            return latestOtp;
        }

        return null;
    }

    private static OtpChallengeResponseDto BuildOtpChallengeResponse(
        bool emailAccepted,
        DateTime expiresAtUtc,
        string message,
        string emailDeliveryStatus,
        string? devOtp = null)
    {
        return new OtpChallengeResponseDto
        {
            Success = true,
            EmailSent = emailAccepted,
            EmailAccepted = emailAccepted,
            EmailDeliveryStatus = emailDeliveryStatus,
            ExpiresAtUtc = expiresAtUtc,
            DevOtp = devOtp,
            Message = message
        };
    }

    public async Task<OtpChallengeResponseDto> RequestLoginOtpAsync(
        RequestLoginOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        
        // Check cooldown only after user lookup to avoid exposing whether an email exists.
        
        var user = await _userManager.FindByEmailAsync(email);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(OtpLifetimeMinutes);

        if (user == null)
        {
            // Standardize response to prevent account enumeration
            return new OtpChallengeResponseDto
            {
                Success = true,
                EmailSent = true, // Fake success
                EmailAccepted = true,
                EmailDeliveryStatus = "not_applicable",
                ExpiresAtUtc = expiresAtUtc,
                Message = "تم إرسال كود التحقق بنجاح."
            };
        }

        var cooldownOtp = await GetActiveCooldownOtpAsync(email, LoginPurpose, cancellationToken);
        if (cooldownOtp != null)
        {
            return BuildOtpChallengeResponse(
                emailAccepted: true,
                cooldownOtp.ExpiresAtUtc,
                "A recent verification code is still active. Please check your email before requesting a new code.",
                "cooldown");
        }

        var otpCode = GenerateOtp();
        var (hash, salt) = HashOtp(otpCode);

        var otp = new EmailOtp
        {
            Email = email,
            UserId = user.Id,
            Purpose = LoginPurpose,
            CodeHash = hash,
            Salt = salt,
            ExpiresAtUtc = expiresAtUtc
        };

        await _unitOfWork.Repository<EmailOtp>().AddAsync(otp);
        await _unitOfWork.CompleteAsync();

        var emailSent = await _emailService.SendOtpEmailAsync(
            email,
            user.FullName,
            otpCode,
            "login verification",
            cancellationToken);

        if (!emailSent)
        {
            otp.IsUsed = true;
            otp.UsedAtUtc = DateTime.UtcNow;
            await _unitOfWork.CompleteAsync();
        }

        return new OtpChallengeResponseDto
        {
            Success = true,
            EmailSent = emailSent,
            EmailAccepted = emailSent,
            EmailDeliveryStatus = emailSent ? "accepted" : "unavailable",
            ExpiresAtUtc = expiresAtUtc,
            DevOtp = null, // Never expose in production response
            Message = "تم إرسال كود التحقق بنجاح."
        };
    }

    public async Task<AuthTokenResponseDto> VerifyLoginOtpAsync(
        VerifyLoginOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(email)
            ?? throw new UnauthorizedAccessException("Invalid email or OTP.");

        var otp = await FindValidOtpAsync(email, LoginPurpose, request.Otp, cancellationToken);

        otp.IsUsed = true;
        otp.UsedAtUtc = DateTime.UtcNow;
        await InvalidateActiveOtpsAsync(email, LoginPurpose, cancellationToken);
        await _unitOfWork.CompleteAsync();

        var roles = await _userManager.GetRolesAsync(user);
        var role = ResolvePrimaryRole(roles);
        var token = _tokenService.CreateToken(user.Id, user.Email ?? email, role, user.FullName);

        return new AuthTokenResponseDto
        {
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            Email = user.Email ?? email,
            FullName = user.FullName,
            Role = role
        };
    }

    public async Task<OtpChallengeResponseDto> RequestPasswordResetOtpAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        
        // Check cooldown only after user lookup to avoid exposing whether an email exists.
        
        var user = await _userManager.FindByEmailAsync(email);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(OtpLifetimeMinutes);

        if (user == null)
        {
            // Standardize response to prevent account enumeration
            return new OtpChallengeResponseDto
            {
                Success = true,
                EmailSent = true, // Fake success
                EmailAccepted = true,
                EmailDeliveryStatus = "not_applicable",
                ExpiresAtUtc = expiresAtUtc,
                Message = "تم إرسال كود التحقق بنجاح."
            };
        }

        var cooldownOtp = await GetActiveCooldownOtpAsync(email, PasswordResetPurpose, cancellationToken);
        if (cooldownOtp != null)
        {
            return BuildOtpChallengeResponse(
                emailAccepted: true,
                cooldownOtp.ExpiresAtUtc,
                "A recent password reset code is still active. Please check your email before requesting a new code.",
                "cooldown");
        }

        var otpCode = GenerateOtp();
        var (hash, salt) = HashOtp(otpCode);

        var otp = new EmailOtp
        {
            Email = email,
            UserId = user.Id,
            Purpose = PasswordResetPurpose,
            CodeHash = hash,
            Salt = salt,
            ExpiresAtUtc = expiresAtUtc
        };

        await _unitOfWork.Repository<EmailOtp>().AddAsync(otp);
        await _unitOfWork.CompleteAsync();

        var emailSent = await _emailService.SendOtpEmailAsync(
            email,
            user.FullName,
            otpCode,
            "password reset",
            cancellationToken);

        if (!emailSent)
        {
            otp.IsUsed = true;
            otp.UsedAtUtc = DateTime.UtcNow;
            await _unitOfWork.CompleteAsync();
        }

        return new OtpChallengeResponseDto
        {
            Success = true,
            EmailSent = emailSent,
            EmailAccepted = emailSent,
            EmailDeliveryStatus = emailSent ? "accepted" : "unavailable",
            ExpiresAtUtc = expiresAtUtc,
            DevOtp = null,
            Message = "تم إرسال كود التحقق بنجاح."
        };
    }

    public async Task<bool> VerifyPasswordResetOtpOnlyAsync(
        string email,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        email = NormalizeEmail(email);
        try 
        {
            await FindValidOtpAsync(email, PasswordResetPurpose, otpCode, cancellationToken);
            return true;
        }
        catch 
        {
            return false;
        }
    }

    public async Task<PasswordResetResponseDto> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(email)
            ?? throw new UnauthorizedAccessException("Invalid email or OTP.");

        var otp = await FindValidOtpAsync(email, PasswordResetPurpose, request.Otp, cancellationToken);

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var identityResult = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);

        if (!identityResult.Succeeded)
        {
            var errors = string.Join(" ", identityResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException(errors);
        }

        otp.IsUsed = true;
        otp.UsedAtUtc = DateTime.UtcNow;
        await InvalidateActiveOtpsAsync(email, PasswordResetPurpose, cancellationToken);
        await _unitOfWork.CompleteAsync();

        return new PasswordResetResponseDto
        {
            Success = true,
            Message = "Password reset successfully."
        };
    }

    private async Task InvalidateActiveOtpsAsync(string email, string purpose, CancellationToken cancellationToken)
    {
        var activeOtps = await _context.EmailOtps
            .Where(otp => otp.Email == email)
            .Where(otp => otp.Purpose == purpose)
            .Where(otp => !otp.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var activeOtp in activeOtps)
        {
            activeOtp.IsUsed = true;
            activeOtp.UsedAtUtc = DateTime.UtcNow;
        }

        if (activeOtps.Count > 0)
        {
            await _unitOfWork.CompleteAsync();
        }
    }

    private async Task<EmailOtp?> GetLatestOtpAsync(string email, string purpose, CancellationToken cancellationToken)
    {
        return await _context.EmailOtps
            .OrderByDescending(otp => otp.RequestedAtUtc)
            .FirstOrDefaultAsync(otp =>
                otp.Email == email &&
                otp.Purpose == purpose &&
                !otp.IsUsed,
                cancellationToken);
    }

    private async Task<EmailOtp> FindValidOtpAsync(
        string email,
        string purpose,
        string submittedOtp,
        CancellationToken cancellationToken)
    {
        submittedOtp = NormalizeOtp(submittedOtp);

        var activeOtps = await _context.EmailOtps
            .Where(otp => otp.Email == email)
            .Where(otp => otp.Purpose == purpose)
            .Where(otp => !otp.IsUsed)
            .OrderByDescending(otp => otp.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        if (activeOtps.Count == 0)
        {
            throw new UnauthorizedAccessException("Invalid email or OTP.");
        }

        var now = DateTime.UtcNow;
        var changed = false;
        EmailOtp? newestValidOtp = null;

        foreach (var otp in activeOtps)
        {
            if (otp.ExpiresAtUtc < now)
            {
                otp.IsUsed = true;
                otp.UsedAtUtc = now;
                changed = true;
                continue;
            }

            newestValidOtp ??= otp;

            if (VerifyOtp(submittedOtp, otp.Salt, otp.CodeHash))
            {
                if (changed)
                {
                    await _unitOfWork.CompleteAsync();
                }

                return otp;
            }
        }

        if (newestValidOtp != null)
        {
            newestValidOtp.FailedAttempts++;
            if (newestValidOtp.FailedAttempts >= MaxFailedAttempts)
            {
                newestValidOtp.IsUsed = true;
                newestValidOtp.UsedAtUtc = now;
            }

            changed = true;
        }

        if (changed)
        {
            await _unitOfWork.CompleteAsync();
        }

        throw new UnauthorizedAccessException("Invalid email or OTP.");
    }

    private async Task EnsureOtpIsValidAsync(EmailOtp otp, string submittedOtp, CancellationToken cancellationToken)
    {
        submittedOtp = NormalizeOtp(submittedOtp);

        if (otp.ExpiresAtUtc < DateTime.UtcNow)
        {
            otp.IsUsed = true;
            otp.UsedAtUtc = DateTime.UtcNow;
            await _unitOfWork.CompleteAsync();
            throw new UnauthorizedAccessException("OTP has expired.");
        }

        if (!VerifyOtp(submittedOtp, otp.Salt, otp.CodeHash))
        {
            otp.FailedAttempts++;
            if (otp.FailedAttempts >= MaxFailedAttempts)
            {
                otp.IsUsed = true;
                otp.UsedAtUtc = DateTime.UtcNow;
            }

            await _unitOfWork.CompleteAsync();
            throw new UnauthorizedAccessException("Invalid email or OTP.");
        }
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string GenerateOtp()
    {
        return RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, OtpLength)).ToString($"D{OtpLength}");
    }

    private static (string Hash, string Salt) HashOtp(string otp)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = HashOtpBytes(otp, saltBytes);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    private static bool VerifyOtp(string otp, string salt, string expectedHash)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expectedHashBytes = Convert.FromBase64String(expectedHash);
        var computedHashBytes = HashOtpBytes(otp, saltBytes);
        return CryptographicOperations.FixedTimeEquals(expectedHashBytes, computedHashBytes);
    }

    private static string NormalizeOtp(string otp)
    {
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

    private static byte[] HashOtpBytes(string otp, byte[] saltBytes)
    {
        return Rfc2898DeriveBytes.Pbkdf2(otp, saltBytes, 10000, HashAlgorithmName.SHA256, 32);
    }

    private static string ResolvePrimaryRole(IEnumerable<string> roles)
    {
        var rolePriority = new[] { "Admin", "Teacher", "Student" };

        foreach (var role in rolePriority)
        {
            if (roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                return role;
            }
        }

        var firstRole = roles.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstRole))
        {
            throw new InvalidOperationException("User does not have an assigned role.");
        }

        return firstRole;
    }
}
