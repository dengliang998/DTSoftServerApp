using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

/// <summary>
/// ESB 服务连接配置。
/// </summary>
[Table("sys_esb_serviceconnection")]
public class SysEsbServiceConnection
{
    /// <summary>
    /// 主键。
    /// </summary>
    [Key]
    public long ItemId { get; init; }

    /// <summary>
    /// 连接编码。
    /// </summary>
    [StringLength(100)]
    public required string Code { get; set; }

    /// <summary>
    /// 连接名称。
    /// </summary>
    [StringLength(100)]
    public required string Name { get; set; }

    /// <summary>
    /// 服务类型：database/webapi。
    /// </summary>
    [StringLength(20)]
    public required string ServiceType { get; set; }

    /// <summary>
    /// 数据库类型：sqlserver/mysql/postgresql/oracle。
    /// </summary>
    [StringLength(50)]
    public string? DbType { get; set; }

    /// <summary>
    /// 数据库连接字符串。
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// WebApi 配置，预留。
    /// </summary>
    public string? WebApiConfig { get; set; }

    /// <summary>
    /// 状态，1-启用，0-禁用。
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 超时时间，单位秒。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 备注。
    /// </summary>
    [StringLength(500)]
    public string? Remark { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreateTime { get; init; }

    /// <summary>
    /// 更新时间。
    /// </summary>
    public DateTime UpdateTime { get; set; }
}
