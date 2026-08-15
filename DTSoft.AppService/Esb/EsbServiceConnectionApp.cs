using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DTSoft.AppService.Localization;
using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.DbProviders;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Esb;
using Microsoft.EntityFrameworkCore;

namespace DTSoft.AppService.Esb;

/// <summary>
/// ESB 服务连接配置服务。
/// </summary>
public class EsbServiceConnectionApp(SysDbContext context, IAppLocalizer localizer)
{
    private const string ServiceTypeDatabase = "database";
    private const string ServiceTypeRestful = "restful";
    private string L(string key, params object[] args) => args.Length == 0 ? localizer[key] : localizer.Format(key, args);

    public async Task<(List<EsbServiceConnectionResponse> Data, int Total)> GetConnections(EsbServiceConnectionQueryParameter parameter)
    {
        var query = context.SysEsbServiceConnection!.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameter.Keyword))
        {
            query = query.Where(item => item.Code.Contains(parameter.Keyword) || item.Name.Contains(parameter.Keyword));
        }

        if (!string.IsNullOrWhiteSpace(parameter.ServiceType))
        {
            var serviceType = NormalizeServiceType(parameter.ServiceType);
            query = query.Where(item => item.ServiceType == serviceType);
        }

        if (parameter.Status.HasValue)
        {
            query = query.Where(item => item.Status == parameter.Status.Value);
        }

        var total = await query.CountAsync();
        IQueryable<SysEsbServiceConnection> dataQuery = query
            .OrderByDescending(item => item.UpdateTime)
            .ThenByDescending(item => item.CreateTime);

        if (parameter is { PageNum: > 0, PageSize: > 0 })
        {
            dataQuery = dataQuery
                .Skip((parameter.PageNum.Value - 1) * parameter.PageSize.Value)
                .Take(parameter.PageSize.Value);
        }

        var list = await dataQuery.ToListAsync();
        return (list.Select(ToResponse).ToList(), total);
    }

    public async Task<List<EsbServiceConnectionResponse>> GetConnectionOptions()
    {
        var options = new List<EsbServiceConnectionResponse> { BuildDefaultConnectionResponse() };
        var connections = await context.SysEsbServiceConnection!
            .AsNoTracking()
            .Where(item => item.Status == 1)
            .OrderBy(item => item.ServiceType)
            .ThenBy(item => item.Name)
            .ToListAsync();

        options.AddRange(connections.Select(ToResponse));
        return options;
    }

    public static List<string> GetSupportedDatabaseTypes()
    {
        return ["sqlserver", "mysql", "postgresql", "oracle"];
    }

    public async Task<EsbServiceConnectionResponse> AddConnection(EsbServiceConnectionAddParameter parameter)
    {
        NormalizeAndValidate(parameter);

        var itemId = YitterHelper.NewId();
        var code = NormalizeConnectionCode(parameter.Code, itemId);
        var duplicated = await context.SysEsbServiceConnection!.AnyAsync(item => item.Code == code);
        if (duplicated) throw new Exception(L("esb.connectionCodeExists"));

        var now = DateTime.Now;
        var entity = new SysEsbServiceConnection
        {
            ItemId = itemId,
            Code = code,
            Name = parameter.Name.Trim(),
            ServiceType = parameter.ServiceType,
            DbType = parameter.DbType,
            ConnectionString = parameter.ConnectionString,
            WebApiConfig = parameter.WebApiConfig,
            Status = NormalizeStatus(parameter.Status),
            TimeoutSeconds = NormalizeTimeoutSeconds(parameter.TimeoutSeconds),
            Remark = parameter.Remark,
            CreateTime = now,
            UpdateTime = now
        };

        context.SysEsbServiceConnection!.Add(entity);
        await context.SaveChangesAsync();
        return ToResponse(entity);
    }

    public async Task<EsbServiceConnectionResponse> UpdateConnection(EsbServiceConnectionUpdateParameter parameter)
    {
        NormalizeAndValidate(parameter);

        var entity = await context.SysEsbServiceConnection!.FirstOrDefaultAsync(item => item.ItemId == parameter.ItemId);
        if (entity == null) throw new Exception(L("esb.connectionNotFound"));

        var code = NormalizeConnectionCode(parameter.Code, entity.ItemId, entity.Code);
        if (!string.Equals(code, entity.Code, StringComparison.Ordinal))
        {
            var duplicated = await context.SysEsbServiceConnection!
                .AnyAsync(item => item.Code == code && item.ItemId != parameter.ItemId);
            if (duplicated) throw new Exception(L("esb.connectionCodeExists"));
        }

        entity.Code = code;
        entity.Name = parameter.Name.Trim();
        entity.ServiceType = parameter.ServiceType;
        entity.DbType = parameter.DbType;
        entity.ConnectionString = parameter.ConnectionString;
        entity.WebApiConfig = parameter.WebApiConfig;
        entity.Status = NormalizeStatus(parameter.Status);
        entity.TimeoutSeconds = NormalizeTimeoutSeconds(parameter.TimeoutSeconds);
        entity.Remark = parameter.Remark;
        entity.UpdateTime = DateTime.Now;

        await context.SaveChangesAsync();
        return ToResponse(entity);
    }

    public async Task DeleteConnection(long id)
    {
        var entity = await context.SysEsbServiceConnection!.FirstOrDefaultAsync(item => item.ItemId == id);
        if (entity == null) throw new Exception(L("esb.connectionNotFound"));

        var used = await context.SysEsbDataSource!.AnyAsync(item => item.ConnectionId == id);
        if (used) throw new Exception(L("esb.connectionInUse"));

        context.SysEsbServiceConnection!.Remove(entity);
        await context.SaveChangesAsync();
    }

    public async Task TestConnection(EsbServiceConnectionTestParameter parameter)
    {
        var serviceType = NormalizeServiceType(parameter.ServiceType);
        if (parameter.ItemId is null or <= 0)
        {
            if (serviceType == ServiceTypeRestful)
            {
                var webApiConfig = NormalizeWebApiConfig(parameter.WebApiConfig);
                if (string.IsNullOrWhiteSpace(webApiConfig)) throw new Exception(L("esb.webApiConfigRequired"));
                await TestRestfulConnection(webApiConfig, NormalizeTimeoutSeconds(parameter.TimeoutSeconds));
                return;
            }

            if (!string.IsNullOrWhiteSpace(parameter.DbType) || !string.IsNullOrWhiteSpace(parameter.ConnectionString))
            {
                var dbType = NormalizeRequiredDbType(parameter.DbType);
                var connectionString = NormalizeConnectionString(parameter.ConnectionString);
                await TestExternalConnection(dbType, connectionString, NormalizeTimeoutSeconds(parameter.TimeoutSeconds));
                return;
            }

            await TestDefaultConnection(parameter.TimeoutSeconds);
            return;
        }

        var entity = await context.SysEsbServiceConnection!
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ItemId == parameter.ItemId.Value);

        if (entity == null)
        {
            if (serviceType == ServiceTypeRestful)
            {
                var webApiConfig = NormalizeWebApiConfig(parameter.WebApiConfig);
                if (string.IsNullOrWhiteSpace(webApiConfig)) throw new Exception(L("esb.webApiConfigRequired"));
                await TestRestfulConnection(webApiConfig, NormalizeTimeoutSeconds(parameter.TimeoutSeconds));
                return;
            }

            var dbType = NormalizeDbType(parameter.DbType);
            var connectionString = NormalizeConnectionString(parameter.ConnectionString);
            await TestExternalConnection(dbType, connectionString, NormalizeTimeoutSeconds(parameter.TimeoutSeconds));
            return;
        }

        if (entity.ServiceType == ServiceTypeRestful)
        {
            if (string.IsNullOrWhiteSpace(entity.WebApiConfig)) throw new Exception(L("esb.webApiConfigRequired"));
            await TestRestfulConnection(entity.WebApiConfig, entity.TimeoutSeconds);
            return;
        }

        if (entity.ServiceType != ServiceTypeDatabase)
        {
            throw new Exception(L("esb.onlyDatabaseTestSupported"));
        }

        await TestExternalConnection(entity.DbType, entity.ConnectionString, entity.TimeoutSeconds);
    }

    private async Task TestRestfulConnection(string config, int timeoutSeconds)
    {
        var configObject = ValidateWebApiConfig(config);
        var baseUrl = configObject["BaseUrl"]?.ToString() ?? configObject["baseUrl"]?.ToString() ?? string.Empty;
        var authType = configObject["AuthType"]?.ToString() ?? configObject["authType"]?.ToString() ?? "none";

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };

        if (string.Equals(authType, "bearer", StringComparison.OrdinalIgnoreCase))
        {
            await TestBearerToken(configObject, baseUrl, client);
            return;
        }

        using var response = await client.GetAsync(baseUrl);
    }

    private async Task TestBearerToken(JsonObject configObject, string baseUrl, HttpClient client)
    {
        var tokenUrl = configObject["TokenUrl"]?.ToString() ?? configObject["tokenUrl"]?.ToString();
        if (string.IsNullOrWhiteSpace(tokenUrl)) throw new Exception(L("esb.webApiTokenUrlRequired"));

        var tokenUri = Uri.TryCreate(tokenUrl, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(new Uri(baseUrl.Trim().TrimEnd('/') + "/"), tokenUrl.TrimStart('/'));
        var tokenMethod = configObject["TokenMethod"]?.ToString() ?? configObject["tokenMethod"]?.ToString() ?? "POST";
        using var request = new HttpRequestMessage(CreateHttpMethod(tokenMethod), tokenUri);

        ApplyHeaders(request, (configObject["TokenHeaders"] as JsonObject) ?? (configObject["tokenHeaders"] as JsonObject));
        var body = configObject["TokenBody"]?.ToString() ?? configObject["tokenBody"]?.ToString();
        if (!string.IsNullOrWhiteSpace(body) && request.Method != HttpMethod.Get)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(L("esb.webApiTokenRequestFailed", (int)response.StatusCode, response.ReasonPhrase ?? string.Empty));
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(responseText);
        }
        catch (JsonException)
        {
            throw new Exception(L("esb.webApiInvalidJson"));
        }

        var tokenPath = configObject["TokenPath"]?.ToString() ?? configObject["tokenPath"]?.ToString() ?? "$.access_token";
        var token = SelectJsonPath(root, tokenPath)?.ToString();
        if (string.IsNullOrWhiteSpace(token)) throw new Exception(L("esb.webApiTokenNotFound"));
    }

    private static void ApplyHeaders(HttpRequestMessage request, JsonObject? headers)
    {
        if (headers == null) return;
        foreach (var header in headers)
        {
            var value = header.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(header.Key) && !string.IsNullOrWhiteSpace(value))
            {
                request.Headers.TryAddWithoutValidation(header.Key, value);
            }
        }
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

    private static JsonNode? SelectJsonPath(JsonNode? root, string? path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path) || path.Trim() == "$") return root;
        var segments = path.Trim().TrimStart('$').TrimStart('.').Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        foreach (var rawSegment in segments)
        {
            if (current is not JsonObject obj) return null;
            var segment = rawSegment.Trim();
            current = obj.FirstOrDefault(item => string.Equals(item.Key, segment, StringComparison.OrdinalIgnoreCase)).Value;
        }

        return current;
    }

    public async Task<SysEsbServiceConnection?> GetEnabledConnection(long? connectionId)
    {
        if (connectionId is null or 0) return null;

        var entity = await context.SysEsbServiceConnection!
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ItemId == connectionId.Value && item.Status == 1);
        if (entity == null) throw new Exception(L("esb.enabledConnectionNotFound"));

        return entity;
    }

    public string GetDefaultDbType()
    {
        return EsbDbConnectionFactory.NormalizeDbType(context.Database.ProviderName ?? "sqlserver", localizer);
    }

    private async Task TestDefaultConnection(int? timeoutSeconds)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = EsbDbConnectionFactory.GetTestQuery(GetDefaultDbType(), localizer);
            command.CommandTimeout = NormalizeTimeoutSeconds(timeoutSeconds);
            await command.ExecuteScalarAsync();
        }
        finally
        {
            if (shouldClose && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task TestExternalConnection(string? dbType, string? connectionString, int timeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new Exception(L("esb.connectionStringRequired"));

        await using var connection = EsbDbConnectionFactory.CreateConnection(dbType, connectionString, localizer);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = EsbDbConnectionFactory.GetTestQuery(dbType, localizer);
        command.CommandTimeout = timeoutSeconds;
        await command.ExecuteScalarAsync();
    }

    private EsbServiceConnectionResponse BuildDefaultConnectionResponse()
    {
        return new EsbServiceConnectionResponse
        {
            ItemId = 0,
            Code = "default",
            Name = L("esb.defaultSystemDb"),
            ServiceType = ServiceTypeDatabase,
            DbType = GetDefaultDbType(),
            ConnectionString = null,
            WebApiConfig = null,
            Status = 1,
            TimeoutSeconds = 30,
            Remark = L("esb.systemDbRemark"),
            IsDefault = true
        };
    }

    private void NormalizeAndValidate(EsbServiceConnectionAddParameter parameter)
    {
        parameter.ServiceType = NormalizeServiceType(parameter.ServiceType);
        parameter.DbType = NormalizeDbType(parameter.DbType);
        parameter.ConnectionString = NormalizeConnectionString(parameter.ConnectionString);
        parameter.WebApiConfig = NormalizeWebApiConfig(parameter.WebApiConfig);

        if (parameter.ServiceType == ServiceTypeDatabase)
        {
            parameter.DbType = NormalizeRequiredDbType(parameter.DbType);
            if (string.IsNullOrWhiteSpace(parameter.ConnectionString)) throw new Exception(L("esb.connectionStringRequired"));
            return;
        }

        if (parameter.ServiceType == ServiceTypeRestful)
        {
            if (string.IsNullOrWhiteSpace(parameter.WebApiConfig)) throw new Exception(L("esb.webApiConfigRequired"));
            _ = ValidateWebApiConfig(parameter.WebApiConfig);
            return;
        }

        throw new Exception(L("esb.serviceTypeUnsupported"));
    }

    private string NormalizeServiceType(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? ServiceTypeDatabase : value.Trim().ToLowerInvariant();
        return normalized is ServiceTypeDatabase or ServiceTypeRestful ? normalized : throw new Exception(L("esb.serviceTypeUnsupported"));
    }

    private string? NormalizeDbType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return EsbDbConnectionFactory.NormalizeDbType(value, localizer);
    }

    private string NormalizeRequiredDbType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new Exception(L("esb.dbTypeRequired"));
        return EsbDbConnectionFactory.NormalizeDbType(value, localizer);
    }

    private static string NormalizeConnectionCode(string? value, long itemId, string? fallback = null)
    {
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        if (!string.IsNullOrWhiteSpace(fallback)) return fallback.Trim();
        return $"conn_{itemId}";
    }

    private static string? NormalizeConnectionString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeWebApiConfig(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private JsonObject ValidateWebApiConfig(string config)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(config);
        }
        catch (JsonException)
        {
            throw new Exception(L("esb.webApiConfigInvalid"));
        }

        if (node is not JsonObject configObject)
        {
            throw new Exception(L("esb.webApiConfigInvalid"));
        }

        var baseUrl = configObject["BaseUrl"]?.ToString() ?? configObject["baseUrl"]?.ToString();
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            throw new Exception(L("esb.webApiBaseUrlRequired"));
        }

        var authType = configObject["AuthType"]?.ToString() ?? configObject["authType"]?.ToString();
        if (string.Equals(authType, "bearer", StringComparison.OrdinalIgnoreCase))
        {
            var tokenUrl = configObject["TokenUrl"]?.ToString() ?? configObject["tokenUrl"]?.ToString();
            if (string.IsNullOrWhiteSpace(tokenUrl))
            {
                throw new Exception(L("esb.webApiTokenUrlRequired"));
            }
        }

        return configObject;
    }

    private static int NormalizeStatus(int value) => value == 1 ? 1 : 0;

    private static int NormalizeTimeoutSeconds(int? value) => Math.Clamp(value ?? 30, 1, 120);

    private static EsbServiceConnectionResponse ToResponse(SysEsbServiceConnection entity)
    {
        return new EsbServiceConnectionResponse
        {
            ItemId = entity.ItemId,
            Code = entity.Code,
            Name = entity.Name,
            ServiceType = entity.ServiceType,
            DbType = entity.DbType,
            ConnectionString = entity.ConnectionString,
            WebApiConfig = entity.WebApiConfig,
            Status = entity.Status,
            TimeoutSeconds = entity.TimeoutSeconds,
            Remark = entity.Remark,
            IsDefault = false,
            CreateTime = entity.CreateTime,
            UpdateTime = entity.UpdateTime
        };
    }
}
