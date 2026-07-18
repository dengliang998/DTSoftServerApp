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
    [Required]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_-]*$", ErrorMessage = "数据源编码只能包含英文、数字、中划线和下划线，且以英文开头")]
    public required string Code { get; set; }

    [Required]
    [StringLength(100)]
    public required string Name { get; set; }

    [Required]
    public required string SourceType { get; set; }

    public long? ConnectionId { get; set; }

    [Required]
    public required string ExecuteMode { get; set; }

    public string? SqlText { get; set; }

    public string? HttpConfig { get; set; }

    public List<EsbParameterConfig>? Parameters { get; set; }

    public EsbResultMapping? ResultMapping { get; set; }

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
    [Required]
    public long ItemId { get; set; }
}

/// <summary>
/// ESB 数据源删除参数。
/// </summary>
public class EsbDataSourceDeleteParameter
{
    [Required]
    public long ItemId { get; set; }
}

/// <summary>
/// ESB 执行请求。
/// </summary>
public class EsbExecuteRequest
{
    [Required]
    public required string Code { get; set; }

    public Dictionary<string, JsonNode?>? Parameters { get; set; }
}

/// <summary>
/// ESB 参数定义。
/// </summary>
public class EsbParameterConfig
{
    [Required]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$", ErrorMessage = "参数名只能包含英文、数字和下划线，且以英文开头")]
    public required string Name { get; set; }

    public string? Label { get; set; }

    public string Type { get; set; } = "string";

    public bool Required { get; set; }

    public JsonNode? DefaultValue { get; set; }
}

/// <summary>
/// ESB 返回映射。
/// </summary>
public class EsbResultMapping
{
    public string? LabelField { get; set; }

    public string? ValueField { get; set; }
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

    public EsbResultMapping? ResultMapping { get; set; }

    public int Status { get; set; }

    public int MaxRows { get; set; }

    public int TimeoutSeconds { get; set; }

    public string? Remark { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}
