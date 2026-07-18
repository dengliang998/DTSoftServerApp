using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.MicroApp;
using System.Text.Json;

namespace DTSoft.AppService.MicroApp;

/// <summary>
/// 微应用配置解析、归一化与响应映射。
/// </summary>
public static class MicroConfigSchema
{
    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static MicroConfigResponse ToResponse(
        SysMicroAppConfig config,
        List<FieldConfig>? fieldsOverride = null,
        List<SubTableConfig>? subTablesOverride = null)
    {
        return new MicroConfigResponse
        {
            ItemId = config.ItemId,
            ConfigName = config.ConfigName,
            ModelName = config.ModelName,
            ConfigDesc = config.ConfigDesc,
            Status = config.Status,
            SupportCreate = config.SupportCreate,
            SupportUpdate = config.SupportUpdate,
            SupportDelete = config.SupportDelete,
            SupportBatchDelete = config.SupportBatchDelete,
            SupportImport = config.SupportImport,
            SupportExport = config.SupportExport,
            ShowSubTablesInList = config.ShowSubTablesInList,
            DataScope = NormalizeDataScope(config.DataScope),
            FormColumns = NormalizeFormColumns(config.FormColumns),
            QueryColumns = NormalizeQueryColumns(config.QueryColumns),
            MicroAppPath = config.ApiPrefix,
            Fields = fieldsOverride ?? ParseFields(config.Fields),
            SubTables = subTablesOverride ?? ParseSubTables(config.SubTables),
            CreateTime = config.CreateTime,
            UpdateTime = config.UpdateTime
        };
    }

    public static List<FieldConfig> ParseFields(string? fields)
    {
        if (string.IsNullOrWhiteSpace(fields))
        {
            return new List<FieldConfig>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<FieldConfig>>(fields, CaseInsensitiveJsonOptions) ??
                   new List<FieldConfig>();
        }
        catch
        {
            return new List<FieldConfig>();
        }
    }

    public static List<SubTableConfig> ParseSubTables(string? subTables)
    {
        if (string.IsNullOrWhiteSpace(subTables))
        {
            return new List<SubTableConfig>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<SubTableConfig>>(subTables, CaseInsensitiveJsonOptions) ??
                   new List<SubTableConfig>();
        }
        catch
        {
            return new List<SubTableConfig>();
        }
    }

    public static string NormalizeDataScope(string? dataScope)
    {
        return dataScope?.Trim().ToLowerInvariant() switch
        {
            "self" => "self",
            "department" => "department",
            _ => "all"
        };
    }

    public static int NormalizeFormColumns(int? formColumns)
    {
        return formColumns is >= 1 and <= 4 ? formColumns.Value : 1;
    }

    public static int NormalizeQueryColumns(int? queryColumns)
    {
        return queryColumns is >= 1 and <= 4 ? queryColumns.Value : 1;
    }

    public static List<SubTableConfig> NormalizeSubTables(List<SubTableConfig>? subTables)
    {
        return subTables?
                   .Where(subTable => !string.IsNullOrWhiteSpace(subTable.TableName))
                   .Select((subTable, index) => new SubTableConfig
                   {
                       Label = string.IsNullOrWhiteSpace(subTable.Label) ? subTable.TableName : subTable.Label,
                       TableName = subTable.TableName.Trim(),
                       MinRows = Math.Max(0, subTable.MinRows ?? 0),
                       MaxRows = subTable.MaxRows is > 0 ? subTable.MaxRows : null,
                       SortOrder = subTable.SortOrder ?? index + 1,
                       EnableLookup = subTable.EnableLookup == true,
                       LookupDataSourceCode = subTable.EnableLookup == true ? subTable.LookupDataSourceCode : string.Empty,
                       LookupParams = subTable.EnableLookup == true ? subTable.LookupParams : string.Empty,
                       LookupPageSize = subTable.EnableLookup == true && subTable.LookupPageSize is >= 5 and <= 200
                           ? subTable.LookupPageSize
                           : null,
                       LookupColumns = subTable.EnableLookup == true
                           ? subTable.LookupColumns?.Where(column =>
                                   !string.IsNullOrWhiteSpace(column.Field) &&
                                   !string.IsNullOrWhiteSpace(column.Label))
                               .ToList() ?? new List<LookupColumnConfig>()
                           : new List<LookupColumnConfig>(),
                       LookupMappings = subTable.EnableLookup == true
                           ? subTable.LookupMappings?.Where(mapping =>
                                   !string.IsNullOrWhiteSpace(mapping.SourceField) &&
                                   !string.IsNullOrWhiteSpace(mapping.TargetField))
                               .ToList() ?? new List<LookupMappingConfig>()
                           : new List<LookupMappingConfig>(),
                       Fields = subTable.Fields ?? new List<FieldConfig>()
                   })
                   .OrderBy(subTable => subTable.SortOrder ?? 0)
                   .ToList() ??
               new List<SubTableConfig>();
    }
}
