using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_role")]
public class SysRole
{
    [Key]
    public long ItemId { get; set; }
    public string? RoleName { get; set; }
    public List<SysRoleMember>? SysRoleMember { get; init; }
    public List<SysMenuAuthority>? SysMenuAuthority { get; init; }
}
