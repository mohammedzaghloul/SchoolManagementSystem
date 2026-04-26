using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using School.Application.Interfaces;

namespace School.Infrastructure.Services;

public class EmailService : IEmailService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private sealed class ResendSendResponse
    {
        public string? Id { get; set; }
    }

    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, HttpClient httpClient, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SendOtpEmailAsync(
        string toEmail,
        string recipientName,
        string otpCode,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        if (await SendViaResendAsync(toEmail, otpCode, purpose, cancellationToken))
        {
            return true;
        }

        var host = GetSetting("Email:SmtpHost", "SMTP_HOST");
        var port = int.TryParse(GetSetting("Email:SmtpPort", "SMTP_PORT"), out var parsedPort) ? parsedPort : 587;
        var username = GetSetting("Email:Username", "SMTP_USERNAME", "SMTP_USER");
        var password = GetSetting("Email:Password", "SMTP_PASSWORD", "SMTP_PASS");
        var from = GetSetting("Email:From", "SMTP_FROM") ?? username;
        var enableSsl = bool.TryParse(GetSetting("Email:EnableSsl", "SMTP_ENABLE_SSL"), out var parsedSsl) && parsedSsl;
        var timeoutMs = int.TryParse(GetSetting("Email:SmtpTimeoutMs", "SMTP_TIMEOUT_MS"), out var parsedTimeout)
            ? parsedTimeout
            : 15000;

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(from))
        {
            _logger.LogWarning("SMTP settings are incomplete. OTP email was not sent to {Email}.", toEmail);
            return false;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(from, "School Platform"),
                Subject = GetOtpSubject(purpose),
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = false,
                Body = BuildTextBody(otpCode, purpose)
            };

            message.To.Add(toEmail);

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = timeoutMs
            };

            await client.SendMailAsync(message).WaitAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email to {Email}.", toEmail);
            return false;
        }
    }

    private async Task<bool> SendViaResendAsync(
        string toEmail,
        string otpCode,
        string purpose,
        CancellationToken cancellationToken)
    {
        var apiKey = GetSetting("Resend:ApiKey", "RESEND_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        var from = GetSetting("Resend:From", "RESEND_FROM") ?? "School Platform Support <auth@mail.mohammedzaghloul.publicvm.com>";

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            from,
            to = new[] { toEmail },
            subject = GetOtpSubject(purpose),
            text = BuildTextBody(otpCode, purpose)
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, SerializerOptions),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var resendResponse = JsonSerializer.Deserialize<ResendSendResponse>(responseBody, SerializerOptions);
                _logger.LogInformation(
                    "Resend accepted/queued OTP email for {Email}. MessageId: {MessageId}. Final delivery may still bounce later.",
                    toEmail,
                    resendResponse?.Id ?? "unknown");
                return true;
            }

            _logger.LogWarning(
                "Resend email API returned {StatusCode} for {Email}. Response: {ResponseBody}",
                (int)response.StatusCode,
                toEmail,
                responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email through Resend to {Email}.", toEmail);
            return false;
        }
    }

    private string? GetSetting(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string GetOtpSubject(string purpose)
    {
        return purpose.ToLowerInvariant() switch
        {
            "password reset" => "Password reset verification code",
            "login verification" => "Login verification code",
            _ => "Verification code"
        };
    }

    private static string BuildTextBody(string otpCode, string purpose)
    {
        return $"Your verification code for {GetPurposeLabel(purpose)} is: {otpCode}\nThis code expires in 10 minutes.\nIf you did not request this code, you can ignore this email.";
    }

    private static string GetPurposeLabel(string purpose)
    {
        return purpose.ToLowerInvariant() switch
        {
            "password reset" => "password reset",
            "login verification" => "login",
            _ => "verification"
        };
    }
}
