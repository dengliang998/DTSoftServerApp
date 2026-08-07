using System.Globalization;
using System.Reflection;
using System.Resources;

namespace DTSoft.AppService.Localization;

public class AppLocalizer : IAppLocalizer
{
    private static readonly ResourceManager ResourceManager = new(
        "DTSoft.AppService.Resources.DTResource",
        Assembly.GetExecutingAssembly());

    private static readonly CultureInfo FallbackCulture = CultureInfo.GetCultureInfo("zh-CN");

    public string this[string key] => GetString(key);

    public string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentUICulture, GetString(key), args);
    }

    private string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var value = ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
        if (!string.IsNullOrWhiteSpace(value)) return value;

        value = ResourceManager.GetString(key, FallbackCulture);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }
}
