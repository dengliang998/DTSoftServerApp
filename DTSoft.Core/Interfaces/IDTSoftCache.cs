namespace DTSoft.Core.Interfaces;

public interface IDtSoftCache
{
    /// <summary>
    /// 获取或者创建缓存数据
    /// </summary>
    /// <param name="key"></param>
    /// <param name="ts"></param>
    /// <param name="func"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task<T> GetOrCreateAsync<T>(string key, TimeSpan ts, Func<T> func);

    /// <summary>
    /// 刷新缓存
    /// </summary>
    /// <param name="key"></param>
    void RefreshCache(string key);

    /// <summary>
    /// 获取缓存（不创建）
    /// </summary>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// 设置缓存
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);

    /// <summary>
    /// 删除缓存
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// 批量获取缓存
    /// </summary>
    Task<Dictionary<string, T>> GetMultipleAsync<T>(IEnumerable<string> keys) where T : class;

    /// <summary>
    /// 批量设置缓存
    /// </summary>
    Task SetMultipleAsync<T>(Dictionary<string, T> values, TimeSpan? expiration = null) where T : class;

    /// <summary>
    /// 检查缓存是否存在
    /// </summary>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// 设置缓存过期时间
    /// </summary>
    Task ExpireAsync(string key, TimeSpan expiration);
}
