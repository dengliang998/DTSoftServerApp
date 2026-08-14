namespace DTSoft.Core.Localization;

public interface ITextLocalizer
{
    string this[string key] { get; }

    string Format(string key, params object[] args);
}
