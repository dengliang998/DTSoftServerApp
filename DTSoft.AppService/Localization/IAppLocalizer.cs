namespace DTSoft.AppService.Localization;

public interface IAppLocalizer
{
    string this[string key] { get; }

    string Format(string key, params object[] args);
}
