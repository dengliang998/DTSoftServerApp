using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DTSoft.Models.Parameter.Esb;

namespace DTSoft.AppService.Esb;

internal static class EsbTemplateRenderer
{
    private static readonly Regex VariablePattern = new(@"\$\{\s*(currentUser|loginUser|user)\.(account|userAcc|name|displayName|email)\s*\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TemplateParameterPattern = new(@"\{\{\s*([a-zA-Z][a-zA-Z0-9_]*(?:\.[a-zA-Z][a-zA-Z0-9_]*)?)\s*\}\}", RegexOptions.Compiled);

    public static string ResolveVariables(string value, Dictionary<string, string> variableContext)
    {
        if (string.IsNullOrEmpty(value)) return value;

        return VariablePattern.Replace(value, match =>
        {
            var key = $"{match.Groups[1].Value}.{match.Groups[2].Value}";
            return variableContext.TryGetValue(key, out var resolved) ? resolved : string.Empty;
        });
    }

    public static Dictionary<string, string> BuildTemplateContext(
        List<EsbParameterConfig> declaredParameters,
        Dictionary<string, JsonNode?> inputParameters,
        Dictionary<string, string> variableContext)
    {
        var context = new Dictionary<string, string>(variableContext, StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in declaredParameters)
        {
            inputParameters.TryGetValue(parameter.Name, out var valueNode);
            valueNode ??= parameter.DefaultValue;
            context[parameter.Name] = valueNode == null ? string.Empty : EsbJsonHelper.ReadJsonNodeAsString(valueNode);
        }

        foreach (var pair in inputParameters)
        {
            context[pair.Key] = pair.Value == null ? string.Empty : EsbJsonHelper.ReadJsonNodeAsString(pair.Value);
        }

        return context;
    }

    public static string RenderTemplate(string? value, Dictionary<string, string> templateContext)
    {
        var resolved = ResolveVariables(value ?? string.Empty, templateContext);
        return TemplateParameterPattern.Replace(resolved, match =>
            templateContext.TryGetValue(match.Groups[1].Value, out var parameterValue) ? parameterValue : string.Empty);
    }

    public static void ApplyHeaders(HttpRequestMessage request, Dictionary<string, string>? headers, Dictionary<string, string> templateContext)
    {
        foreach (var pair in headers ?? new Dictionary<string, string>())
        {
            var value = RenderTemplate(pair.Value, templateContext);
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(value)) continue;
            request.Headers.TryAddWithoutValidation(pair.Key, value);
        }
    }
}
