using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_systemurl")]
public class SysSystemUrl
{
    [Key]
    public long ItemId { get; init; }
    public string? PageCode { get; set; }
    public string? MenuName { get; set; }
    public string? PageUrl { get; set; }
    public long MenuId { get; set; }
}
