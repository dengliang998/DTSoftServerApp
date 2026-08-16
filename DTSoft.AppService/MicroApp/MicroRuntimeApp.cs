using System.Text.Json;
using System.Text.RegularExpressions;
using DTSoft.AppService.Localization;
using DTSoft.Core.Common.Excel;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.MicroApp;
using Microsoft.EntityFrameworkCore;

namespace DTSoft.AppService.MicroApp;

public class MicroRuntimeApp(
    SysDbContext context,
    MicroTableService microTableService,
    IDtSoftCache dtSoftCache,
    IAppLocalizer localizer)
{
    private const string RuntimeSubTablesKey = "__subTables";
    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private string L(string key, params object[] args) => args.Length == 0 ? localizer[key] : localizer.Format(key, args);

    public async Task<MicroRuntimeResult> GetList(
        string modelName,
        int pageNum,
        int pageSize,
        string keyword,
        string filters,
        string sortField,
        string sortOrder,
        string userAccount)
    {
        try
        {
            var resolution = await ResolveConfigAsync(modelName);
            if (resolution.Failure != null) return resolution.Failure;
            var config = resolution.Config!;

            await microTableService.EnsureTableExistsAsync(config);
            var result = await microTableService.ExecuteMicroQueryAsync(
                config,
                pageNum,
                pageSize,
                keyword,
                ParseQueryFilters(filters),
                sortField,
                sortOrder,
                userAccount);

            return Success("common.fetchSuccess", result);
        }
        catch (Exception ex)
        {
            return FailureWithException("micro.queryFailed", ex);
        }
    }

    public async Task<MicroRuntimeResult> GetDetail(string modelName, long id, string userAccount)
    {
        try
        {
            var resolution = await ResolveConfigAsync(modelName);
            if (resolution.Failure != null) return resolution.Failure;
            var config = resolution.Config!;

            await microTableService.EnsureTableExistsAsync(config);
            var result = await microTableService.ExecuteMicroDetailWithSubTablesAsync(config, id, userAccount);
            return Success("common.fetchSuccess", result);
        }
        catch (Exception ex)
        {
            return FailureWithException("micro.detailFailed", ex);
        }
    }

    public async Task<MicroRuntimeResult> Create(string modelName, object data, string userAccount)
    {
        try
        {
            var resolution = await ResolveConfigAsync(modelName, c => c.SupportCreate, "micro.createNotSupported");
            if (resolution.Failure != null) return resolution.Failure;
            var config = resolution.Config!;

            await microTableService.EnsureTableExistsAsync(config);
            var dataDict = ConvertObjectToDictionary(data);
            var subTableData = ExtractSubTableData(dataDict);
            var validationErrors = ValidateMicroData(config, dataDict);
            validationErrors.AddRange(ValidateMicroSubTableData(config, subTableData));
            if (validationErrors.Count > 0)
            {
                return ValidationFailure(validationErrors);
            }

            var result = await microTableService.ExecuteMicroInsertWithSubTablesAsync(
                config,
                dataDict,
                subTableData,
                userAccount);

            return Success("common.addSuccess", result);
        }
        catch (Exception ex)
        {
            return FailureWithException("micro.addFailed", ex);
        }
    }

    public async Task<MicroRuntimeResult> Update(string modelName, long id, object data, string userAccount)
    {
        try
        {
            var resolution = await ResolveConfigAsync(modelName, c => c.SupportUpdate, "micro.updateNotSupported");
            if (resolution.Failure != null) return resolution.Failure;
            var config = resolution.Config!;

            await microTableService.EnsureTableExistsAsync(config);
            var dataDict = ConvertObjectToDictionary(data);
            var subTableData = ExtractSubTableData(dataDict);
            var validationErrors = ValidateMicroData(config, dataDict);
            validationErrors.AddRange(ValidateMicroSubTableData(config, subTableData));
            if (validationErrors.Count > 0)
            {
                return ValidationFailure(validationErrors);
            }

            var result = await microTableService.ExecuteMicroUpdateWithSubTablesAsync(
                config,
                id,
                dataDict,
                subTableData,
                userAccount);

            return result ? Success("common.updateSuccess") : Failure("micro.dataNotFound");
        }
        catch (Exception ex)
        {
            return FailureWithException("micro.updateFailed", ex);
        }
    }

    public async Task<MicroRuntimeResult> Delete(string modelName, long id, string userAccount)
    {
        try
        {
            var resolution = await ResolveConfigAsync(modelName, c => c.SupportDelete, "micro.deleteNotSupported");
            if (resolution.Failure != null) return resolution.Failure;
            var config = resolution.Config!;

            await microTableService.EnsureTableExistsAsync(config);
            var result = await microTableService.ExecuteMicroDeleteWithSubTablesAsync(config, id, userAccount);
            return result ? Success("common.deleteSuccess") : Failure("micro.dataNotFound");
        }
        catch (Exception ex)
        {
            return FailureWithException("micro.deleteFailed", ex);
        }
    }

    public async Task<MicroRuntimeResult> BatchDelete(
        string modelName,
        MicroBatchDeleteParameter parameter,
        string userAccount)
    {
        try
        {
            var resolution = await ResolveConfigAsync(
                modelName,
                c => c.SupportDelete && c.SupportBatchDelete,
                "micro.batchDeleteNotSupported");
            if (resolution.Failure != null) return resolution.Failure;
            var config = resolution.Config!;

            if (parameter.Ids.Count == 0)
            {
                return Failure("micro.selectDeleteData");
            }

            await microTableService.EnsureTableExistsAsync(config);
            var rowsAffected = await microTableService.ExecuteMicroBatchDeleteWithSubTablesAsync(
                config,
                parameter.Ids,
                userAccount);

            return SuccessMessage(L("micro.batchDeleteSuccess", rowsAffected), new { deleted = rowsAffected });
        }
        catch (Exception ex)
        {
            return FailureWithException("micro.deleteFailed", ex);
        }
    }

    public async Task<MicroRuntimeExportResult> ExportExcel(
        string modelName,
        string keyword,
        string filters,
        string sortField,
        string sortOrder,
        string userAccount)
    {
        try
        {
            var config = await GetActiveConfigAsync(modelName);
            if (config == null)
            {
                return ExportFailure("micro.configNotFound");
            }

            if (!config.SupportExport)
            {
                return ExportFailure("micro.exportNotSupported");
            }

            await microTableService.EnsureTableExistsAsync(config);
            var fields = string.IsNullOrEmpty(config.Fields)
                ? new List<FieldConfig>()
                : JsonSerializer.Deserialize<List<FieldConfig>>(config.Fields);

            var result = await microTableService.ExecuteMicroQueryAsync(
                config,
                1,
                int.MaxValue,
                keyword,
                ParseQueryFilters(filters),
                sortField,
                sortOrder,
                userAccount);

            var resultType = result.GetType();
            var listProperty = resultType.GetProperty("list");
            var dataList = listProperty?.GetValue(result) as List<Dictionary<string, object>>;
            if (dataList == null || !dataList.Any())
            {
                return ExportFailure("micro.noDataToExport");
            }

            var fileName = $"{config.ConfigName}_export.xlsx";
            var excelData = await ExcelExportHelper.ExportDictionaryToExcelWithFieldConfigAsync(dataList, fields!, fileName);
            return new MicroRuntimeExportResult(true, string.Empty, excelData, fileName);
        }
        catch (Exception ex)
        {
            return ExportFailureWithException("micro.exportFailed", ex);
        }
    }

    public async Task<MicroRuntimeResult> ImportExcel(
        string modelName,
        string fileName,
        Stream stream,
        string userAccount)
    {
        try
        {
            var allowedExtensions = new[] { ".xlsx", ".xls" };
            var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return Failure("micro.excelFormatInvalid");
            }

            var resolution = await ResolveConfigAsync(modelName, c => c.SupportImport, "micro.importNotSupported");
            if (resolution.Failure != null) return resolution.Failure;
            var config = resolution.Config!;

            await microTableService.EnsureTableExistsAsync(config);
            var fields = string.IsNullOrEmpty(config.Fields)
                ? new List<FieldConfig>()
                : JsonSerializer.Deserialize<List<FieldConfig>>(config.Fields);

            var importedData = await ExcelImportHelper.ImportAndValidateDataAsync(stream, fields!, localizer);
            var successCount = 0;
            var errorCount = 0;
            var errorMessages = new List<string>();

            try
            {
                successCount = await microTableService.ExecuteMicroBatchInsertAsync(config, importedData, userAccount);
            }
            catch (Exception ex)
            {
                errorCount = importedData.Count;
                errorMessages.Add(FailureMessage("micro.importFailed", ex));
            }

            var total = importedData.Count;
            var resultMsg = L("micro.importSuccess", successCount, errorCount);
            if (errorCount > 0)
            {
                resultMsg = L("micro.importSuccessWithError", successCount, errorCount, string.Join("; ", errorMessages.Take(5)));
            }

            return SuccessMessage(resultMsg, new { total, success = successCount, failed = errorCount });
        }
        catch (Exception ex)
        {
            return FailureWithException("micro.importFailed", ex);
        }
    }

    private async Task<SysMicroAppConfig?> GetActiveConfigAsync(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return null;

        await microTableService.EnsureMicroConfigSubTablesColumnAsync();

        var cacheKey = MicroConfigCacheKeys.ActiveConfig(modelName);
        var cachedJson = await dtSoftCache.GetAsync<string>(cacheKey);
        if (!string.IsNullOrWhiteSpace(cachedJson))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<SysMicroAppConfig>(cachedJson);
                if (cached is { Status: 1 } &&
                    cached.ModelName.Equals(modelName, StringComparison.OrdinalIgnoreCase))
                {
                    return cached;
                }
            }
            catch
            {
                // 忽略缓存反序列化失败，回源数据库。
            }
        }

        var config = await context.Set<SysMicroAppConfig>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ModelName == modelName && c.Status == 1);

        if (config != null)
        {
            await dtSoftCache.SetAsync(cacheKey, JsonSerializer.Serialize(config), TimeSpan.FromMinutes(1));
        }

        return config;
    }

    private async Task<(SysMicroAppConfig? Config, MicroRuntimeResult? Failure)> ResolveConfigAsync(
        string modelName,
        Func<SysMicroAppConfig, bool>? isSupported = null,
        string? unsupportedKey = null)
    {
        var config = await GetActiveConfigAsync(modelName);
        if (config == null)
        {
            return (null, Failure("micro.configNotFound"));
        }

        if (isSupported != null && !isSupported(config))
        {
            return (null, Failure(unsupportedKey ?? "common.operationFailed"));
        }

        return (config, null);
    }

    private Dictionary<string, object> ConvertObjectToDictionary(object obj)
    {
        var jsonString = JsonSerializer.Serialize(obj);
        using var jsonDocument = JsonDocument.Parse(jsonString);
        var result = new Dictionary<string, object>();

        foreach (var property in jsonDocument.RootElement.EnumerateObject())
        {
            result[property.Name] = ConvertJsonValue(property.Value)!;
        }

        return result;
    }

    private object? ConvertJsonValue(JsonElement jsonElement)
    {
        return jsonElement.ValueKind switch
        {
            JsonValueKind.String => jsonElement.GetString(),
            JsonValueKind.Number => jsonElement.TryGetInt32(out var intVal) ? intVal : jsonElement.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => ConvertObjectToDictionary(jsonElement),
            JsonValueKind.Array => jsonElement.EnumerateArray().Select(ConvertJsonValue).ToArray(),
            _ => jsonElement.ToString()
        };
    }

    private Dictionary<string, List<Dictionary<string, object>>> ExtractSubTableData(Dictionary<string, object> dataDict)
    {
        var result = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase);
        if (!dataDict.TryGetValue(RuntimeSubTablesKey, out var rawSubTables))
        {
            return result;
        }

        dataDict.Remove(RuntimeSubTablesKey);

        if (rawSubTables is JsonElement jsonElement)
        {
            rawSubTables = ConvertJsonValue(jsonElement);
        }

        if (rawSubTables is not Dictionary<string, object> subTableObject)
        {
            return result;
        }

        foreach (var kvp in subTableObject)
        {
            var rows = new List<Dictionary<string, object>>();
            if (kvp.Value is object[] rowArray)
            {
                foreach (var rowItem in rowArray)
                {
                    if (rowItem is Dictionary<string, object> row)
                    {
                        rows.Add(row);
                    }
                }
            }

            result[kvp.Key] = rows;
        }

        return result;
    }

    private static List<MicroQueryFilter> ParseQueryFilters(string filters)
    {
        if (string.IsNullOrWhiteSpace(filters))
        {
            return new List<MicroQueryFilter>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<MicroQueryFilter>>(filters, CaseInsensitiveJsonOptions) ??
                   new List<MicroQueryFilter>();
        }
        catch
        {
            return new List<MicroQueryFilter>();
        }
    }

    private List<string> ValidateMicroData(SysMicroAppConfig config, Dictionary<string, object> dataDict)
    {
        var fields = string.IsNullOrWhiteSpace(config.Fields)
            ? new List<FieldConfig>()
            : JsonSerializer.Deserialize<List<FieldConfig>>(config.Fields) ?? new List<FieldConfig>();

        return ValidateFields(fields, dataDict, string.Empty);
    }

    private List<string> ValidateMicroSubTableData(
        SysMicroAppConfig config,
        Dictionary<string, List<Dictionary<string, object>>> subTableData)
    {
        var errors = new List<string>();
        var subTables = MicroConfigSchema.ParseSubTables(config.SubTables);
        if (subTables.Count == 0)
        {
            return errors;
        }

        var configuredNames = subTables
            .Where(subTable => !string.IsNullOrWhiteSpace(subTable.TableName))
            .Select(subTable => subTable.TableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var submittedName in subTableData.Keys)
        {
            if (!configuredNames.Contains(submittedName))
            {
                errors.Add(L("micro.subTableNotConfigured", submittedName));
            }
        }

        foreach (var subTable in subTables)
        {
            subTableData.TryGetValue(subTable.TableName, out var rows);
            rows ??= new List<Dictionary<string, object>>();

            if (subTable.MinRows.HasValue && rows.Count < subTable.MinRows.Value)
            {
                errors.Add(L("micro.subTableMinRows", subTable.Label, subTable.MinRows.Value));
            }

            if (subTable.MaxRows.HasValue && subTable.MaxRows.Value > 0 && rows.Count > subTable.MaxRows.Value)
            {
                errors.Add(L("micro.subTableMaxRows", subTable.Label, subTable.MaxRows.Value));
            }

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                errors.AddRange(ValidateFields(
                    subTable.Fields,
                    rows[rowIndex],
                    L("micro.rowPrefix", subTable.Label, rowIndex + 1)));
            }
        }

        return errors;
    }

    private List<string> ValidateFields(List<FieldConfig> fields, Dictionary<string, object> dataDict, string prefix)
    {
        var errors = new List<string>();
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.FieldName))
            {
                continue;
            }

            dataDict.TryGetValue(field.FieldName, out var rawValue);
            var value = rawValue is JsonElement jsonElement ? ConvertJsonValue(jsonElement) : rawValue;
            var textValue = value?.ToString();
            var fieldLabel = string.IsNullOrWhiteSpace(prefix) ? field.Label : $"{prefix}{field.Label}";

            if (field.Required && string.IsNullOrWhiteSpace(textValue))
            {
                errors.Add(L("micro.fieldRequired", fieldLabel));
                continue;
            }

            if (string.IsNullOrWhiteSpace(textValue))
            {
                continue;
            }

            if (field.MinLength.HasValue && textValue.Length < field.MinLength.Value)
            {
                errors.Add(L("micro.fieldMinLength", fieldLabel, field.MinLength.Value));
            }

            if (field.MaxLength.HasValue && textValue.Length > field.MaxLength.Value)
            {
                errors.Add(L("micro.fieldMaxLength", fieldLabel, field.MaxLength.Value));
            }

            if (field.FieldType == "number" && decimal.TryParse(textValue, out var numberValue))
            {
                if (field.MinValue.HasValue && numberValue < field.MinValue.Value)
                {
                    errors.Add(L("micro.fieldMinValue", fieldLabel, field.MinValue.Value));
                }

                if (field.MaxValue.HasValue && numberValue > field.MaxValue.Value)
                {
                    errors.Add(L("micro.fieldMaxValue", fieldLabel, field.MaxValue.Value));
                }
            }

            if (!string.IsNullOrWhiteSpace(field.Pattern) && !IsRegexMatch(textValue, field.Pattern))
            {
                errors.Add(L("micro.fieldFormatInvalid", fieldLabel));
            }
        }

        return errors;
    }

    private static bool IsRegexMatch(string value, string pattern)
    {
        try
        {
            return Regex.IsMatch(value, pattern);
        }
        catch
        {
            return false;
        }
    }

    private MicroRuntimeResult Success(string key, object? data = null) => new(true, L(key), data);

    private MicroRuntimeResult SuccessMessage(string message, object? data = null) => new(true, message, data);

    private MicroRuntimeResult Failure(string key) => new(false, L(key));

    private MicroRuntimeResult ValidationFailure(IEnumerable<string> errors) => new(false, string.Join("；", errors));

    private MicroRuntimeResult FailureWithException(string key, Exception ex) => new(false, FailureMessage(key, ex));

    private MicroRuntimeExportResult ExportFailure(string key) => new(false, L(key));

    private MicroRuntimeExportResult ExportFailureWithException(string key, Exception ex) => new(false, FailureMessage(key, ex));

    private string FailureMessage(string key, Exception ex) => L("common.failedWithReason", L(key), ex.Message);
}

public sealed record MicroRuntimeResult(bool Success, string Msg, object? Data = null);

public sealed record MicroRuntimeExportResult(
    bool Success,
    string Msg,
    byte[]? FileContent = null,
    string? FileName = null);
