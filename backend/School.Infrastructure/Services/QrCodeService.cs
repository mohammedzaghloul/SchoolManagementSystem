using School.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace School.Infrastructure.Services;

public class QrCodeService : IQrCodeService
{
    private readonly IConfiguration _config;
    private readonly string _secretKey;

    public QrCodeService(IConfiguration config)
    {
        _config = config;
        _secretKey = _config["Jwt:Key"] ?? "super_secret_secure_key_for_school_api_with_enough_length_to_be_valid";
    }

    public string GenerateQrToken(int sessionId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // Format: SessionId:Timestamp
        var payload = $"{sessionId}:{timestamp}";
        var signature = ComputeHmac(payload, _secretKey);
        
        // Token is base64 encoded string of payload|signature
        var tokenString = $"{payload}|{signature}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenString));
    }

    public bool ValidateQrToken(string token, int sessionId)
    {
        try
        {
            var decodedString = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = decodedString.Split('|');
            if (parts.Length != 2) return false;

            var payload = parts[0];
            var providedSignature = parts[1];

            // Recompute signature to verify
            var expectedSignature = ComputeHmac(payload, _secretKey);
            if (providedSignature != expectedSignature) return false;

            // Extract session id and timestamp
            var payloadParts = payload.Split(':');
            var tokenSessionId = int.Parse(payloadParts[0]);
            var timestamp = long.Parse(payloadParts[1]);

            if (tokenSessionId != sessionId) return false;

            // Token is only valid for 30 seconds
            var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (currentTimestamp - timestamp > 30) return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private string ComputeHmac(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }
}
