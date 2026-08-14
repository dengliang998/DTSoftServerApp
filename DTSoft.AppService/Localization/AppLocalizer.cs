using System.Globalization;
using System.Reflection;
using System.Resources;

namespace DTSoft.AppService.Localization;

public class AppLocalizer : IAppLocalizer
{
    private static readonly ResourceManager ResourceManager = new(
        "DTSoft.AppService.Resources.DTResource",
        Assembly.GetExecutingAssembly());

    private readonly CultureInfo _fallbackCulture;

    public AppLocalizer(string fallbackCultureName = "en-US")
    {
        _fallbackCulture = GetCultureOrDefault(fallbackCultureName);
    }

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

        value = ResourceManager.GetString(key, _fallbackCulture);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private static CultureInfo GetCultureOrDefault(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return CultureInfo.GetCultureInfo("en-US");
        }

        try
        {
            return CultureInfo.GetCultureInfo(cultureName.Trim());
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("en-US");
        }
    }
}
