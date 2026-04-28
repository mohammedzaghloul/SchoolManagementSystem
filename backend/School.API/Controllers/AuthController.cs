using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using School.Application.DTOs.Auth;
using School.Application.Interfaces;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace School.API.Controllers;

[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("strict")]
public class AuthController : BaseApiController
{
    private readonly IEmailOtpService _emailOtpService;
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthController(
        IEmailOtpService emailOtpService,
        IAuthService authService,
        ITokenService tokenService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _emailOtpService = emailOtpService;
        _authService = authService;
        _tokenService = tokenService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var token = await _authService.LoginAsync(request.Email, request.Password);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { message = "البريد الإلكتروني أو كلمة المرور غير صحيحة." });
        }

        return Ok(new { token });
    }

    [HttpPost("request-login-otp")]
    [ProducesResponseType(typeof(OtpChallengeResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OtpChallengeResponseDto>> RequestLoginOtp(
        [FromBody] RequestLoginOtpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _emailOtpService.RequestLoginOtpAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("verify-login-otp")]
    [ProducesResponseType(typeof(AuthTokenResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthTokenResponseDto>> VerifyLoginOtp(
        [FromBody] VerifyLoginOtpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _emailOtpService.VerifyLoginOtpAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(OtpChallengeResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OtpChallengeResponseDto>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var centralResult = await TryForwardForgotPasswordToCentralAuthAsync(request, cancellationToken);
            if (centralResult is not null)
            {
                if (centralResult.Response is not null)
                {
                    return Ok(centralResult.Response);
                }

                return StatusCode(centralResult.StatusCode, new { message = centralResult.Message });
            }

            var response = await _emailOtpService.RequestPasswordResetOtpAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<CentralForgotPasswordForwardResult?> TryForwardForgotPasswordToCentralAuthAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue<bool>("CentralAuth:Enabled"))
        {
            return null;
        }

        var baseUrl = _configuration["CentralAuth:BaseUrl"]?.TrimEnd('/');
        var apiKey = _configuration["CentralAuth:ForgotPasswordApiKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/auth/forgot-password")
            {
                Content = JsonContent.Create(new
                {
                    externalUserId = request.Email,
                    resetUrlBase = _configuration["CentralAuth:ResetUrlBase"] ?? "http://localhost:4200/auth/reset-password"
                })
            };
            httpRequest.Headers.Add("X-API-Key", apiKey);

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await ReadCentralErrorMessageAsync(response, cancellationToken)
                    ?? "تعذر إرسال طلب إعادة تعيين كلمة المرور عبر منصة الدخول الموحدة.";

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    message = "لا يوجد ربط بين هذا البريد وتطبيق المدرسة داخل منصة الدخول الموحدة.";
                }
                else if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    message = "تم إرسال طلب استرجاع قريبًا. انتظر دقيقة ثم حاول مرة أخرى.";
                }

                return CentralForgotPasswordForwardResult.Rejected((int)response.StatusCode, message);
            }

            var centralResponse = await ReadCentralForgotPasswordResponseAsync(response.Content, cancellationToken);
            return CentralForgotPasswordForwardResult.Accepted(new OtpChallengeResponseDto
            {
                Success = centralResponse?.Accepted ?? true,
                EmailSent = false,
                EmailAccepted = true,
                EmailDeliveryStatus = "accepted_by_central_auth",
                Message = centralResponse?.Message ?? "تم إرسال رابط إعادة تعيين كلمة المرور عبر منصة الدخول الموحدة.",
                ExpiresAtUtc = centralResponse?.ExpiresAt?.UtcDateTime ?? DateTime.UtcNow.AddMinutes(10)
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return CentralForgotPasswordForwardResult.Rejected(
                StatusCodes.Status503ServiceUnavailable,
                "منصة الدخول الموحدة غير متاحة حاليًا، حاول مرة أخرى بعد قليل.");
        }
    }

    private static async Task<CentralForgotPasswordResponse?> ReadCentralForgotPasswordResponseAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        try
        {
            return await content.ReadFromJsonAsync<CentralForgotPasswordResponse>(cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static async Task<string?> ReadCentralErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var centralError = await response.Content.ReadFromJsonAsync<CentralErrorResponse>(cancellationToken);
            return centralError?.Error?.Message;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    [HttpPost("forgot-password/validate-token")]
    public async Task<ActionResult<CentralValidateResetTokenResponse>> ValidateCentralResetToken(
        [FromBody] CentralValidateResetTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ForwardCentralAuthAsync<CentralValidateResetTokenRequest, CentralValidateResetTokenResponse>(
            "/api/auth/forgot-password/validate",
            request,
            cancellationToken);

        return response is null
            ? BadRequest(new { message = "رابط إعادة تعيين كلمة المرور غير صالح أو انتهت صلاحيته." })
            : Ok(response);
    }

    [HttpPost("forgot-password/reset-token")]
    public async Task<ActionResult> ResetCentralPassword(
        [FromBody] CentralResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ForwardCentralAuthAsync<CentralResetPasswordRequest, CentralMessageResponse>(
            "/api/auth/forgot-password/reset",
            request,
            cancellationToken);

        return response is null
            ? BadRequest(new { message = "تعذر تحديث كلمة المرور من رابط إعادة التعيين." })
            : Ok(response);
    }

    private async Task<TResponse?> ForwardCentralAuthAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue<bool>("CentralAuth:Enabled"))
        {
            return default;
        }

        var baseUrl = _configuration["CentralAuth:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return default;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.PostAsJsonAsync($"{baseUrl}{path}", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return default;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return default;
        }
    }

    [HttpPost("verify-otp")]
    public async Task<ActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken cancellationToken)
    {
        var isValid = await _emailOtpService.VerifyPasswordResetOtpOnlyAsync(request.Email, request.Otp, cancellationToken);
        if (!isValid)
        {
            return BadRequest(new { message = "كود التحقق غير صحيح أو منتهي الصلاحية." });
        }
        return Ok(new { success = true });
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(PasswordResetResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PasswordResetResponseDto>> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _emailOtpService.ResetPasswordAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private sealed record CentralForgotPasswordResponse(
        bool Accepted,
        string Message,
        DateTimeOffset? ExpiresAt);

    private sealed record CentralForgotPasswordForwardResult(
        OtpChallengeResponseDto? Response,
        int StatusCode,
        string Message)
    {
        public static CentralForgotPasswordForwardResult Accepted(OtpChallengeResponseDto response) =>
            new(response, StatusCodes.Status200OK, response.Message);

        public static CentralForgotPasswordForwardResult Rejected(int statusCode, string message) =>
            new(null, statusCode, message);
    }

    private sealed record CentralErrorResponse(CentralErrorDetail? Error);

    private sealed record CentralErrorDetail(string? Code, string? Message);

    public sealed record CentralValidateResetTokenRequest(string Email, string Token);

    public sealed record CentralValidateResetTokenResponse(bool IsValid, DateTimeOffset? ExpiresAt);

    public sealed record CentralResetPasswordRequest(string Email, string Token, string NewPassword);

    public sealed record CentralMessageResponse(string Message);
}

