using System.ComponentModel.DataAnnotations;

namespace DTSoft.Models.Parameter.Dictionary;

public class DictionaryTypeQuery
{
    public string? Keyword { get; set; }

    public bool? Enabled { get; set; }
}

public class DictionaryTypeDto
{
    public long? ItemId { get; set; }

    [Required]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_:-]*$", ErrorMessage = "字典编码只能包含英文、数字、下划线、中划线和冒号，且以英文开头")]
    public string DictCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string DictName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool Enabled { get; set; } = true;

    public int Sort { get; set; }
}

public class DictionaryItemQuery
{
    public string DictCode { get; set; } = string.Empty;

    public string? Keyword { get; set; }

    public bool? Enabled { get; set; }
}

public class DictionaryItemDto
{
    public long? ItemId { get; set; }

    [Required]
    public string DictCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ItemLabel { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string ItemValue { get; set; } = string.Empty;

    [StringLength(50)]
    public string? TagType { get; set; }

    [StringLength(500)]
    public string? Remark { get; set; }

    public bool Enabled { get; set; } = true;

    public int Sort { get; set; }
}
