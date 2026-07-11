namespace DTSoft.AppService.MicroApp;

/// <summary>
/// 微应用配置缓存键。
/// </summary>
public static class MicroConfigCacheKeys
{
    /// <summary>
    /// 获取启用状态微应用配置的缓存键。
    /// </summary>
    /// <param name="modelName">模型名称。</param>
    /// <returns>缓存键。</returns>
    public static string ActiveConfig(string modelName) =>
        $"MicroConfig:Active:{modelName.Trim().ToLowerInvariant()}";
}
