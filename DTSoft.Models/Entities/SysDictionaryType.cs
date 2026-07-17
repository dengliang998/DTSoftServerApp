using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_dictionary_type")]
public class SysDictionaryType
{
    [Key]
    public long ItemId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DictCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DictName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool Enabled { get; set; } = true;

    public int Sort { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    public List<SysDictionaryData>? Items { get; init; }
}
