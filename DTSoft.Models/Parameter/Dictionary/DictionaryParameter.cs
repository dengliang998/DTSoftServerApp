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

    [Required(ErrorMessage = "validation.required")]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_:-]*$", ErrorMessage = "dictionary.codeInvalid")]
    public string DictCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [StringLength(100, ErrorMessage = "validation.stringLength")]
    public string DictName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "validation.stringLength")]
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

    [Required(ErrorMessage = "validation.required")]
    public string DictCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [StringLength(100, ErrorMessage = "validation.stringLength")]
    public string ItemLabel { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [StringLength(200, ErrorMessage = "validation.stringLength")]
    public string ItemValue { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "validation.stringLength")]
    public string? TagType { get; set; }

    [StringLength(500, ErrorMessage = "validation.stringLength")]
    public string? Remark { get; set; }

    public bool Enabled { get; set; } = true;

    public int Sort { get; set; }
}

public class DictionarySortItem
{
    public long ItemId { get; set; }

    public int Sort { get; set; }
}

public class DictionaryTypeSortRequest
{
    public List<DictionarySortItem> Items { get; set; } = [];
}

public class DictionaryItemSortRequest
{
    public string DictCode { get; set; } = string.Empty;

    public List<DictionarySortItem> Items { get; set; } = [];
}
