using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

/// <summary>
/// 用户部门关联实体
/// </summary>
[Table("sys_user_member")]
public class SysUserMember
{
    /// <summary>
    /// 主键ID（雪花ID）
    /// </summary>
    [Key]
    public long ItemId { get; init; }
    
    /// <summary>
    /// 部门ID
    /// </summary>
    [ForeignKey("SysOu")]
    public long DepartmentId { get; init; }
    
    /// <summary>
    /// 用户账号
    /// </summary>
    [ForeignKey("SysUser")]
    public string? UserAcc { get; init; }
    
    /// <summary>
    /// 部门导航属性
    /// </summary>
    public SysOu? SysOu { get; init; }
    
    /// <summary>
    /// 用户导航属性
    /// </summary>
    public SysUser? SysUser { get; init; }
}
