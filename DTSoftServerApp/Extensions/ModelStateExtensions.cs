using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DTSoftServerApp.Extensions;

public static class ModelStateExtensions
{
    public static IEnumerable<string> GetErrorMessages(this ModelStateDictionary modelState)
    {
        return modelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message));
    }
}
