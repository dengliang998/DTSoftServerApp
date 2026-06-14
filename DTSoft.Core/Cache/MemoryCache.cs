using DTSoft.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DTSoft.Core.Cache;

public class MemoryCache(IMemoryCache memoryCache) : IDtSoftCache
{
    async Task<T> IDtSoftCache.GetOrCreateAsync<T>(string key, TimeSpan ts, Func<T> func) =>
        (await memoryCache.GetOrCreateAsync(key, (arg) =>
        {
            arg.SetSlidingExpiration(ts);
            return Task.FromResult(func());
        }))!;

    void IDtSoftCache.RefreshCache(string key) => memoryCache.Remove(key);

    Task<T?> IDtSoftCache.GetAsync<T>(string key) where T : class
    {
        if (memoryCache.TryGetValue(key, out T? value))
            return Task.FromResult<T?>(value);
        return Task.FromResult<T?>(null);
    }

    Task IDtSoftCache.SetAsync<T>(string key, T value, TimeSpan? expiration)
    {
        var options = new MemoryCacheEntryOptions();
        if (expiration.HasValue)
            options.SetSlidingExpiration(expiration.Value);
        
        memoryCache.Set(key, value, options);
        return Task.CompletedTask;
    }

    Task IDtSoftCache.RemoveAsync(string key)
    {
        memoryCache.Remove(key);
        return Task.CompletedTask;
    }

    Task<Dictionary<string, T>> IDtSoftCache.GetMultipleAsync<T>(IEnumerable<string> keys)
    {
        var result = new Dictionary<string, T>();
        foreach (var key in keys)
        {
            if (memoryCache.TryGetValue(key, out T? value) && value is not null)
                result[key] = value;
        }
        return Task.FromResult(result);
    }

    Task IDtSoftCache.SetMultipleAsync<T>(Dictionary<string, T> values, TimeSpan? expiration)
    {
        var options = new MemoryCacheEntryOptions();
        if (expiration.HasValue)
            options.SetSlidingExpiration(expiration.Value);
        
        foreach (var kvp in values)
            memoryCache.Set(kvp.Key, kvp.Value, options);
        
        return Task.CompletedTask;
    }

    Task<bool> IDtSoftCache.ExistsAsync(string key) =>
        Task.FromResult(memoryCache.TryGetValue(key, out _));

    Task IDtSoftCache.ExpireAsync(string key, TimeSpan expiration)
    {
        // MemoryCache 不支持单独修改过期时间，需要重新设置
        if (memoryCache.TryGetValue(key, out object? value))
        {
            memoryCache.Remove(key);
            var options = new MemoryCacheEntryOptions().SetSlidingExpiration(expiration);
            memoryCache.Set(key, value, options);
        }
        return Task.CompletedTask;
    }
}
