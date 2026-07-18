using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

/// <summary>
/// ESB 数据源配置。
/// </summary>
[Table("sys_esb_datasource")]
public class SysEsbDataSource
{
    /// <summary>
    /// 主键。
    /// </summary>
    [Key]
    public long ItemId { get; init; }

    /// <summary>
    /// 数据源编码。
    /// </summary>
    [StringLength(100)]
    public required string Code { get; set; }

    /// <summary>
    /// 数据源名称。
    /// </summary>
    [StringLength(100)]
    public required string Name { get; set; }

    /// <summary>
    /// 绑定的 ESB 服务连接 ID，空表示默认系统库。
    /// </summary>
    public long? ConnectionId { get; set; }

    /// <summary>
    /// 数据源类型：sql/http。
    /// </summary>
    [StringLength(20)]
    public required string SourceType { get; set; }

    /// <summary>
    /// 执行模式：query。
    /// </summary>
    [StringLength(20)]
    public required string ExecuteMode { get; set; }

    /// <summary>
    /// SQL 内容。
    /// </summary>
    public string? SqlText { get; set; }

    /// <summary>
    /// 外部接口配置，预留。
    /// </summary>
    public string? HttpConfig { get; set; }

    /// <summary>
    /// 参数定义 JSON。
    /// </summary>
    public string? ParameterConfig { get; set; }

    /// <summary>
    /// 返回映射 JSON。
    /// </summary>
    public string? ResultMapping { get; set; }

    /// <summary>
    /// 状态，1-启用，0-禁用。
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 最大返回行数。
    /// </summary>
    public int MaxRows { get; set; } = 500;

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
