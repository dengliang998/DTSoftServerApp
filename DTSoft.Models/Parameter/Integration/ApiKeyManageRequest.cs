using System.ComponentModel.DataAnnotations;

namespace DTSoft.Models.Parameter.Integration;

/// <summary>
/// API密钥管理请求参数
/// </summary>
public class ApiKeyCreateRequest
{
    /// <summary>
    /// 密钥名称（唯一）
    /// </summary>
    [Required(ErrorMessage = "integration.keyNameRequired")]
    [MaxLength(100, ErrorMessage = "integration.keyNameMaxLength")]
    public string KeyName { get; set; } = string.Empty;
    
    /// <summary>
    /// 描述信息
    /// </summary>
    [MaxLength(500, ErrorMessage = "integration.descriptionMaxLength")]
    public string? Description { get; set; }
    
    /// <summary>
    /// 过期时间（可选）
    /// </summary>
    public DateTime? ExpireTime { get; set; }
}

/// <summary>
/// API密钥更新请求参数
/// </summary>
public class ApiKeyUpdateRequest
{
    /// <summary>
    /// 主键ID
    /// </summary>
    [Required(ErrorMessage = "integration.itemIdRequired")]
    public long ItemId { get; set; }
    
    /// <summary>
    /// 描述信息
    /// </summary>
    [MaxLength(500, ErrorMessage = "integration.descriptionMaxLength")]
    public string? Description { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; }
    
    /// <summary>
    /// 过期时间（可选）
    /// </summary>
    public DateTime? ExpireTime { get; set; }
}

/// <summary>
/// API密钥删除请求参数
/// </summary>
public class ApiKeyDeleteRequest
{
    /// <summary>
    /// 主键ID
    /// </summary>
    [Required(ErrorMessage = "integration.itemIdRequired")]
    public long ItemId { get; set; }
}

/// <summary>
/// API密钥查询请求参数
/// </summary>
public class ApiKeyQueryRequest
{
    /// <summary>
    /// 密钥名称（模糊查询）
    /// </summary>
    public string? KeyName { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool? Enabled { get; set; }
}
