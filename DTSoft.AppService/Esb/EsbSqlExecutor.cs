using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json.Nodes;
using DTSoft.AppService.Localization;
using DTSoft.Core.Exceptions;
using DTSoft.Core.DbProviders;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Esb;

namespace DTSoft.AppService.Esb;

public class EsbSqlExecutor(IAppLocalizer localizer)
{
    private string L(string key, params object[] args) => args.Length == 0 ? localizer[key] : localizer.Format(key, args);
    private DtSoftException BadRequest(string key, params object[] args) => DtSoftException.BadRequest(L(key, args), key);

    public async Task<object> Execute(
        SysEsbDataSource entity,
        DbConnection defaultConnection,
        SysEsbServiceConnection? serviceConnection,
        string defaultDbType,
        List<EsbParameterConfig> declaredParameters,
        Dictionary<string, JsonNode?> inputParameters,
        Dictionary<string, string> variableContext,
        int? pageNum,
        int? pageSize)
    {
        var sql = NormalizeSql(entity.SqlText);
        EsbSqlSafety.ValidateSafeQuerySql(sql, localizer);
        EsbSqlSafety.ValidateSqlParameters(sql, declaredParameters, localizer);

        var dbType = serviceConnection == null
            ? defaultDbType
            : EsbDbConnectionFactory.NormalizeDbType(serviceConnection.DbType, localizer);

        var connection = serviceConnection == null
            ? defaultConnection
            : EsbDbConnectionFactory.CreateConnection(serviceConnection.DbType, serviceConnection.ConnectionString!, localizer);

        var ownsConnection = serviceConnection != null;
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            var parameterPrefix = EsbDbConnectionFactory.GetParameterPrefix(dbType, localizer);
            command.CommandText = EsbSqlSafety.ApplyProviderParameterPrefix(sql, parameterPrefix);
            command.CommandTimeout = NormalizeTimeoutSeconds(entity.TimeoutSeconds);

            foreach (var parameterConfig in declaredParameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"{parameterPrefix}{parameterConfig.Name}";
                parameter.Value = ResolveParameterValue(parameterConfig, inputParameters, variableContext);
                command.Parameters.Add(parameter);
            }

            var rows = new List<Dictionary<string, object?>>();
            var maxRows = NormalizeMaxRows(entity.MaxRows);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (rows.Count >= maxRows) break;

                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }

            if (pageNum is > 0 && pageSize is > 0)
            {
                var normalizedPageNum = pageNum.Value;
                var normalizedPageSize = Math.Clamp(pageSize.Value, 1, 200);
                return new EsbPagedExecuteResponse
                {
                    List = rows.Skip((normalizedPageNum - 1) * normalizedPageSize).Take(normalizedPageSize).ToList(),
                    Total = rows.Count,
                    PageNum = normalizedPageNum,
                    PageSize = normalizedPageSize
                };
            }

            return rows;
        }
        finally
        {
            if (shouldClose && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }

            if (ownsConnection)
            {
                await connection.DisposeAsync();
            }
        }
    }

    private object ResolveParameterValue(EsbParameterConfig config, Dictionary<string, JsonNode?> inputParameters, Dictionary<string, string> variableContext)
    {
        inputParameters.TryGetValue(config.Name, out var valueNode);
        valueNode ??= config.DefaultValue;

        if (valueNode == null)
        {
            if (config.Required) throw BadRequest("esb.parameterRequired", config.Name);
            return DBNull.Value;
        }

        var text = EsbTemplateRenderer.ResolveVariables(EsbJsonHelper.ReadJsonNodeAsString(valueNode), variableContext);
        if (config.Required && string.IsNullOrWhiteSpace(text)) throw BadRequest("esb.parameterRequired", config.Name);

        return NormalizeParameterType(config.Type) switch
        {
            "number" => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw BadRequest("esb.parameterNumber", config.Name),
            "boolean" => bool.TryParse(text, out var boolean) ? boolean : throw BadRequest("esb.parameterBoolean", config.Name),
            "datetime" => DateTime.TryParse(text, out var dateTime) ? dateTime : throw BadRequest("esb.parameterDateTime", config.Name),
            _ => text
        };
    }

    private string NormalizeSql(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw BadRequest("esb.sqlRequired");
        return value.Trim();
    }

    private static string NormalizeParameterType(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "string" : value.Trim();
        return normalized is "number" or "boolean" or "datetime" ? normalized : "string";
    }

    private static int NormalizeMaxRows(int? value) => Math.Clamp(value ?? 500, 1, 1000);

    private static int NormalizeTimeoutSeconds(int? value) => Math.Clamp(value ?? 30, 1, 120);
}
