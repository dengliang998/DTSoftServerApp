using System.Globalization;
using DTSoft.AppService.Localization;
using DTSoft.Core.Common;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;

namespace DTSoftServerApp.Extensions;

public static class LocalizationConfigurationExtensions
{
    private const string LanguageHeaderName = "X-Language";
    private static readonly string[] BuiltInCultures = ["zh-CN", "en-US"];

    public static IServiceCollection AddDtSoftRequestLocalization(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = GetSupportedCultures(configuration);
            var defaultCulture = GetDefaultCulture(configuration, supportedCultures);

            options.DefaultRequestCulture = new RequestCulture(defaultCulture);
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;

            options.RequestCultureProviders.Insert(0, new CustomRequestCultureProvider(context =>
            {
                var headerLanguage = context.Request.Headers[LanguageHeaderName].ToString();
                var culture = FindSupportedCulture(headerLanguage, supportedCultures);

                return Task.FromResult(culture is null
                    ? null
                    : new ProviderCultureResult(culture.Name, culture.Name));
            }));
        });

        return services;
    }

    public static IApplicationBuilder UseDtSoftRequestLocalization(this IApplicationBuilder builder)
    {
        var options = builder.ApplicationServices
            .GetRequiredService<IOptions<RequestLocalizationOptions>>()
            .Value;

        return builder.UseRequestLocalization(options);
    }

    public static AppLocalizer CreateAppLocalizer(IConfiguration configuration)
    {
        var supportedCultures = GetSupportedCultures(configuration);
        var defaultCulture = GetDefaultCulture(configuration, supportedCultures);
        return new AppLocalizer(defaultCulture.Name);
    }

    private static List<CultureInfo> GetSupportedCultures(IConfiguration configuration)
    {
        var cultures = configuration
            .GetSection(AppConfigurationKeys.Localization.Languages)
            .GetChildren()
            .Select(section => section["Code"] ?? section["LanguageCode"])
            .Select(TryGetBuiltInCulture)
            .Where(culture => culture is not null)
            .Select(culture => culture!)
            .GroupBy(culture => culture.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (cultures.Count > 0)
        {
            return cultures;
        }

        return BuiltInCultures.Select(CultureInfo.GetCultureInfo).ToList();
    }

    private static CultureInfo GetDefaultCulture(IConfiguration configuration, IReadOnlyCollection<CultureInfo> supportedCultures)
    {
        var configured = FindSupportedCulture(configuration[AppConfigurationKeys.Localization.DefaultLanguage], supportedCultures);
        if (configured is not null) return configured;

        return FindSupportedCulture("zh-CN", supportedCultures)
               ?? supportedCultures.FirstOrDefault()
               ?? CultureInfo.GetCultureInfo("zh-CN");
    }

    private static CultureInfo? FindSupportedCulture(string? value, IEnumerable<CultureInfo> supportedCultures)
    {
        var culture = TryGetBuiltInCulture(value);
        if (culture is null) return null;

        return supportedCultures.FirstOrDefault(item =>
            item.Name.Equals(culture.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static CultureInfo? TryGetCulture(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        try
        {
            return CultureInfo.GetCultureInfo(value.Trim());
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private static CultureInfo? TryGetBuiltInCulture(string? value)
    {
        var culture = TryGetCulture(value);
        if (culture is null) return null;

        return BuiltInCultures.Any(item => item.Equals(culture.Name, StringComparison.OrdinalIgnoreCase))
            ? culture
            : null;
    }
}
