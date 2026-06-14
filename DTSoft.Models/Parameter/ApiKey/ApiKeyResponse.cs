namespace DTSoft.Models.Parameter.ApiKey;

/// <summary>
/// API密钥响应DTO
/// </summary>
public class ApiKeyResponse
{
    /// <summary>
    /// 主键ID
    /// </summary>
    public long ItemId { get; set; }
    
    /// <summary>
    /// 密钥名称
    /// </summary>
    public string KeyName { get; set; } = string.Empty;
    
    /// <summary>
    /// 密钥（仅在创建时返回明文，其他时候返回null）
    /// </summary>
    public string? SecretKey { get; set; }
    
    /// <summary>
    /// 描述信息
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }
    
    /// <summary>
    /// 创建人
    /// </summary>
    public string? CreatedBy { get; set; }
    
    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime? ExpireTime { get; set; }
}

/// <summary>
/// API密钥登录响应
/// </summary>
public class ApiKeyLoginResponse
{
    /// <summary>
    /// JWT Token
    /// </summary>
    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime Expires { get; set; }
    
    /// <summary>
    /// 过期时间（小时）
    /// </summary>
    public int ExpiresInHours { get; set; }
    
    /// <summary>
    /// 过期时间（秒）
    /// </summary>
    public int ExpiresInSeconds { get; set; }
}
