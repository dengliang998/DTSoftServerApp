using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

/// <summary>
/// 用户直属主管关联（一个用户最多一个直属主管）
/// </summary>
[Table("sys_user_supervisor")]
public class SysUserSupervisor
{
    /// <summary>
    /// 主键ID（雪花ID）
    /// </summary>
    [Key]
    public long ItemId { get; init; }

    /// <summary>
    /// 用户账号（下属）
    /// </summary>
    [ForeignKey("SysUser")]
    public string? UserAcc { get; init; }

    /// <summary>
    /// 直属主管账号（上级）
    /// </summary>
    [ForeignKey("SupervisorUser")]
    public string? SupervisorAcc { get; init; }

    /// <summary>
    /// 用户导航属性
    /// </summary>
    public SysUser? SysUser { get; init; }

    /// <summary>
    /// 主管导航属性
    /// </summary>
    public SysUser? SupervisorUser { get; init; }
}

