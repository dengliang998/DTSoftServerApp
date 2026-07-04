using DTSoft.Core.Common;
using DTSoft.Core.Interfaces;
using Microsoft.Extensions.Logging;
using ServiceStack.Redis;
using IRedisClient = ServiceStack.Redis.IRedisClient;

namespace DTSoft.Core.Cache;

public class RedisCache(ILogger<RedisCache> logger, ConfigHelper configHelper) : IDtSoftCache
{
    private readonly string _host = configHelper.GetSectionValue(AppConfigurationKeys.Cache.Redis.Host)
        ?? configHelper.GetSectionValue(AppConfigurationKeys.Cache.Redis.LegacyHost)
        ?? "localhost";
    private readonly int _port = Convert.ToInt32(
        configHelper.GetSectionValue(AppConfigurationKeys.Cache.Redis.Port)
            ?? configHelper.GetSectionValue(AppConfigurationKeys.Cache.Redis.LegacyPort)
            ?? "6379");
    private readonly string _password = configHelper.GetSectionValue(AppConfigurationKeys.Cache.Redis.Password)
        ?? configHelper.GetSectionValue(AppConfigurationKeys.Cache.Redis.LegacyPassword)
        ?? string.Empty;

    Task<T> IDtSoftCache.GetOrCreateAsync<T>(string key,TimeSpan ts, Func<T> func)
    {
        try
        {
            using IRedisClient redis = new RedisClient(_host, _port, _password);
            var existing = redis.Get<T>(key);
            if (existing is null)
            {
                var value = func();
                redis.Set(key, value, ts);
                return Task.FromResult(value);
            }

            return Task.FromResult(existing!);
        }
        catch (Exception ex)
        {
            LogError(ex);
            return Task.FromResult(func());
        }
    }

    void IDtSoftCache.RefreshCache(string key)
    {
        try
        {
            using IRedisClient redis = new RedisClient(_host, _port, _password);
            redis.Remove(key);
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }

    Task<T?> IDtSoftCache.GetAsync<T>(string key) where T : class
    {
        try
        {
            using IRedisClient redis = new RedisClient(_host, _port, _password);
            return Task.FromResult<T?>(redis.Get<T>(key));
        }
        catch (Exception ex)
        {
            LogError(ex);
            return Task.FromResult<T?>(default);
        }
    }

    Task IDtSoftCache.SetAsync<T>(string key, T value, TimeSpan? expiration)
    {
        try
        {
            using IRedisClient redis = new RedisClient(_host, _port, _password);
            if (expiration.HasValue)
                redis.Set(key, value, expiration.Value);
            else
                redis.Set(key, value);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogError(ex);
            return Task.CompletedTask;
        }
    }

    Task IDtSoftCache.RemoveAsync(string key)
    {
        try
        {
            using IRedisClient redis = new RedisClient(_host, _port, _password);
            redis.Remove(key);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogError(ex);
            return Task.CompletedTask;
        }
    }

    Task<Dictionary<string, T>> IDtSoftCache.GetMultipleAsync<T>(IEnumerable<string> keys) where T : class
    {
        try
        {
            using IRedisClient redis = new RedisClient(_host, _port, _password);
            var result = new Dictionary<string, T>();
            foreach (var key in keys)
            {
                var value = redis.Get<T>(key);
                if (value is not null)
                    result[key] = value;
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            LogError(ex);
            return Task.FromResult(new Dictionary<string, T>());
        }
    }

    Task IDtSoftCache.SetMultipleAsync<T>(Dictionary<string, T> values, TimeSpan? expiration) where T : class
    {
        try
        {
            using IRedisClient redis = new RedisClient(_host, _port, _password);
            foreach (var kvp in values)
            {
                if (expiration.HasValue)
                    redis.Set(kvp.Key, kvp.Value, expiration.Value);
                else
                    redis.Set(kvp.Key, kvp.Value);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogError(ex);
            return Task.CompletedTask;
        }
    }

    Task<bool> IDtSoftCache.ExistsAsync(string key)
    {
        try
        {
            using IRedisClient redis = new RedisClient(_host, _port, _password);
            return Task.FromResult(redis.ContainsKey(key));
        }
        catch (Exception ex)
        {
            LogError(ex);
            return Task.FromResult(false);
        }
    }

    Task IDtSoftCache.ExpireAsync(string key, TimeSpan expiration)
    {
        try
        {
            using IRedisClient redis = new RedisClient(_host, _port, _password);
            redis.ExpireEntryAt(key, DateTime.UtcNow + expiration);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogError(ex);
            return Task.CompletedTask;
        }
    }

    private void LogError(Exception ex) => logger.LogError(new EventId(8099), $"Redis错误：异常：{ex.Message}");
}
