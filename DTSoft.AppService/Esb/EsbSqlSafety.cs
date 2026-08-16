using System.Text;
using System.Text.RegularExpressions;
using DTSoft.AppService.Localization;
using DTSoft.Core.Exceptions;
using DTSoft.Models.Parameter.Esb;

namespace DTSoft.AppService.Esb;

internal static class EsbSqlSafety
{
    private static readonly Regex SqlParameterPattern = new(@"(?<!@)@([a-zA-Z][a-zA-Z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex UnsafeSqlKeywordPattern = new(
        @"\b(insert|update|delete|merge|drop|alter|create|truncate|exec|execute|grant|revoke|into|call|copy|replace|load|set|use|backup|restore)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void ValidateSafeQuerySql(string sql, IAppLocalizer localizer)
    {
        var trimmed = sql.Trim();
        if (!trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase))
        {
            throw DtSoftException.BadRequest(localizer["esb.selectOnly"], "esb.selectOnly");
        }

        if (trimmed.Contains(';'))
        {
            throw DtSoftException.BadRequest(localizer["esb.multiStatementNotAllowed"], "esb.multiStatementNotAllowed");
        }

        if (UnsafeSqlKeywordPattern.IsMatch(RemoveSqlStringLiterals(trimmed)))
        {
            throw DtSoftException.BadRequest(localizer["esb.sqlUnsafe"], "esb.sqlUnsafe");
        }
    }

    public static void ValidateSqlParameters(string sql, List<EsbParameterConfig> parameters, IAppLocalizer localizer)
    {
        var declared = parameters.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var used = SqlParameterPattern.Matches(RemoveSqlStringLiterals(sql))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = used.Where(item => !declared.Contains(item)).ToList();
        if (missing.Count > 0)
        {
            throw DtSoftException.BadRequest(
                localizer.Format("esb.sqlParameterUndeclared", string.Join(", ", missing)),
                "esb.sqlParameterUndeclared");
        }
    }

    public static string ApplyProviderParameterPrefix(string sql, string parameterPrefix)
    {
        if (parameterPrefix == "@") return sql;

        var result = new StringBuilder();
        var inString = false;
        for (var i = 0; i < sql.Length; i++)
        {
            var current = sql[i];
            if (current == '\'')
            {
                result.Append(current);
                if (inString && i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    result.Append(sql[++i]);
                    continue;
                }

                inString = !inString;
                continue;
            }

            if (!inString && current == '@' && i + 1 < sql.Length && IsParameterStart(sql[i + 1]))
            {
                result.Append(parameterPrefix);
                continue;
            }

            result.Append(current);
        }

        return result.ToString();
    }

    private static string RemoveSqlStringLiterals(string sql)
    {
        return Regex.Replace(sql, @"'([^']|'')*'", "''");
    }

    private static bool IsParameterStart(char value)
    {
        return value is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
    }
}
