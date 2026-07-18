using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.DbProviders;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Esb;
using Microsoft.EntityFrameworkCore;

namespace DTSoft.AppService.Esb;

/// <summary>
/// ESB 数据源配置与执行服务。
/// </summary>
public class EsbDataSourceApp(SysDbContext context, EsbServiceConnectionApp connectionApp)
{
    private const string SourceTypeSql = "sql";
    private const string ExecuteModeQuery = "query";
    private static readonly Regex VariablePattern = new(@"\$\{\s*(currentUser|loginUser|user)\.(account|userAcc|name|displayName|email)\s*\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlParameterPattern = new(@"(?<!@)@([a-zA-Z][a-zA-Z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex UnsafeSqlKeywordPattern = new(
        @"\b(insert|update|delete|merge|drop|alter|create|truncate|exec|execute|grant|revoke|into|call|copy|replace|load|set|use|backup|restore)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<(List<EsbDataSourceResponse> Data, int Total)> GetDataSources(EsbDataSourceQueryParameter parameter)
    {
        var query = context.SysEsbDataSource!.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameter.Keyword))
        {
            query = query.Where(item => item.Code.Contains(parameter.Keyword) || item.Name.Contains(parameter.Keyword));
        }

        if (!string.IsNullOrWhiteSpace(parameter.SourceType))
        {
            query = query.Where(item => item.SourceType == parameter.SourceType);
        }

        if (parameter.ConnectionId == 0)
        {
            query = query.Where(item => item.ConnectionId == null || item.ConnectionId == 0);
        }
        else if (parameter.ConnectionId > 0)
        {
            query = query.Where(item => item.ConnectionId == parameter.ConnectionId.Value);
        }

        if (parameter.Status.HasValue)
        {
            query = query.Where(item => item.Status == parameter.Status.Value);
        }

        var total = await query.CountAsync();
        IQueryable<SysEsbDataSource> dataQuery = query
            .OrderByDescending(item => item.UpdateTime)
            .ThenByDescending(item => item.CreateTime);

        if (parameter is { PageNum: > 0, PageSize: > 0 })
        {
            dataQuery = dataQuery
                .Skip((parameter.PageNum.Value - 1) * parameter.PageSize.Value)
                .Take(parameter.PageSize.Value);
        }

        var list = await dataQuery.ToListAsync();
        var connectionNames = await BuildConnectionNameMap(list.Select(item => item.ConnectionId));
        return (list.Select(item => ToResponse(item, ResolveConnectionName(item.ConnectionId, connectionNames))).ToList(), total);
    }

    public async Task<EsbDataSourceResponse> GetDataSourceById(long id)
    {
        var entity = await context.SysEsbDataSource!.AsNoTracking().FirstOrDefaultAsync(item => item.ItemId == id);
        if (entity == null) throw new Exception("未找到指定的 ESB 数据源");
        var connectionNames = await BuildConnectionNameMap([entity.ConnectionId]);
        return ToResponse(entity, ResolveConnectionName(entity.ConnectionId, connectionNames));
    }

    public async Task<EsbDataSourceResponse> AddDataSource(EsbDataSourceAddParameter parameter)
    {
        NormalizeAndValidate(parameter);
        await ValidateConnection(parameter);

        var code = parameter.Code.Trim();
        var duplicated = await context.SysEsbDataSource!.AnyAsync(item => item.Code == code);
        if (duplicated) throw new Exception("数据源编码已存在");

        var now = DateTime.Now;
        var entity = new SysEsbDataSource
        {
            ItemId = YitterHelper.NewId(),
            Code = code,
            Name = parameter.Name.Trim(),
            ConnectionId = NormalizeConnectionId(parameter.ConnectionId),
            SourceType = NormalizeSourceType(parameter.SourceType),
            ExecuteMode = NormalizeExecuteMode(parameter.ExecuteMode),
            SqlText = NormalizeSql(parameter.SqlText),
            HttpConfig = parameter.HttpConfig,
            ParameterConfig = SerializeParameters(parameter.Parameters),
            ResultMapping = SerializeResultMapping(parameter.ResultMapping),
            Status = NormalizeStatus(parameter.Status),
            MaxRows = NormalizeMaxRows(parameter.MaxRows),
            TimeoutSeconds = NormalizeTimeoutSeconds(parameter.TimeoutSeconds),
            Remark = parameter.Remark,
            CreateTime = now,
            UpdateTime = now
        };

        context.SysEsbDataSource!.Add(entity);
        await context.SaveChangesAsync();
        var connectionNames = await BuildConnectionNameMap([entity.ConnectionId]);
        return ToResponse(entity, ResolveConnectionName(entity.ConnectionId, connectionNames));
    }

    public async Task<EsbDataSourceResponse> UpdateDataSource(EsbDataSourceUpdateParameter parameter)
    {
        NormalizeAndValidate(parameter);
        await ValidateConnection(parameter);

        var entity = await context.SysEsbDataSource!.FirstOrDefaultAsync(item => item.ItemId == parameter.ItemId);
        if (entity == null) throw new Exception("未找到指定的 ESB 数据源");

        var code = parameter.Code.Trim();
        var duplicated = await context.SysEsbDataSource!
            .AnyAsync(item => item.Code == code && item.ItemId != parameter.ItemId);
        if (duplicated) throw new Exception("数据源编码已存在");

        entity.Code = code;
        entity.Name = parameter.Name.Trim();
        entity.ConnectionId = NormalizeConnectionId(parameter.ConnectionId);
        entity.SourceType = NormalizeSourceType(parameter.SourceType);
        entity.ExecuteMode = NormalizeExecuteMode(parameter.ExecuteMode);
        entity.SqlText = NormalizeSql(parameter.SqlText);
        entity.HttpConfig = parameter.HttpConfig;
        entity.ParameterConfig = SerializeParameters(parameter.Parameters);
        entity.ResultMapping = SerializeResultMapping(parameter.ResultMapping);
        entity.Status = NormalizeStatus(parameter.Status);
        entity.MaxRows = NormalizeMaxRows(parameter.MaxRows);
        entity.TimeoutSeconds = NormalizeTimeoutSeconds(parameter.TimeoutSeconds);
        entity.Remark = parameter.Remark;
        entity.UpdateTime = DateTime.Now;

        await context.SaveChangesAsync();
        var connectionNames = await BuildConnectionNameMap([entity.ConnectionId]);
        return ToResponse(entity, ResolveConnectionName(entity.ConnectionId, connectionNames));
    }

    public async Task DeleteDataSource(long id)
    {
        var entity = await context.SysEsbDataSource!.FirstOrDefaultAsync(item => item.ItemId == id);
        if (entity == null) throw new Exception("未找到指定的 ESB 数据源");

        context.SysEsbDataSource!.Remove(entity);
        await context.SaveChangesAsync();
    }

    public async Task<object> Execute(EsbExecuteRequest request, string userAccount)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) throw new Exception("数据源编码不能为空");

        var entity = await context.SysEsbDataSource!
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Code == request.Code.Trim() && item.Status == 1);
        if (entity == null) throw new Exception("未找到启用的 ESB 数据源");

        if (!string.Equals(entity.SourceType, SourceTypeSql, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("当前版本仅支持 SQL 数据源");
        }

        if (!string.Equals(entity.ExecuteMode, ExecuteModeQuery, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("ESB 数据源仅支持查询模式");
        }

        return await ExecuteSqlQuery(
            entity,
            request.Parameters ?? new Dictionary<string, JsonNode?>(),
            userAccount,
            request.PageNum,
            request.PageSize);
    }

    private async Task<object> ExecuteSqlQuery(
        SysEsbDataSource entity,
        Dictionary<string, JsonNode?> inputParameters,
        string userAccount,
        int? pageNum,
        int? pageSize)
    {
        var sql = NormalizeSql(entity.SqlText);
        ValidateSafeQuerySql(sql);

        var declaredParameters = DeserializeParameters(entity.ParameterConfig);
        ValidateSqlParameters(sql, declaredParameters);
        var variableContext = await BuildVariableContext(userAccount);

        var serviceConnection = await connectionApp.GetEnabledConnection(entity.ConnectionId);
        var dbType = serviceConnection == null
            ? connectionApp.GetDefaultDbType()
            : EsbDbConnectionFactory.NormalizeDbType(serviceConnection.DbType);

        var connection = serviceConnection == null
            ? context.Database.GetDbConnection()
            : EsbDbConnectionFactory.CreateConnection(serviceConnection.DbType, serviceConnection.ConnectionString!);

        var ownsConnection = serviceConnection != null;
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            var parameterPrefix = EsbDbConnectionFactory.GetParameterPrefix(dbType);
            command.CommandText = ApplyProviderParameterPrefix(sql, parameterPrefix);
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

            var mappedRows = ApplyResultMapping(rows, DeserializeResultMapping(entity.ResultMapping));
            if (pageNum is > 0 && pageSize is > 0)
            {
                var normalizedPageNum = pageNum.Value;
                var normalizedPageSize = Math.Clamp(pageSize.Value, 1, 200);
                return new EsbPagedExecuteResponse
                {
                    List = mappedRows.Skip((normalizedPageNum - 1) * normalizedPageSize).Take(normalizedPageSize).ToList(),
                    Total = mappedRows.Count,
                    PageNum = normalizedPageNum,
                    PageSize = normalizedPageSize
                };
            }

            return mappedRows;
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

    private async Task<Dictionary<string, string>> BuildVariableContext(string userAccount)
    {
        var normalizedAccount = userAccount.Trim();
        var user = await context.SysUser!
            .AsNoTracking()
            .Where(item => item.Account == normalizedAccount)
            .Select(item => new
            {
                item.Account,
                item.DisplayName,
                item.Email
            })
            .FirstOrDefaultAsync();

        var account = user?.Account ?? normalizedAccount;
        var displayName = user?.DisplayName ?? string.Empty;
        var email = user?.Email ?? string.Empty;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["currentUser.account"] = account,
            ["currentUser.userAcc"] = account,
            ["currentUser.name"] = displayName,
            ["currentUser.displayName"] = displayName,
            ["currentUser.email"] = email,
            ["loginUser.account"] = account,
            ["loginUser.userAcc"] = account,
            ["loginUser.name"] = displayName,
            ["loginUser.displayName"] = displayName,
            ["loginUser.email"] = email,
            ["user.account"] = account,
            ["user.userAcc"] = account,
            ["user.name"] = displayName,
            ["user.displayName"] = displayName,
            ["user.email"] = email
        };
    }

    private static object ResolveParameterValue(EsbParameterConfig config, Dictionary<string, JsonNode?> inputParameters, Dictionary<string, string> variableContext)
    {
        inputParameters.TryGetValue(config.Name, out var valueNode);
        valueNode ??= config.DefaultValue;

        if (valueNode == null)
        {
            if (config.Required) throw new Exception($"参数 {config.Name} 不能为空");
            return DBNull.Value;
        }

        var text = ResolveVariables(ReadJsonNodeAsString(valueNode), variableContext);
        if (config.Required && string.IsNullOrWhiteSpace(text)) throw new Exception($"参数 {config.Name} 不能为空");

        return NormalizeParameterType(config.Type) switch
        {
            "number" => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw new Exception($"参数 {config.Name} 必须是数字"),
            "boolean" => bool.TryParse(text, out var boolean) ? boolean : throw new Exception($"参数 {config.Name} 必须是布尔值"),
            "datetime" => DateTime.TryParse(text, out var dateTime) ? dateTime : throw new Exception($"参数 {config.Name} 必须是日期时间"),
            _ => text
        };
    }

    private static string ResolveVariables(string value, Dictionary<string, string> variableContext)
    {
        if (string.IsNullOrEmpty(value)) return value;

        return VariablePattern.Replace(value, match =>
        {
            var key = $"{match.Groups[1].Value}.{match.Groups[2].Value}";
            return variableContext.TryGetValue(key, out var resolved) ? resolved : string.Empty;
        });
    }

    private static List<Dictionary<string, object?>> ApplyResultMapping(List<Dictionary<string, object?>> rows, EsbResultMapping? mapping)
    {
        var labelField = NormalizeMappingField(mapping?.LabelField);
        var valueField = NormalizeMappingField(mapping?.ValueField);

        if (labelField == null && valueField == null)
        {
            return rows;
        }

        foreach (var row in rows)
        {
            if (labelField != null)
            {
                row["Label"] = row.TryGetValue(labelField, out var label) ? label : null;
            }

            if (valueField != null)
            {
                row["Value"] = row.TryGetValue(valueField, out var value) ? value : null;
            }
        }

        return rows;
    }

    private static string? NormalizeMappingField(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task ValidateConnection(EsbDataSourceAddParameter parameter)
    {
        if (!string.Equals(parameter.SourceType, SourceTypeSql, StringComparison.OrdinalIgnoreCase)) return;

        var connection = await connectionApp.GetEnabledConnection(parameter.ConnectionId);
        if (connection != null && !string.Equals(connection.ServiceType, "database", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("SQL 数据源只能绑定数据库服务连接");
        }
    }

    private static string ApplyProviderParameterPrefix(string sql, string parameterPrefix)
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

    private static bool IsParameterStart(char value)
    {
        return value is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
    }

    private static string ReadJsonNodeAsString(JsonNode valueNode)
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

    private static void NormalizeAndValidate(EsbDataSourceAddParameter parameter)
    {
        parameter.SourceType = NormalizeSourceType(parameter.SourceType);
        parameter.ExecuteMode = NormalizeExecuteMode(parameter.ExecuteMode);

        if (!string.Equals(parameter.SourceType, SourceTypeSql, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("当前版本仅支持 SQL 数据源");
        }

        if (!string.Equals(parameter.ExecuteMode, ExecuteModeQuery, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("ESB 数据源仅支持查询模式");
        }

        var sql = NormalizeSql(parameter.SqlText);
        ValidateSafeQuerySql(sql);
        ValidateSqlParameters(sql, parameter.Parameters ?? []);
    }

    private static void ValidateSafeQuerySql(string sql)
    {
        var trimmed = sql.Trim();
        if (!trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("ESB 数据源仅允许 SELECT 查询");
        }

        if (trimmed.Contains(';'))
        {
            throw new Exception("ESB 数据源不允许多语句 SQL");
        }

        if (UnsafeSqlKeywordPattern.IsMatch(RemoveSqlStringLiterals(trimmed)))
        {
            throw new Exception("SQL 包含非查询操作或高风险关键字");
        }
    }

    private static void ValidateSqlParameters(string sql, List<EsbParameterConfig> parameters)
    {
        var declared = parameters.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var used = SqlParameterPattern.Matches(RemoveSqlStringLiterals(sql))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = used.Where(item => !declared.Contains(item)).ToList();
        if (missing.Count > 0)
        {
            throw new Exception($"SQL 参数未声明：{string.Join(", ", missing)}");
        }
    }

    private static string RemoveSqlStringLiterals(string sql)
    {
        return Regex.Replace(sql, @"'([^']|'')*'", "''");
    }

    private static string NormalizeSourceType(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? SourceTypeSql : value.Trim();
        return normalized.Equals(SourceTypeSql, StringComparison.OrdinalIgnoreCase) ? SourceTypeSql : normalized;
    }

    private static string NormalizeExecuteMode(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? ExecuteModeQuery : value.Trim();
        return normalized.Equals(ExecuteModeQuery, StringComparison.OrdinalIgnoreCase) ? ExecuteModeQuery : normalized;
    }

    private static string NormalizeSql(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new Exception("SQL 不能为空");
        return value.Trim();
    }

    private static string NormalizeParameterType(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "string" : value.Trim();
        return normalized is "number" or "boolean" or "datetime" ? normalized : "string";
    }

    private static int NormalizeStatus(int value) => value == 1 ? 1 : 0;

    private static long? NormalizeConnectionId(long? value) => value is null or <= 0 ? null : value.Value;

    private static int NormalizeMaxRows(int? value) => Math.Clamp(value ?? 500, 1, 1000);

    private static int NormalizeTimeoutSeconds(int? value) => Math.Clamp(value ?? 30, 1, 120);

    private static string SerializeParameters(List<EsbParameterConfig>? parameters)
    {
        return JsonSerializer.Serialize(parameters ?? []);
    }

    private static string? SerializeResultMapping(EsbResultMapping? mapping)
    {
        return mapping == null ? null : JsonSerializer.Serialize(mapping);
    }

    private static List<EsbParameterConfig> DeserializeParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<EsbParameterConfig>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static EsbResultMapping? DeserializeResultMapping(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<EsbResultMapping>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<long, string>> BuildConnectionNameMap(IEnumerable<long?> connectionIds)
    {
        var ids = connectionIds
            .Where(item => item.HasValue && item.Value > 0)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0) return new Dictionary<long, string>();

        return await context.SysEsbServiceConnection!
            .AsNoTracking()
            .Where(item => ids.Contains(item.ItemId))
            .ToDictionaryAsync(item => item.ItemId, item => item.Name);
    }

    private static string ResolveConnectionName(long? connectionId, Dictionary<long, string> connectionNames)
    {
        if (connectionId is null or 0) return "默认系统库";
        return connectionNames.TryGetValue(connectionId.Value, out var name) ? name : "已删除连接";
    }

    private static EsbDataSourceResponse ToResponse(SysEsbDataSource entity, string connectionName)
    {
        return new EsbDataSourceResponse
        {
            ItemId = entity.ItemId,
            Code = entity.Code,
            Name = entity.Name,
            ConnectionId = entity.ConnectionId,
            ConnectionName = connectionName,
            SourceType = entity.SourceType,
            ExecuteMode = entity.ExecuteMode,
            SqlText = entity.SqlText,
            HttpConfig = entity.HttpConfig,
            Parameters = DeserializeParameters(entity.ParameterConfig),
            ResultMapping = DeserializeResultMapping(entity.ResultMapping),
            Status = entity.Status,
            MaxRows = entity.MaxRows,
            TimeoutSeconds = entity.TimeoutSeconds,
            Remark = entity.Remark,
            CreateTime = entity.CreateTime,
            UpdateTime = entity.UpdateTime
        };
    }
}
