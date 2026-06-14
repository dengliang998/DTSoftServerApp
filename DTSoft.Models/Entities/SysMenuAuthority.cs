using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

/// <summary>
/// 菜单权限
/// </summary>
[Table("sys_menuauthority")]
public class SysMenuAuthority
{
    [Key]
    public long ItemId { get; init; }
    public long RoleID { get; init; }
    public long MenuID { get; init; }

    public SysMenu? SysMenu { get; init; }
    public SysRole? SysRole { get; init; }
}
