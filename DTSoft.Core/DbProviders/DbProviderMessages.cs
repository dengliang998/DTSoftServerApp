using DTSoft.Core.Localization;

namespace DTSoft.Core.DbProviders;

internal static class DbProviderMessages
{
    internal static string Text(ITextLocalizer? localizer, string key, params object[] args)
    {
        if (localizer is null)
        {
            return args.Length == 0 ? key : string.Format(key, args);
        }

        return args.Length == 0 ? localizer[key] : localizer.Format(key, args);
    }
}
