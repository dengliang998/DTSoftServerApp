using System.ComponentModel.DataAnnotations;

namespace DTSoft.Models.Parameter.Esb;

/// <summary>
/// ESB 服务连接查询参数。
/// </summary>
public class EsbServiceConnectionQueryParameter
{
    public string? Keyword { get; set; }

    public string? ServiceType { get; set; }

    public int? Status { get; set; }

    public int? PageNum { get; set; }

    public int? PageSize { get; set; }
}

/// <summary>
/// ESB 服务连接新增参数。
/// </summary>
public class EsbServiceConnectionAddParameter
{
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_-]*$", ErrorMessage = "esb.connectionCodeInvalid")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "validation.required")]
    [StringLength(100, ErrorMessage = "validation.stringLength")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "validation.required")]
    public required string ServiceType { get; set; }

    public string? DbType { get; set; }

    public string? ConnectionString { get; set; }

    public string? WebApiConfig { get; set; }

    public int Status { get; set; } = 1;

    public int? TimeoutSeconds { get; set; }

    public string? Remark { get; set; }
}

/// <summary>
/// ESB 服务连接更新参数。
/// </summary>
public class EsbServiceConnectionUpdateParameter : EsbServiceConnectionAddParameter
{
    public long ItemId { get; set; }
}

/// <summary>
/// ESB 服务连接删除参数。
/// </summary>
public class EsbServiceConnectionDeleteParameter
{
    public long ItemId { get; set; }
}

/// <summary>
/// ESB 服务连接测试参数。
/// </summary>
public class EsbServiceConnectionTestParameter
{
    public long? ItemId { get; set; }

    public string? ServiceType { get; set; }

    public string? DbType { get; set; }

    public string? ConnectionString { get; set; }

    public string? WebApiConfig { get; set; }

    public int? TimeoutSeconds { get; set; }
}

/// <summary>
/// ESB 服务连接响应。
/// </summary>
public class EsbServiceConnectionResponse
{
    public long ItemId { get; set; }

    public required string Code { get; set; }

    public required string Name { get; set; }

    public required string ServiceType { get; set; }

    public string? DbType { get; set; }

    public string? ConnectionString { get; set; }

    public string? WebApiConfig { get; set; }

    public int Status { get; set; }

    public int TimeoutSeconds { get; set; }

    public string? Remark { get; set; }

    public bool IsDefault { get; set; }

    public DateTime? CreateTime { get; set; }

    public DateTime? UpdateTime { get; set; }
}
