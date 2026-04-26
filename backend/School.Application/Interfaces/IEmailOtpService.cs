using School.Application.DTOs.Auth;

namespace School.Application.Interfaces;

public interface IEmailOtpService
{
    Task<OtpChallengeResponseDto> RequestLoginOtpAsync(RequestLoginOtpRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokenResponseDto> VerifyLoginOtpAsync(VerifyLoginOtpRequest request, CancellationToken cancellationToken = default);
    Task<OtpChallengeResponseDto> RequestPasswordResetOtpAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task<bool> VerifyPasswordResetOtpOnlyAsync(
        string email,
        string otpCode,
        CancellationToken cancellationToken = default);
    Task<PasswordResetResponseDto> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}
