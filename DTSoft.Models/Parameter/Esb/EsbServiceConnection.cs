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
    [Required]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_-]*$", ErrorMessage = "连接编码只能包含英文、数字、中划线和下划线，且以英文开头")]
    public required string Code { get; set; }

    [Required]
    [StringLength(100)]
    public required string Name { get; set; }

    [Required]
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
    [Required]
    public long ItemId { get; set; }
}

/// <summary>
/// ESB 服务连接删除参数。
/// </summary>
public class EsbServiceConnectionDeleteParameter
{
    [Required]
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
