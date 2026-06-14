using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

/// <summary>
/// 角色成员
/// </summary>
[Table("sys_rolemember")]
public class SysRoleMember
{
    [Key]
    public long ItemId { get; init; }
    
    [ForeignKey("SysRole")]
    public long RoleId { get; init; }
    
    [ForeignKey("SysUser")]
    public string? UserAcc { get; init; }
    public SysRole? SysRole { get; init; }

    public SysUser? SysUser { get; init; }
}
