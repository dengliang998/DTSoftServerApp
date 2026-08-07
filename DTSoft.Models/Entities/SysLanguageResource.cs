using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_language_resource")]
public class SysLanguageResource
{
    [Key]
    public long ItemId { get; set; }

    public string ResourceKey { get; set; } = string.Empty;

    public string? Module { get; set; }

    public string? Description { get; set; }

    public string? ValuesJson { get; set; }
}
