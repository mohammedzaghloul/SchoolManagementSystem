namespace School.Application.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Attempts to submit an OTP email to the configured provider.
    /// A true result means the provider accepted the request, not guaranteed inbox delivery.
    /// </summary>
    Task<bool> SendOtpEmailAsync(
        string toEmail,
        string recipientName,
        string otpCode,
        string purpose,
        CancellationToken cancellationToken = default);
}
