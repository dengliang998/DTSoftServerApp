using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace DTSoft.Models.Parameter.Esb;

/// <summary>
/// ESB 数据源查询参数。
/// </summary>
public class EsbDataSourceQueryParameter
{
    public string? Keyword { get; set; }

    public string? SourceType { get; set; }

    public long? ConnectionId { get; set; }

    public int? Status { get; set; }

    public int? PageNum { get; set; }

    public int? PageSize { get; set; }
}

/// <summary>
/// ESB 数据源新增参数。
/// </summary>
public class EsbDataSourceAddParameter
{
    [Required(ErrorMessage = "validation.required")]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_-]*$", ErrorMessage = "esb.dataSourceCodeInvalid")]
    public required string Code { get; set; }

    [Required(ErrorMessage = "validation.required")]
    [StringLength(100, ErrorMessage = "validation.stringLength")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "validation.required")]
    public required string SourceType { get; set; }

    public long? ConnectionId { get; set; }

    [Required(ErrorMessage = "validation.required")]
    public required string ExecuteMode { get; set; }

    public string? SqlText { get; set; }

    public string? HttpConfig { get; set; }

    public List<EsbParameterConfig>? Parameters { get; set; }

    public int Status { get; set; } = 1;

    public int? MaxRows { get; set; }

    public int? TimeoutSeconds { get; set; }

    public string? Remark { get; set; }
}

/// <summary>
/// ESB 数据源更新参数。
/// </summary>
public class EsbDataSourceUpdateParameter : EsbDataSourceAddParameter
{
    public long ItemId { get; set; }
}

/// <summary>
/// ESB 数据源删除参数。
/// </summary>
public class EsbDataSourceDeleteParameter
{
    public long ItemId { get; set; }
}

/// <summary>
/// ESB 执行请求。
/// </summary>
public class EsbExecuteRequest
{
    [Required(ErrorMessage = "validation.required")]
    public required string Code { get; set; }

    public Dictionary<string, JsonNode?>? Parameters { get; set; }

    public int? PageNum { get; set; }

    public int? PageSize { get; set; }
}

/// <summary>
/// ESB 参数定义。
/// </summary>
public class EsbParameterConfig
{
    [Required(ErrorMessage = "validation.required")]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$", ErrorMessage = "esb.parameterNameInvalid")]
    public required string Name { get; set; }

    public string? Label { get; set; }

    public string Type { get; set; } = "string";

    public bool Required { get; set; }

    public JsonNode? DefaultValue { get; set; }
}

/// <summary>
/// ESB 数据源响应。
/// </summary>
public class EsbDataSourceResponse
{
    public long ItemId { get; set; }

    public required string Code { get; set; }

    public required string Name { get; set; }

    public required string SourceType { get; set; }

    public long? ConnectionId { get; set; }

    public string? ConnectionName { get; set; }

    public required string ExecuteMode { get; set; }

    public string? SqlText { get; set; }

    public string? HttpConfig { get; set; }

    public List<EsbParameterConfig> Parameters { get; set; } = [];

    public int Status { get; set; }

    public int MaxRows { get; set; }

    public int TimeoutSeconds { get; set; }

    public string? Remark { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// ESB 分页执行结果。
/// </summary>
public class EsbPagedExecuteResponse
{
    public List<Dictionary<string, object?>> List { get; set; } = [];

    public int Total { get; set; }

    public int PageNum { get; set; }

    public int PageSize { get; set; }
}
