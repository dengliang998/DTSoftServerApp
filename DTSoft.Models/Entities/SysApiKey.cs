using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

/// <summary>
/// API密钥实体 - 用于第三方系统集成认证
/// </summary>
[Table("sys_api_key")]
public class SysApiKey
{
    /// <summary>
    /// 主键ID（雪花ID）
    /// </summary>
    [Key]
    public long ItemId { get; set; }
    
    /// <summary>
    /// 密钥名称（唯一标识）
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string KeyName { get; set; } = string.Empty;
    
    /// <summary>
    /// 密钥（加密存储，SHA256哈希）
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string SecretKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 描述信息
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }
    
    /// <summary>
    /// 创建人
    /// </summary>
    [MaxLength(50)]
    public string? CreatedBy { get; set; }
    
    /// <summary>
    /// 过期时间（可选，为null表示永不过期）
    /// </summary>
    public DateTime? ExpireTime { get; set; }
}
