using DTSoft.AppService.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DTSoftServerApp.Helpers;

public static class ModelStateLocalizationHelper
{
    public static IEnumerable<string> GetLocalizedErrors(ModelStateDictionary modelState, IAppLocalizer localizer)
    {
        return modelState.Values
            .SelectMany(v => v.Errors)
            .Select(error => Translate(localizer, error.ErrorMessage))
            .Where(message => !string.IsNullOrWhiteSpace(message));
    }

    public static string Translate(IAppLocalizer localizer, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return localizer["common.argumentMissing"];

        if (IsResourceKey(message))
            return localizer[message];

        return message;
    }

    private static bool IsResourceKey(string message)
    {
        return message.Contains('.') && message.IndexOf(' ') < 0;
    }
}
