using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_menu")]
public class SysMenu
{
    [Key]
    public long ItemId { get; set; }
    public long Pid { get; set; }
    public string? MenuName { get; set; }
    public string? MenuPath { get; set; }
    public int Order { get; set; }
    public string? Icon { get; set; }
    public bool IsHidden { get; set; }
    public int MType { get; set; }
    public List<SysMenuAuthority>? SysMenuAuthority { get; init; } = [];
}
