using System.Globalization;
using DTSoft.Core.Common;

namespace DTSoftServerApp.Middleware;

public class RequestLanguageMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "zh-CN",
        "en-US"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var culture = ResolveCulture(context.Request);
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        try
        {
            await next(context);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private CultureInfo ResolveCulture(HttpRequest request)
    {
        var headerLanguage = request.Headers["X-Language"].ToString();
        var culture = NormalizeCulture(headerLanguage);
        if (culture is not null) return culture;

        return GetDefaultCulture();
    }

    private CultureInfo GetDefaultCulture()
    {
        return NormalizeCulture(configuration[AppConfigurationKeys.Localization.DefaultLanguage])
               ?? CultureInfo.GetCultureInfo("en-US");
    }

    private static CultureInfo? NormalizeCulture(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var code = SupportedCultures.FirstOrDefault(p => p.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        return code is null ? null : CultureInfo.GetCultureInfo(code);
    }
}

public static class RequestLanguageMiddlewareExtensions
{
    public static void UseRequestLanguage(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<RequestLanguageMiddleware>();
    }
}
