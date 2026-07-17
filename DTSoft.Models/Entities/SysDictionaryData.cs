using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_dictionary_data")]
public class SysDictionaryData
{
    [Key]
    public long ItemId { get; set; }

    public long DictTypeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DictCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ItemLabel { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ItemValue { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? TagType { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    public bool Enabled { get; set; } = true;

    public int Sort { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    public SysDictionaryType? DictType { get; init; }
}
