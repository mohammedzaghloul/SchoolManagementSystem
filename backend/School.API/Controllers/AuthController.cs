using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using School.Application.DTOs.Auth;
using School.Application.Interfaces;

namespace School.API.Controllers;

[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("strict")]
public class AuthController : BaseApiController
{
    private readonly IEmailOtpService _emailOtpService;
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;

    public AuthController(IEmailOtpService emailOtpService, IAuthService authService, ITokenService tokenService)
    {
        _emailOtpService = emailOtpService;
        _authService = authService;
        _tokenService = tokenService;
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
            var response = await _emailOtpService.RequestPasswordResetOtpAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
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
}

