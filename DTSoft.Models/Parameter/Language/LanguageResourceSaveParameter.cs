namespace DTSoft.Models.Parameter.Language;

public class LanguageResourceSaveParameter
{
    public long? ItemId { get; set; }

    public string? ResourceKey { get; set; }

    public string? Module { get; set; }

    public string? Description { get; set; }

    public Dictionary<string, string?> Values { get; set; } = new();
}
