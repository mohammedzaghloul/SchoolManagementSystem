using Microsoft.Extensions.Configuration;
using School.Application.Interfaces;
using System;

namespace School.Infrastructure.Services;

public class LiveSessionService : ILiveSessionService
{
    private readonly IConfiguration _configuration;

    public LiveSessionService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(string channelName, string userAccount, uint expireTimeInSeconds)
    {
        // Integration with Agora Dynamic Key logic would go here.
        // For now, we return a mock token that includes the necessary info.
        // In production, use the Agora SDK for C# (DynamicKey/AccessToken)
        
        return $"mock_token_for_{channelName}_{userAccount}";
    }

    public string GenerateToken(string channelName, uint uid, uint expireTimeInSeconds)
    {
        // Integration with Agora Dynamic Key logic would go here.
        return $"mock_token_for_{channelName}_{uid}";
    }
}
