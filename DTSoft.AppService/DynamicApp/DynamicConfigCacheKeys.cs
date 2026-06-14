namespace DTSoft.AppService.DynamicApp;

public static class DynamicConfigCacheKeys
{
    public static string ActiveConfig(string modelName) =>
        $"DynamicConfig:Active:{modelName.Trim().ToLowerInvariant()}";
}

