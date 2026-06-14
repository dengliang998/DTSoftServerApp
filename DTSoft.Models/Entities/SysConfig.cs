using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_config")]
public class SysConfig
{
    /// <summary>
    /// 主键
    /// </summary>
    [Key]
    public long ItemId { get; init; }

    /// <summary>
    /// 登录页背景图
    /// </summary>
    public string? LoginImg { get; set; }

    /// <summary>
    /// 系统名称
    /// </summary>
    public string? SystemName { get; set; }
}
