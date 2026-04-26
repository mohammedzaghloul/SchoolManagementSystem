using System.ComponentModel.DataAnnotations;

namespace School.Application.DTOs.Auth;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RequestLoginOtpRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;
}

public class VerifyLoginOtpRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits.")]
    public string Otp { get; set; } = string.Empty;
}

public class VerifyOtpRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits.")]
    public string Otp { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits.")]
    public string Otp { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class OtpChallengeResponseDto
{
    public bool Success { get; set; }
    // Backward-compatible name. This means the mail provider accepted/queued the send request,
    // not that the recipient inbox has definitely delivered it.
    public bool EmailSent { get; set; }
    public bool EmailAccepted { get; set; }
    public string EmailDeliveryStatus { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string? DevOtp { get; set; }
}

public class AuthTokenResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class PasswordResetResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
