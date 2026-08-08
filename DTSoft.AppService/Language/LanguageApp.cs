using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.AppService.Localization;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Language;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DTSoft.AppService.Language;

public class LanguageApp(
    SysDbContext dbContext,
    IDtSoftCache dtSoftCache,
    DtSoftHelper dtSoftHelper,
    IAppLocalizer localizer,
    IConfiguration configuration)
{
    private const string LanguageListCacheKey = "Language:List";
    private const string LanguageResourceCacheKeyPrefix = "Language:Resources:";
    private static readonly HashSet<string> BuiltInLanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "zh-CN",
        "en-US"
    };

    private sealed class LanguageListItem
    {
        public long ItemId { get; init; }
        public string LanguageCode { get; init; } = string.Empty;
        public string LanguageName { get; init; } = string.Empty;
        public string NativeName { get; init; } = string.Empty;
        public bool IsEnabled { get; init; }
        public bool IsDefault { get; init; }
        public int Sort { get; init; }
    }

    private sealed class EnabledLanguageItem
    {
        public string LanguageCode { get; init; } = string.Empty;
        public string LanguageName { get; init; } = string.Empty;
        public string NativeName { get; init; } = string.Empty;
        public bool IsDefault { get; init; }
        public int Sort { get; init; }
    }

    private sealed class ConfigLanguageItem
    {
        public string Code { get; init; } = string.Empty;
        public string LanguageCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string LanguageName { get; init; } = string.Empty;
        public string NativeName { get; init; } = string.Empty;
        public int Sort { get; init; }
    }

    private sealed class LanguageResourceItem
    {
        public long ItemId { get; init; }
        public string ResourceKey { get; init; } = string.Empty;
        public string Module { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public Dictionary<string, string?> Values { get; init; } = new();
    }

    public async Task<JsonObject> GetLanguagesAsync()
    {
        await EnsureSeedDataAsync();

        var list = await dbContext.SysLanguage!
            .AsNoTracking()
            .OrderBy(p => p.Sort)
            .ThenBy(p => p.LanguageCode)
            .Select(p => new LanguageListItem
            {
                ItemId = p.ItemId,
                LanguageCode = p.LanguageCode,
                LanguageName = p.LanguageName,
                NativeName = p.NativeName,
                IsEnabled = p.IsEnabled,
                IsDefault = p.IsDefault,
                Sort = p.Sort
            })
            .ToListAsync();

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = JsonSerializer.SerializeToNode(list)
        };
    }

    public Task<JsonObject> GetEnabledLanguagesAsync()
    {
        var list = GetConfiguredEnabledLanguages();

        return Task.FromResult(new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = JsonSerializer.SerializeToNode(list)
        });
    }

    public async Task<JsonObject> SaveLanguageAsync(LanguageSaveParameter parameter, string loginUserAcc)
    {
        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            return Fail(localizer["permission.noModify"]);
        }

        var languageCode = NormalizeLanguageCode(parameter.LanguageCode);
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return Fail(localizer["language.codeRequired"]);
        }

        if (!BuiltInLanguageCodes.Contains(languageCode))
        {
            return Fail(localizer["language.unsupported"]);
        }

        var languageName = parameter.LanguageName?.Trim();
        var nativeName = parameter.NativeName?.Trim();
        if (string.IsNullOrWhiteSpace(languageName) || string.IsNullOrWhiteSpace(nativeName))
        {
            return Fail(localizer["language.nameRequired"]);
        }

        SysLanguage? language = null;
        if (parameter.ItemId is > 0)
        {
            language = await dbContext.SysLanguage!.FirstOrDefaultAsync(p => p.ItemId == parameter.ItemId.Value);
        }

        var duplicated = await dbContext.SysLanguage!
            .AnyAsync(p => p.LanguageCode == languageCode && (language == null || p.ItemId != language.ItemId));
        if (duplicated)
        {
            return Fail(localizer["resource.conflict"]);
        }

        language ??= await dbContext.SysLanguage!.FirstOrDefaultAsync(p => p.LanguageCode == languageCode);
        if (language is null)
        {
            language = new SysLanguage
            {
                ItemId = YitterHelper.NewId(),
                LanguageCode = languageCode
            };
            dbContext.SysLanguage!.Add(language);
        }

        language.LanguageName = languageName;
        language.NativeName = nativeName;
        language.IsEnabled = parameter.IsDefault || parameter.IsEnabled;
        language.IsDefault = parameter.IsDefault;
        language.Sort = parameter.Sort;

        if (language.IsDefault)
        {
            var otherDefaultLanguages = await dbContext.SysLanguage!
                .Where(p => p.ItemId != language.ItemId)
                .ToListAsync();
            foreach (var item in otherDefaultLanguages)
            {
                item.IsDefault = false;
            }
        }

        await dbContext.SaveChangesAsync();
        await EnsureOneDefaultLanguageAsync();
        dtSoftCache.RefreshCache(LanguageListCacheKey);

        return Success();
    }

    public async Task<JsonObject> DeleteLanguageAsync(long itemId, string loginUserAcc)
    {
        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            return Fail(localizer["permission.noModify"]);
        }

        var language = await dbContext.SysLanguage!.FirstOrDefaultAsync(p => p.ItemId == itemId);
        if (language is null)
        {
            return Fail(localizer["language.notFound"]);
        }

        if (language.IsDefault)
        {
            return Fail(localizer["language.defaultCannotDelete"]);
        }

        dbContext.SysLanguage!.Remove(language);
        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache(LanguageListCacheKey);

        return Success();
    }

    public async Task<JsonObject> GetLanguageResourcesAsync()
    {
        var rows = await dbContext.SysLanguageResource!
            .AsNoTracking()
            .OrderBy(p => p.Module)
            .ThenBy(p => p.ResourceKey)
            .ToListAsync();

        var list = rows.Select(p => new LanguageResourceItem
        {
            ItemId = p.ItemId,
            ResourceKey = p.ResourceKey,
            Module = p.Module ?? string.Empty,
            Description = p.Description ?? string.Empty,
            Values = ParseValues(p.ValuesJson)
        }).ToList();

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = JsonSerializer.SerializeToNode(list)
        };
    }

    public async Task<JsonObject> GetLanguageResourceValuesAsync(string? languageCode)
    {
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
        if (string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            normalizedLanguageCode = GetDefaultLanguageCode();
        }

        var values = await GetResourceValuesAsync(normalizedLanguageCode);
        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = JsonSerializer.SerializeToNode(values)
        };
    }

    public async Task<Dictionary<string, string>> GetResourceValuesAsync(string languageCode)
    {
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
        if (string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            normalizedLanguageCode = GetDefaultLanguageCode();
        }

        var cacheKey = $"{LanguageResourceCacheKeyPrefix}{normalizedLanguageCode}";
        return await dtSoftCache.GetOrCreateAsync(cacheKey, TimeSpan.FromMinutes(5), () =>
            dbContext.SysLanguageResource!
                .AsNoTracking()
                .Select(p => new { p.ResourceKey, p.ValuesJson })
                .ToList()
                .Select(p => new
                {
                    p.ResourceKey,
                    Value = ParseValues(p.ValuesJson).GetValueOrDefault(normalizedLanguageCode)
                })
                .Where(p => !string.IsNullOrWhiteSpace(p.Value))
                .ToDictionary(p => p.ResourceKey, p => p.Value!, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<JsonObject> SaveLanguageResourceAsync(LanguageResourceSaveParameter parameter, string loginUserAcc)
    {
        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            return Fail(localizer["permission.noModify"]);
        }

        var resourceKey = parameter.ResourceKey?.Trim();
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return Fail(localizer["language.resourceKeyRequired"]);
        }

        SysLanguageResource? resource = null;
        if (parameter.ItemId is > 0)
        {
            resource = await dbContext.SysLanguageResource!.FirstOrDefaultAsync(p => p.ItemId == parameter.ItemId.Value);
        }

        var duplicated = await dbContext.SysLanguageResource!
            .AnyAsync(p => p.ResourceKey == resourceKey && (resource == null || p.ItemId != resource.ItemId));
        if (duplicated)
        {
            return Fail(localizer["resource.conflict"]);
        }

        resource ??= new SysLanguageResource
        {
            ItemId = YitterHelper.NewId()
        };

        if (dbContext.Entry(resource).State == EntityState.Detached)
        {
            dbContext.SysLanguageResource!.Add(resource);
        }

        resource.ResourceKey = resourceKey;
        resource.Module = parameter.Module?.Trim();
        resource.Description = parameter.Description?.Trim();
        resource.ValuesJson = JsonSerializer.Serialize(NormalizeValues(parameter.Values));

        await dbContext.SaveChangesAsync();
        RefreshResourceCaches();
        return Success();
    }

    public async Task<JsonObject> DeleteLanguageResourceAsync(long itemId, string loginUserAcc)
    {
        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            return Fail(localizer["permission.noModify"]);
        }

        var resource = await dbContext.SysLanguageResource!.FirstOrDefaultAsync(p => p.ItemId == itemId);
        if (resource is null)
        {
            return Fail(localizer["language.resourceNotFound"]);
        }

        dbContext.SysLanguageResource!.Remove(resource);
        await dbContext.SaveChangesAsync();
        RefreshResourceCaches();
        return Success();
    }

    public async Task EnsureSeedDataAsync()
    {
        if (dbContext.SysLanguage is null) return;
        if (await dbContext.SysLanguage.AnyAsync()) return;

        dbContext.SysLanguage.AddRange(
            new SysLanguage
            {
                ItemId = YitterHelper.NewId(),
                LanguageCode = "zh-CN",
                LanguageName = "简体中文",
                NativeName = "简体中文",
                IsEnabled = true,
                IsDefault = true,
                Sort = 10
            },
            new SysLanguage
            {
                ItemId = YitterHelper.NewId(),
                LanguageCode = "en-US",
                LanguageName = "English",
                NativeName = "English",
                IsEnabled = true,
                IsDefault = false,
                Sort = 20
            });

        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache(LanguageListCacheKey);
    }

    private async Task EnsureOneDefaultLanguageAsync()
    {
        if (await dbContext.SysLanguage!.AnyAsync(p => p.IsDefault && p.IsEnabled)) return;

        var fallback = await dbContext.SysLanguage!
            .OrderBy(p => p.Sort)
            .ThenBy(p => p.LanguageCode)
            .FirstOrDefaultAsync(p => p.IsEnabled);
        if (fallback is null) return;

        fallback.IsDefault = true;
        await dbContext.SaveChangesAsync();
    }

    private static string NormalizeLanguageCode(string? value)
    {
        var code = value?.Trim();
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return BuiltInLanguageCodes.FirstOrDefault(p => p.Equals(code, StringComparison.OrdinalIgnoreCase)) ?? code;
    }

    private List<EnabledLanguageItem> GetConfiguredEnabledLanguages()
    {
        var defaultLanguage = GetDefaultLanguageCode();

        var configured = configuration
            .GetSection(AppConfigurationKeys.Localization.Languages)
            .Get<List<ConfigLanguageItem>>() ?? new List<ConfigLanguageItem>();

        var list = configured
            .Select((item, index) =>
            {
                var code = NormalizeLanguageCode(item.Code);
                if (string.IsNullOrWhiteSpace(code))
                {
                    code = NormalizeLanguageCode(item.LanguageCode);
                }

                return new
                {
                    Code = code,
                    Name = item.Name,
                    LanguageName = item.LanguageName,
                    NativeName = item.NativeName,
                    Sort = item.Sort > 0 ? item.Sort : (index + 1) * 10
                };
            })
            .Where(item => BuiltInLanguageCodes.Contains(item.Code))
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(item => new EnabledLanguageItem
            {
                LanguageCode = item.Code,
                LanguageName = string.IsNullOrWhiteSpace(item.Name) && string.IsNullOrWhiteSpace(item.LanguageName)
                    ? GetBuiltInLanguageName(item.Code)
                    : (string.IsNullOrWhiteSpace(item.Name) ? item.LanguageName : item.Name).Trim(),
                NativeName = string.IsNullOrWhiteSpace(item.NativeName)
                    ? GetBuiltInNativeName(item.Code)
                    : item.NativeName.Trim(),
                IsDefault = item.Code.Equals(defaultLanguage, StringComparison.OrdinalIgnoreCase),
                Sort = item.Sort
            })
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Sort)
            .ThenBy(item => item.LanguageCode)
            .ToList();

        return list;
    }

    private string GetDefaultLanguageCode()
    {
        var defaultLanguage = NormalizeLanguageCode(configuration[AppConfigurationKeys.Localization.DefaultLanguage]);
        return string.IsNullOrWhiteSpace(defaultLanguage) || !BuiltInLanguageCodes.Contains(defaultLanguage)
            ? "zh-CN"
            : defaultLanguage;
    }

    private static string GetBuiltInLanguageName(string languageCode) =>
        languageCode.Equals("en-US", StringComparison.OrdinalIgnoreCase) ? "English" : "简体中文";

    private static string GetBuiltInNativeName(string languageCode) =>
        languageCode.Equals("en-US", StringComparison.OrdinalIgnoreCase) ? "English" : "简体中文";

    private static Dictionary<string, string?> ParseValues(string? valuesJson)
    {
        if (string.IsNullOrWhiteSpace(valuesJson)) return new Dictionary<string, string?>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(valuesJson) ?? new Dictionary<string, string?>();
        }
        catch
        {
            return new Dictionary<string, string?>();
        }
    }

    private static Dictionary<string, string?> NormalizeValues(Dictionary<string, string?> values)
    {
        return values
            .Where(p => BuiltInLanguageCodes.Contains(p.Key))
            .ToDictionary(p => NormalizeLanguageCode(p.Key), p => string.IsNullOrWhiteSpace(p.Value) ? null : p.Value.Trim());
    }

    private void RefreshResourceCaches()
    {
        foreach (var languageCode in BuiltInLanguageCodes)
        {
            dtSoftCache.RefreshCache($"{LanguageResourceCacheKeyPrefix}{languageCode}");
        }
    }

    private static JsonObject Success() => new()
    {
        ["success"] = true,
        ["StateCode"] = 0
    };

    private static JsonObject Fail(string message) => new()
    {
        ["success"] = false,
        ["StateCode"] = 400,
        ["Msg"] = message
    };
}
