using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DTSoft.AppService.Localization;
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
public class EsbDataSourceApp(SysDbContext context, EsbServiceConnectionApp connectionApp, IAppLocalizer localizer)
{
    private const string SourceTypeSql = "sql";
    private const string SourceTypeRestful = "restful";
    private const string ExecuteModeQuery = "query";
    private static readonly ConcurrentDictionary<string, EsbCachedToken> TokenCache = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> TokenLocks = new();
    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly Regex VariablePattern = new(@"\$\{\s*(currentUser|loginUser|user)\.(account|userAcc|name|displayName|email)\s*\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TemplateParameterPattern = new(@"\{\{\s*([a-zA-Z][a-zA-Z0-9_]*(?:\.[a-zA-Z][a-zA-Z0-9_]*)?)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex SqlParameterPattern = new(@"(?<!@)@([a-zA-Z][a-zA-Z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex UnsafeSqlKeywordPattern = new(
        @"\b(insert|update|delete|merge|drop|alter|create|truncate|exec|execute|grant|revoke|into|call|copy|replace|load|set|use|backup|restore)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private string L(string key, params object[] args) => args.Length == 0 ? localizer[key] : localizer.Format(key, args);

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
        if (entity == null) throw new Exception(L("esb.dataSourceNotFound"));
        var connectionNames = await BuildConnectionNameMap([entity.ConnectionId]);
        return ToResponse(entity, ResolveConnectionName(entity.ConnectionId, connectionNames));
    }

    public async Task<EsbDataSourceResponse> AddDataSource(EsbDataSourceAddParameter parameter)
    {
        NormalizeAndValidate(parameter);
        await ValidateConnection(parameter);

        var code = parameter.Code.Trim();
        var duplicated = await context.SysEsbDataSource!.AnyAsync(item => item.Code == code);
        if (duplicated) throw new Exception(L("esb.dataSourceCodeExists"));

        var now = DateTime.Now;
        var entity = new SysEsbDataSource
        {
            ItemId = YitterHelper.NewId(),
            Code = code,
            Name = parameter.Name.Trim(),
            ConnectionId = NormalizeConnectionId(parameter.ConnectionId),
            SourceType = NormalizeSourceType(parameter.SourceType),
            ExecuteMode = NormalizeExecuteMode(parameter.ExecuteMode),
            SqlText = parameter.SourceType == SourceTypeSql ? NormalizeSql(parameter.SqlText) : null,
            HttpConfig = parameter.SourceType == SourceTypeRestful ? NormalizeHttpConfig(parameter.HttpConfig) : null,
            ParameterConfig = SerializeParameters(parameter.Parameters),
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
        if (entity == null) throw new Exception(L("esb.dataSourceNotFound"));

        var code = parameter.Code.Trim();
        var duplicated = await context.SysEsbDataSource!
            .AnyAsync(item => item.Code == code && item.ItemId != parameter.ItemId);
        if (duplicated) throw new Exception(L("esb.dataSourceCodeExists"));

        entity.Code = code;
        entity.Name = parameter.Name.Trim();
        entity.ConnectionId = NormalizeConnectionId(parameter.ConnectionId);
        entity.SourceType = NormalizeSourceType(parameter.SourceType);
        entity.ExecuteMode = NormalizeExecuteMode(parameter.ExecuteMode);
        entity.SqlText = parameter.SourceType == SourceTypeSql ? NormalizeSql(parameter.SqlText) : null;
        entity.HttpConfig = parameter.SourceType == SourceTypeRestful ? NormalizeHttpConfig(parameter.HttpConfig) : null;
        entity.ParameterConfig = SerializeParameters(parameter.Parameters);
        entity.ResultMapping = null;
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
        if (entity == null) throw new Exception(L("esb.dataSourceNotFound"));

        context.SysEsbDataSource!.Remove(entity);
        await context.SaveChangesAsync();
    }

    public async Task<object> Execute(EsbExecuteRequest request, string userAccount)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) throw new Exception(L("esb.dataSourceCodeRequired"));

        var entity = await context.SysEsbDataSource!
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Code == request.Code.Trim() && item.Status == 1);
        if (entity == null) throw new Exception(L("esb.dataSourceEnabledNotFound"));

        if (!string.Equals(entity.ExecuteMode, ExecuteModeQuery, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(L("esb.queryModeOnly"));
        }

        if (string.Equals(entity.SourceType, SourceTypeRestful, StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteWebApiQuery(
                entity,
                request.Parameters ?? new Dictionary<string, JsonNode?>(),
                userAccount,
                request.PageNum,
                request.PageSize);
        }

        if (!string.Equals(entity.SourceType, SourceTypeSql, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(L("esb.sourceTypeUnsupported"));
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
            : EsbDbConnectionFactory.NormalizeDbType(serviceConnection.DbType, localizer);

        var connection = serviceConnection == null
            ? context.Database.GetDbConnection()
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

    private async Task<object> ExecuteWebApiQuery(
        SysEsbDataSource entity,
        Dictionary<string, JsonNode?> inputParameters,
        string userAccount,
        int? pageNum,
        int? pageSize)
    {
        var serviceConnection = await connectionApp.GetEnabledConnection(entity.ConnectionId);
        if (serviceConnection == null || !string.Equals(serviceConnection.ServiceType, SourceTypeRestful, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(L("esb.webApiRequiresWebApiConnection"));
        }

        var connectionConfig = DeserializeWebApiConnectionConfig(serviceConnection.WebApiConfig);
        var requestConfig = DeserializeWebApiRequestConfig(entity.HttpConfig);
        ValidateWebApiConfig(connectionConfig, requestConfig);

        var declaredParameters = DeserializeParameters(entity.ParameterConfig);
        var variableContext = await BuildVariableContext(userAccount);
        var templateContext = BuildTemplateContext(declaredParameters, inputParameters, variableContext);
        var requestUri = BuildWebApiRequestUri(connectionConfig, requestConfig, templateContext);

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(NormalizeTimeoutSeconds(entity.TimeoutSeconds));

        using var request = new HttpRequestMessage(CreateHttpMethod(requestConfig.Method), requestUri);
        ApplyHeaders(request, connectionConfig.Headers, templateContext);
        ApplyHeaders(request, requestConfig.Headers, templateContext);
        await ApplyAuthentication(request, connectionConfig, templateContext, requestUri, client);

        var body = RenderTemplate(requestConfig.Body, templateContext);
        if (!string.IsNullOrWhiteSpace(body) && request.Method != HttpMethod.Get)
        {
            request.Content = new StringContent(body, Encoding.UTF8, string.IsNullOrWhiteSpace(requestConfig.ContentType) ? "application/json" : requestConfig.ContentType.Trim());
        }

        using var response = await client.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(L("esb.webApiRequestFailed", (int)response.StatusCode, response.ReasonPhrase ?? string.Empty));
        }

        var root = ParseJsonResponse(responseText);
        var dataNode = SelectJsonPath(root, requestConfig.ResultPath) ?? root;
        var rows = ConvertJsonNodeToRows(dataNode, NormalizeMaxRows(entity.MaxRows));

        if (pageNum is > 0 && pageSize is > 0)
        {
            var normalizedPageNum = pageNum.Value;
            var normalizedPageSize = Math.Clamp(pageSize.Value, 1, 200);
            var total = ReadJsonPathAsInt(root, requestConfig.TotalPath) ?? rows.Count;
            return new EsbPagedExecuteResponse
            {
                List = rows.Skip((normalizedPageNum - 1) * normalizedPageSize).Take(normalizedPageSize).ToList(),
                Total = total,
                PageNum = normalizedPageNum,
                PageSize = normalizedPageSize
            };
        }

        return rows;
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

    private object ResolveParameterValue(EsbParameterConfig config, Dictionary<string, JsonNode?> inputParameters, Dictionary<string, string> variableContext)
    {
        inputParameters.TryGetValue(config.Name, out var valueNode);
        valueNode ??= config.DefaultValue;

        if (valueNode == null)
        {
            if (config.Required) throw new Exception(L("esb.parameterRequired", config.Name));
            return DBNull.Value;
        }

        var text = ResolveVariables(ReadJsonNodeAsString(valueNode), variableContext);
        if (config.Required && string.IsNullOrWhiteSpace(text)) throw new Exception(L("esb.parameterRequired", config.Name));

        return NormalizeParameterType(config.Type) switch
        {
            "number" => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw new Exception(L("esb.parameterNumber", config.Name)),
            "boolean" => bool.TryParse(text, out var boolean) ? boolean : throw new Exception(L("esb.parameterBoolean", config.Name)),
            "datetime" => DateTime.TryParse(text, out var dateTime) ? dateTime : throw new Exception(L("esb.parameterDateTime", config.Name)),
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

    private async Task ValidateConnection(EsbDataSourceAddParameter parameter)
    {
        var connection = await connectionApp.GetEnabledConnection(parameter.ConnectionId);
        if (string.Equals(parameter.SourceType, SourceTypeSql, StringComparison.OrdinalIgnoreCase))
        {
            if (connection != null && !string.Equals(connection.ServiceType, "database", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(L("esb.sqlRequiresDatabaseConnection"));
            }

            return;
        }

        if (string.Equals(parameter.SourceType, SourceTypeRestful, StringComparison.OrdinalIgnoreCase))
        {
            if (connection == null || !string.Equals(connection.ServiceType, SourceTypeRestful, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(L("esb.webApiRequiresWebApiConnection"));
            }
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

    private void NormalizeAndValidate(EsbDataSourceAddParameter parameter)
    {
        parameter.SourceType = NormalizeSourceType(parameter.SourceType);
        parameter.ExecuteMode = NormalizeExecuteMode(parameter.ExecuteMode);

        if (!string.Equals(parameter.ExecuteMode, ExecuteModeQuery, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(L("esb.queryModeOnly"));
        }

        if (string.Equals(parameter.SourceType, SourceTypeSql, StringComparison.OrdinalIgnoreCase))
        {
            var sql = NormalizeSql(parameter.SqlText);
            ValidateSafeQuerySql(sql);
            ValidateSqlParameters(sql, parameter.Parameters ?? []);
            return;
        }

        if (string.Equals(parameter.SourceType, SourceTypeRestful, StringComparison.OrdinalIgnoreCase))
        {
            var requestConfig = DeserializeWebApiRequestConfig(parameter.HttpConfig);
            ValidateWebApiRequestConfig(requestConfig);
            return;
        }

        throw new Exception(L("esb.sourceTypeUnsupported"));
    }

    private void ValidateSafeQuerySql(string sql)
    {
        var trimmed = sql.Trim();
        if (!trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(L("esb.selectOnly"));
        }

        if (trimmed.Contains(';'))
        {
            throw new Exception(L("esb.multiStatementNotAllowed"));
        }

        if (UnsafeSqlKeywordPattern.IsMatch(RemoveSqlStringLiterals(trimmed)))
        {
            throw new Exception(L("esb.sqlUnsafe"));
        }
    }

    private void ValidateSqlParameters(string sql, List<EsbParameterConfig> parameters)
    {
        var declared = parameters.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var used = SqlParameterPattern.Matches(RemoveSqlStringLiterals(sql))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = used.Where(item => !declared.Contains(item)).ToList();
        if (missing.Count > 0)
        {
            throw new Exception(L("esb.sqlParameterUndeclared", string.Join(", ", missing)));
        }
    }

    private static string RemoveSqlStringLiterals(string sql)
    {
        return Regex.Replace(sql, @"'([^']|'')*'", "''");
    }

    private static string NormalizeSourceType(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? SourceTypeSql : value.Trim().ToLowerInvariant();
        return normalized is SourceTypeSql or SourceTypeRestful ? normalized : normalized;
    }

    private static string NormalizeExecuteMode(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? ExecuteModeQuery : value.Trim();
        return normalized.Equals(ExecuteModeQuery, StringComparison.OrdinalIgnoreCase) ? ExecuteModeQuery : normalized;
    }

    private string NormalizeSql(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new Exception(L("esb.sqlRequired"));
        return value.Trim();
    }

    private string NormalizeHttpConfig(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new Exception(L("esb.webApiConfigRequired"));
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

    private EsbWebApiConnectionConfig DeserializeWebApiConnectionConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new EsbWebApiConnectionConfig();
        try
        {
            return JsonSerializer.Deserialize<EsbWebApiConnectionConfig>(json, CaseInsensitiveJsonOptions) ?? new EsbWebApiConnectionConfig();
        }
        catch (JsonException)
        {
            throw new Exception(L("esb.webApiConfigInvalid"));
        }
    }

    private EsbWebApiRequestConfig DeserializeWebApiRequestConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new EsbWebApiRequestConfig();
        try
        {
            return JsonSerializer.Deserialize<EsbWebApiRequestConfig>(json, CaseInsensitiveJsonOptions) ?? new EsbWebApiRequestConfig();
        }
        catch (JsonException)
        {
            throw new Exception(L("esb.webApiConfigInvalid"));
        }
    }

    private void ValidateWebApiConfig(EsbWebApiConnectionConfig connectionConfig, EsbWebApiRequestConfig requestConfig)
    {
        if (string.IsNullOrWhiteSpace(connectionConfig.BaseUrl)) throw new Exception(L("esb.webApiBaseUrlRequired"));
        if (!Uri.TryCreate(connectionConfig.BaseUrl, UriKind.Absolute, out _)) throw new Exception(L("esb.webApiBaseUrlRequired"));
        ValidateWebApiRequestConfig(requestConfig);
    }

    private void ValidateWebApiRequestConfig(EsbWebApiRequestConfig requestConfig)
    {
        if (string.IsNullOrWhiteSpace(requestConfig.Path)) throw new Exception(L("esb.webApiPathRequired"));
        _ = CreateHttpMethod(requestConfig.Method);
    }

    private HttpMethod CreateHttpMethod(string? method)
    {
        var normalized = string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();
        return normalized switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            _ => throw new Exception(L("esb.webApiMethodUnsupported"))
        };
    }

    private static Dictionary<string, string> BuildTemplateContext(
        List<EsbParameterConfig> declaredParameters,
        Dictionary<string, JsonNode?> inputParameters,
        Dictionary<string, string> variableContext)
    {
        var context = new Dictionary<string, string>(variableContext, StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in declaredParameters)
        {
            inputParameters.TryGetValue(parameter.Name, out var valueNode);
            valueNode ??= parameter.DefaultValue;
            context[parameter.Name] = valueNode == null ? string.Empty : ReadJsonNodeAsString(valueNode);
        }

        foreach (var pair in inputParameters)
        {
            context[pair.Key] = pair.Value == null ? string.Empty : ReadJsonNodeAsString(pair.Value);
        }

        return context;
    }

    private static string RenderTemplate(string? value, Dictionary<string, string> templateContext)
    {
        var resolved = ResolveVariables(value ?? string.Empty, templateContext);
        return TemplateParameterPattern.Replace(resolved, match =>
            templateContext.TryGetValue(match.Groups[1].Value, out var parameterValue) ? parameterValue : string.Empty);
    }

    private static Uri BuildWebApiRequestUri(
        EsbWebApiConnectionConfig connectionConfig,
        EsbWebApiRequestConfig requestConfig,
        Dictionary<string, string> templateContext)
    {
        var baseUri = new Uri(connectionConfig.BaseUrl!.Trim().TrimEnd('/') + "/");
        var path = RenderTemplate(requestConfig.Path, templateContext).TrimStart('/');
        var builder = new UriBuilder(new Uri(baseUri, path));
        var queryItems = new List<string>();
        if (!string.IsNullOrWhiteSpace(builder.Query))
        {
            queryItems.Add(builder.Query.TrimStart('?'));
        }

        foreach (var pair in requestConfig.Query ?? new Dictionary<string, string>())
        {
            var value = RenderTemplate(pair.Value, templateContext);
            if (string.IsNullOrEmpty(value)) continue;
            queryItems.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(value)}");
        }

        builder.Query = string.Join("&", queryItems.Where(item => !string.IsNullOrWhiteSpace(item)));
        return builder.Uri;
    }

    private static void ApplyHeaders(HttpRequestMessage request, Dictionary<string, string>? headers, Dictionary<string, string> templateContext)
    {
        foreach (var pair in headers ?? new Dictionary<string, string>())
        {
            var value = RenderTemplate(pair.Value, templateContext);
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(value)) continue;
            request.Headers.TryAddWithoutValidation(pair.Key, value);
        }
    }

    private async Task ApplyAuthentication(
        HttpRequestMessage request,
        EsbWebApiConnectionConfig connectionConfig,
        Dictionary<string, string> templateContext,
        Uri requestUri,
        HttpClient client)
    {
        var authType = (connectionConfig.AuthType ?? "none").Trim().ToLowerInvariant();
        if (authType == "bearer")
        {
            var token = await ResolveBearerToken(connectionConfig, templateContext, client);
            if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return;
        }

        if (authType != "apikey") return;

        var apiKeyName = RenderTemplate(connectionConfig.ApiKeyName, templateContext);
        var apiKeyValue = RenderTemplate(connectionConfig.ApiKeyValue, templateContext);
        if (string.IsNullOrWhiteSpace(apiKeyName) || string.IsNullOrWhiteSpace(apiKeyValue)) return;

        if (string.Equals(connectionConfig.ApiKeyIn, "query", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(requestUri);
            var prefix = string.IsNullOrWhiteSpace(builder.Query) ? string.Empty : builder.Query.TrimStart('?') + "&";
            builder.Query = prefix + $"{Uri.EscapeDataString(apiKeyName)}={Uri.EscapeDataString(apiKeyValue)}";
            request.RequestUri = builder.Uri;
            return;
        }

        request.Headers.TryAddWithoutValidation(apiKeyName, apiKeyValue);
    }

    private async Task<string> ResolveBearerToken(
        EsbWebApiConnectionConfig connectionConfig,
        Dictionary<string, string> templateContext,
        HttpClient client)
    {
        var tokenUrlText = RenderTemplate(connectionConfig.TokenUrl, templateContext);
        if (string.IsNullOrWhiteSpace(tokenUrlText)) throw new Exception(L("esb.webApiTokenUrlRequired"));

        var tokenUri = Uri.TryCreate(tokenUrlText, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(new Uri(connectionConfig.BaseUrl!.Trim().TrimEnd('/') + "/"), tokenUrlText.TrimStart('/'));
        var body = RenderTemplate(connectionConfig.TokenBody, templateContext);
        var cacheKey = BuildTokenCacheKey(connectionConfig, tokenUri, body);
        var refreshSkewSeconds = Math.Clamp(connectionConfig.TokenRefreshSkewSeconds ?? 60, 0, 3600);
        if (TokenCache.TryGetValue(cacheKey, out var cachedToken) &&
            cachedToken.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(refreshSkewSeconds))
        {
            return cachedToken.Token;
        }

        var tokenLock = TokenLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await tokenLock.WaitAsync();
        try
        {
            if (TokenCache.TryGetValue(cacheKey, out cachedToken) &&
                cachedToken.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(refreshSkewSeconds))
            {
                return cachedToken.Token;
            }

            using var tokenRequest = new HttpRequestMessage(CreateHttpMethod(connectionConfig.TokenMethod), tokenUri);
            ApplyHeaders(tokenRequest, connectionConfig.TokenHeaders, templateContext);

            if (!string.IsNullOrWhiteSpace(body) && tokenRequest.Method != HttpMethod.Get)
            {
                tokenRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(tokenRequest);
            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(L("esb.webApiTokenRequestFailed", (int)response.StatusCode, response.ReasonPhrase ?? string.Empty));
            }

            var root = ParseJsonResponse(responseText);
            var tokenNode = SelectJsonPath(root, connectionConfig.TokenPath) ?? SelectJsonPath(root, "$.access_token");
            var token = tokenNode == null ? null : ReadJsonNodeAsString(tokenNode);
            if (string.IsNullOrWhiteSpace(token)) throw new Exception(L("esb.webApiTokenNotFound"));

            TokenCache[cacheKey] = new EsbCachedToken(token, ResolveTokenExpiresAt(root, connectionConfig));
            return token;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private static string BuildTokenCacheKey(EsbWebApiConnectionConfig connectionConfig, Uri tokenUri, string body)
    {
        var rawKey = JsonSerializer.Serialize(new
        {
            connectionConfig.BaseUrl,
            connectionConfig.AuthType,
            TokenUrl = tokenUri.ToString(),
            connectionConfig.TokenMethod,
            connectionConfig.TokenHeaders,
            Body = body,
            connectionConfig.TokenPath,
            connectionConfig.TokenExpiresInPath,
            connectionConfig.TokenExpiresAtPath
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
    }

    private static DateTimeOffset ResolveTokenExpiresAt(JsonNode root, EsbWebApiConnectionConfig connectionConfig)
    {
        var expiresAtNode = SelectJsonPath(root, connectionConfig.TokenExpiresAtPath);
        if (expiresAtNode != null)
        {
            var expiresAtText = ReadJsonNodeAsString(expiresAtNode);
            if (DateTimeOffset.TryParse(expiresAtText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiresAt))
            {
                return expiresAt.ToUniversalTime();
            }
        }

        var expiresInNode = SelectJsonPath(root, connectionConfig.TokenExpiresInPath) ?? SelectJsonPath(root, "$.expires_in");
        if (expiresInNode != null)
        {
            var expiresInText = ReadJsonNodeAsString(expiresInNode);
            if (int.TryParse(expiresInText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresInSeconds) &&
                expiresInSeconds > 0)
            {
                return DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
            }
        }

        return DateTimeOffset.UtcNow.AddMinutes(5);
    }

    private JsonNode ParseJsonResponse(string responseText)
    {
        try
        {
            return JsonNode.Parse(responseText) ?? new JsonObject();
        }
        catch (JsonException)
        {
            throw new Exception(L("esb.webApiInvalidJson"));
        }
    }

    private static JsonNode? SelectJsonPath(JsonNode? root, string? path)
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

    private static int? ReadJsonPathAsInt(JsonNode root, string? path)
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

    private static List<Dictionary<string, object?>> ConvertJsonNodeToRows(JsonNode? node, int maxRows)
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

    private string ResolveConnectionName(long? connectionId, Dictionary<long, string> connectionNames)
    {
        if (connectionId is null or 0) return localizer["esb.defaultSystemDb"];
        return connectionNames.TryGetValue(connectionId.Value, out var name) ? name : localizer["esb.deletedConnection"];
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
            Status = entity.Status,
            MaxRows = entity.MaxRows,
            TimeoutSeconds = entity.TimeoutSeconds,
            Remark = entity.Remark,
            CreateTime = entity.CreateTime,
            UpdateTime = entity.UpdateTime
        };
    }

    private sealed class EsbWebApiConnectionConfig
    {
        public string? BaseUrl { get; set; }

        public string? AuthType { get; set; }

        public string? Token { get; set; }

        public string? TokenUrl { get; set; }

        public string? TokenMethod { get; set; }

        public Dictionary<string, string>? TokenHeaders { get; set; }

        public string? TokenBody { get; set; }

        public string? TokenPath { get; set; }

        public string? TokenExpiresInPath { get; set; }

        public string? TokenExpiresAtPath { get; set; }

        public int? TokenRefreshSkewSeconds { get; set; }

        public string? ApiKeyName { get; set; }

        public string? ApiKeyValue { get; set; }

        public string? ApiKeyIn { get; set; }

        public Dictionary<string, string>? Headers { get; set; }
    }

    private sealed class EsbWebApiRequestConfig
    {
        public string? Method { get; set; }

        public string? Path { get; set; }

        public Dictionary<string, string>? Query { get; set; }

        public Dictionary<string, string>? Headers { get; set; }

        public string? Body { get; set; }

        public string? ContentType { get; set; }

        public string? ResultPath { get; set; }

        public string? TotalPath { get; set; }
    }

    private sealed record EsbCachedToken(string Token, DateTimeOffset ExpiresAt);
}
