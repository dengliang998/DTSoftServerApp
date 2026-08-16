using System.Globalization;
using System.Text.Json.Nodes;

namespace DTSoft.AppService.Esb;

internal static class EsbJsonHelper
{
    public static string ReadJsonNodeAsString(JsonNode valueNode)
    {
        if (valueNode is JsonValue value)
        {
            if (value.TryGetValue<string>(out var stringValue)) return stringValue;
            if (value.TryGetValue<decimal>(out var decimalValue)) return decimalValue.ToString(CultureInfo.InvariantCulture);
            if (value.TryGetValue<int>(out var intValue)) return intValue.ToString(CultureInfo.InvariantCulture);
            if (value.TryGetValue<long>(out var longValue)) return longValue.ToString(CultureInfo.InvariantCulture);
            if (value.TryGetValue<bool>(out var boolValue)) return boolValue.ToString();
            if (value.TryGetValue<DateTime>(out var dateTimeValue)) return dateTimeValue.ToString("O");
        }

        return valueNode.ToJsonString();
    }

    public static JsonNode? SelectJsonPath(JsonNode? root, string? path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path) || path.Trim() == "$") return root;
        var segments = path.Trim().TrimStart('$').TrimStart('.').Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        foreach (var rawSegment in segments)
        {
            var segment = rawSegment.Trim();
            if (current is JsonObject obj)
            {
                current = obj.FirstOrDefault(item => string.Equals(item.Key, segment, StringComparison.OrdinalIgnoreCase)).Value;
                continue;
            }

            if (current is JsonArray array && int.TryParse(segment.Trim('[', ']'), out var index) && index >= 0 && index < array.Count)
            {
                current = array[index];
                continue;
            }

            return null;
        }

        return current;
    }

    public static int? ReadJsonPathAsInt(JsonNode root, string? path)
    {
        var node = SelectJsonPath(root, path);
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue)) return intValue;
            if (value.TryGetValue<long>(out var longValue)) return (int)Math.Min(int.MaxValue, longValue);
            if (value.TryGetValue<string>(out var stringValue) && int.TryParse(stringValue, out var parsed)) return parsed;
        }

        return null;
    }

    public static List<Dictionary<string, object?>> ConvertJsonNodeToRows(JsonNode? node, int maxRows)
    {
        if (node is JsonArray array)
        {
            return array.Take(maxRows).Select(ConvertJsonNodeToRow).ToList();
        }

        return [ConvertJsonNodeToRow(node)];
    }

    private static Dictionary<string, object?> ConvertJsonNodeToRow(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            return obj.ToDictionary(item => item.Key, item => ConvertJsonValue(item.Value), StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Value"] = ConvertJsonValue(node)
        };
    }

    private static object? ConvertJsonValue(JsonNode? node)
    {
        if (node == null) return null;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var stringValue)) return stringValue;
            if (value.TryGetValue<int>(out var intValue)) return intValue;
            if (value.TryGetValue<long>(out var longValue)) return longValue;
            if (value.TryGetValue<decimal>(out var decimalValue)) return decimalValue;
            if (value.TryGetValue<double>(out var doubleValue)) return doubleValue;
            if (value.TryGetValue<bool>(out var boolValue)) return boolValue;
        }

        return node.ToJsonString();
    }
}
