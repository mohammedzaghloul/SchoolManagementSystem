using System.Text.Json;
using School.Application.Interfaces;
using StackExchange.Redis;

namespace School.Infrastructure.Services;

public class CacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;

    public CacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _database = redis.GetDatabase();
    }

    private bool IsConnected => _redis.IsConnected;

    public async Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null)
    {
        if (!IsConnected)
        {
            return;
        }

        try 
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var serializedResponse = JsonSerializer.Serialize(value, options);

            if (expirationTime.HasValue)
            {
                await _database.StringSetAsync(key, serializedResponse, expirationTime.Value);
            }
            else
            {
                await _database.StringSetAsync(key, serializedResponse);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cache Error (Set): {ex.Message}");
        }
    }

    public async Task<T> GetAsync<T>(string key)
    {
        if (!IsConnected)
        {
            return default;
        }

        try
        {
            var cachedResponse = await _database.StringGetAsync(key);

            if (cachedResponse.IsNullOrEmpty)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(cachedResponse.ToString());
        }
        catch (Exception ex)
        {
            // Only log if it's not a connection-related timeout we already know about
            if (!(ex is RedisConnectionException) && !(ex is TimeoutException))
            {
                Console.WriteLine($"Cache Error (Get): {ex.Message}");
            }
            return default;
        }
    }

    public async Task RemoveAsync(string key)
    {
        if (!IsConnected) return;

        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cache Error (Remove): {ex.Message}");
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        if (!IsConnected) return false;

        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cache Error (Exists): {ex.Message}");
            return false;
        }
    }
}
