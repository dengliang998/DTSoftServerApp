using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_language")]
public class SysLanguage
{
    [Key]
    public long ItemId { get; set; }

    public string LanguageCode { get; set; } = string.Empty;

    public string LanguageName { get; set; } = string.Empty;

    public string NativeName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public bool IsDefault { get; set; }

    public int Sort { get; set; }
}
