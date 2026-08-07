namespace DTSoft.Models.Parameter.Language;

public class LanguageSaveParameter
{
    public long? ItemId { get; set; }

    public string? LanguageCode { get; set; }

    public string? LanguageName { get; set; }

    public string? NativeName { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsDefault { get; set; }

    public int Sort { get; set; }
}
