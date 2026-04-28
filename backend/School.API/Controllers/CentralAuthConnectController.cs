using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace School.API.Controllers;

[ApiController]
[Authorize]
[Route("api/central-auth/connect")]
public sealed class CentralAuthConnectController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _stateProtector;

    public CentralAuthConnectController(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _stateProtector = dataProtectionProvider.CreateProtector("central-auth-connect-state-v1");
    }

    [HttpGet("start")]
    public ActionResult<CentralAuthConnectStartResponse> Start()
    {
        var config = ReadConfig();
        if (!config.IsConfigured)
        {
            return BadRequest(new { message = "Central Auth OAuth integration is not configured." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var externalEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(externalEmail))
        {
            return Unauthorized(new { message = "School account is missing required identity claims." });
        }

        var state = _stateProtector.Protect(JsonSerializer.Serialize(new CentralAuthConnectState(
            userId,
            externalEmail,
            DateTimeOffset.UtcNow.AddMinutes(10),
            Guid.NewGuid().ToString("N")), JsonOptions));

        var authorizationUrl = BuildAuthorizationUrl(config, state);
        return Ok(new CentralAuthConnectStartResponse(authorizationUrl));
    }

    [HttpPost("callback")]
    public async Task<ActionResult<CentralAuthConnectCompleteResponse>> Complete(
        CentralAuthConnectCallbackRequest request,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[OAuth] Callback received. Error: {request.Error}, Code: {(request.Code?.Length > 0 ? "***" : null)}, State: {(request.State?.Length > 0 ? "***" : null)}");
        
        if (!string.IsNullOrWhiteSpace(request.Error))
        {
            Console.WriteLine($"[OAuth] Error from Central Auth: {request.Error}");
            return BadRequest(new { message = "Central Auth account connection was cancelled." });
        }

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.State))
        {
            Console.WriteLine("[OAuth] Missing code or state");
            return BadRequest(new { message = "Connection callback is missing the authorization code or state." });
        }

        var config = ReadConfig();
        Console.WriteLine($"[OAuth] Config: Enabled={config.Enabled}, IsConfigured={config.IsConfigured}");
        if (!config.IsConfigured)
        {
            Console.WriteLine("[OAuth] Config not configured");
            return BadRequest(new { message = "Central Auth OAuth integration is not configured." });
        }

        var state = TryReadState(request.State);
        Console.WriteLine($"[OAuth] State: {state != null}, Expires={state?.ExpiresAtUtc}");
        if (state is null || state.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            Console.WriteLine("[OAuth] State invalid or expired");
            return BadRequest(new { message = "Connection request expired. Start the link flow again." });
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Console.WriteLine($"[OAuth] CurrentUserId: {currentUserId}, State.UserId: {state.UserId}");
        if (!string.Equals(currentUserId, state.UserId, StringComparison.Ordinal))
        {
            Console.WriteLine("[OAuth] User ID mismatch - Forbid");
            return Forbid();
        }

        var token = await ExchangeCodeAsync(config, request.Code, cancellationToken);
        Console.WriteLine($"[OAuth] Token exchange: {(token != null ? "Success" : "Failed")}");
        if (token is null)
        {
            return BadRequest(new { message = "Central Auth rejected the authorization code." });
        }

        var userInfo = await GetUserInfoAsync(config, token.AccessToken, cancellationToken);
        Console.WriteLine($"[OAuth] UserInfo: {(userInfo != null ? $"Email={userInfo.Email}" : "null")}");
        if (userInfo is null || string.IsNullOrWhiteSpace(userInfo.Email))
        {
            return BadRequest(new { message = "Central Auth did not return a valid platform user." });
        }

        var link = await CreateLinkAsync(config, token.AccessToken, state.ExternalEmail, cancellationToken);
        Console.WriteLine($"[OAuth] CreateLink: {(link != null ? "Success" : "Failed")}");
        if (link is null)
        {
            return BadRequest(new { message = "Could not create the account link." });
        }

        Console.WriteLine("[OAuth] Success!");
        return Ok(new CentralAuthConnectCompleteResponse(
            true,
            "تم ربط حساب المدرسة بحساب الدخول الموحد بنجاح.",
            state.ExternalEmail,
            userInfo.Email,
            userInfo.Name));
    }

    [HttpGet("status")]
    public async Task<ActionResult<CentralAuthLinkStatusResponse>> Status(CancellationToken cancellationToken)
    {
        var config = ReadConfig();
        if (!config.IsConfigured)
        {
            return Ok(new CentralAuthLinkStatusResponse(false, null, null, null, null));
        }

        var externalEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(externalEmail))
        {
            return Unauthorized(new { message = "School account is missing an email claim." });
        }

        var status = await GetLinkStatusAsync(config, externalEmail, cancellationToken);
        return status is null
            ? BadRequest(new { message = "تعذر قراءة حالة الربط مع منصة الدخول الموحد." })
            : Ok(status);
    }

    [HttpDelete("link")]
    public async Task<ActionResult<CentralAuthLinkStatusResponse>> Unlink(CancellationToken cancellationToken)
    {
        var config = ReadConfig();
        if (!config.IsConfigured)
        {
            return BadRequest(new { message = "Central Auth OAuth integration is not configured." });
        }

        var externalEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(externalEmail))
        {
            return Unauthorized(new { message = "School account is missing an email claim." });
        }

        var deleted = await DeleteLinkAsync(config, externalEmail, cancellationToken);
        if (!deleted)
        {
            return BadRequest(new { message = "تعذر إلغاء الربط مع منصة الدخول الموحد." });
        }

        return Ok(new CentralAuthLinkStatusResponse(false, null, externalEmail, null, null));
    }

    private CentralAuthConnectConfig ReadConfig()
    {
        var enabled = _configuration.GetValue<bool>("CentralAuth:Enabled");
        var baseUrl = _configuration["CentralAuth:BaseUrl"]?.TrimEnd('/');
        var clientId = _configuration["CentralAuth:ClientId"];
        var clientSecret = _configuration["CentralAuth:ClientSecret"];
        var apiKey = _configuration["CentralAuth:ForgotPasswordApiKey"];
        var redirectUri = _configuration["CentralAuth:ConnectRedirectUri"]
            ?? "http://localhost:4200/auth/connect/callback";

        return new CentralAuthConnectConfig(
            enabled,
            baseUrl ?? string.Empty,
            clientId ?? string.Empty,
            clientSecret ?? string.Empty,
            apiKey ?? string.Empty,
            redirectUri);
    }

    private string BuildAuthorizationUrl(CentralAuthConnectConfig config, string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = config.ClientId,
            ["redirect_uri"] = config.RedirectUri,
            ["scope"] = "openid profile email",
            ["state"] = state,
            ["nonce"] = Guid.NewGuid().ToString("N")
        };

        var queryString = string.Join("&", query.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value ?? string.Empty)}"));

        return $"{config.BaseUrl}/connect/authorize?{queryString}";
    }

    private CentralAuthConnectState? TryReadState(string protectedState)
    {
        try
        {
            var json = _stateProtector.Unprotect(protectedState);
            return JsonSerializer.Deserialize<CentralAuthConnectState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task<CentralOAuthTokenResponse?> ExchangeCodeAsync(
        CentralAuthConnectConfig config,
        string code,
        CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = config.RedirectUri,
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret
        });

        var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsync($"{config.BaseUrl}/connect/token", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<CentralOAuthTokenResponse>(JsonOptions, cancellationToken);
    }

    private async Task<CentralUserInfoResponse?> GetUserInfoAsync(
        CentralAuthConnectConfig config,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{config.BaseUrl}/connect/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<CentralUserInfoResponse>(JsonOptions, cancellationToken);
    }

    private async Task<CentralUserLinkResponse?> CreateLinkAsync(
        CentralAuthConnectConfig config,
        string accessToken,
        string externalEmail,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}/api/integrations/user-links")
        {
            Content = JsonContent.Create(new { externalEmail })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-API-Key", config.ApiKey);

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);
        
        // Log response for debugging
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        Console.WriteLine($"CreateLinkAsync Response: {response.StatusCode} - {responseContent}");
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return JsonSerializer.Deserialize<CentralUserLinkResponse>(responseContent, JsonOptions);
    }

    private async Task<CentralAuthLinkStatusResponse?> GetLinkStatusAsync(
        CentralAuthConnectConfig config,
        string externalEmail,
        CancellationToken cancellationToken)
    {
        var requestUri = $"{config.BaseUrl}/api/integrations/user-links/status?externalUserId={Uri.EscapeDataString(externalEmail)}";
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("X-API-Key", config.ApiKey);

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<CentralAuthLinkStatusResponse>(JsonOptions, cancellationToken);
    }

    private async Task<bool> DeleteLinkAsync(
        CentralAuthConnectConfig config,
        string externalEmail,
        CancellationToken cancellationToken)
    {
        var requestUri = $"{config.BaseUrl}/api/integrations/user-links?externalUserId={Uri.EscapeDataString(externalEmail)}";
        var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        request.Headers.Add("X-API-Key", config.ApiKey);

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public sealed record CentralAuthConnectStartResponse(string AuthorizationUrl);

    public sealed record CentralAuthConnectCallbackRequest(string? Code, string? State, string? Error);

    public sealed record CentralAuthConnectCompleteResponse(
        bool Success,
        string Message,
        string ExternalEmail,
        string PlatformEmail,
        string? PlatformDisplayName);

    public sealed record CentralAuthLinkStatusResponse(
        bool IsLinked,
        string? ExternalAppName,
        string? ExternalEmail,
        string? PlatformEmail,
        string? PlatformDisplayName);

    private sealed record CentralAuthConnectState(
        string UserId,
        string ExternalEmail,
        DateTimeOffset ExpiresAtUtc,
        string Nonce);

    private sealed record CentralAuthConnectConfig(
        bool Enabled,
        string BaseUrl,
        string ClientId,
        string ClientSecret,
        string ApiKey,
        string RedirectUri)
    {
        public bool IsConfigured =>
            Enabled &&
            !string.IsNullOrWhiteSpace(BaseUrl) &&
            !string.IsNullOrWhiteSpace(ClientId) &&
            !string.IsNullOrWhiteSpace(ClientSecret) &&
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(RedirectUri);
    }

    private sealed record CentralOAuthTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("id_token")] string IdToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] string Scope);

    private sealed record CentralUserInfoResponse(
        [property: JsonPropertyName("sub")] string Subject,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record CentralUserLinkResponse(Guid Id);
}
