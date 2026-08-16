using System.Text.Json;
using System.Text.Json.Nodes;
using DTSoft.AppService.Localization;
using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Exceptions;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Esb;
using Microsoft.EntityFrameworkCore;

namespace DTSoft.AppService.Esb;

/// <summary>
/// ESB 数据源配置与执行服务。
/// </summary>
public class EsbDataSourceApp(
    SysDbContext context,
    EsbServiceConnectionApp connectionApp,
    EsbSqlExecutor sqlExecutor,
    EsbWebApiExecutor webApiExecutor,
    IAppLocalizer localizer)
{
    private const string SourceTypeSql = "sql";
    private const string SourceTypeRestful = "restful";
    private const string ExecuteModeQuery = "query";
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
        if (string.IsNullOrWhiteSpace(request.Code)) throw DtSoftException.BadRequest(L("esb.dataSourceCodeRequired"), "esb.dataSourceCodeRequired");

        var entity = await context.SysEsbDataSource!
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Code == request.Code.Trim() && item.Status == 1);
        if (entity == null) throw DtSoftException.NotFound(L("esb.dataSourceEnabledNotFound"), "esb.dataSourceEnabledNotFound");

        if (!string.Equals(entity.ExecuteMode, ExecuteModeQuery, StringComparison.OrdinalIgnoreCase))
        {
            throw DtSoftException.BadRequest(L("esb.queryModeOnly"), "esb.queryModeOnly");
        }

        var inputParameters = request.Parameters ?? new Dictionary<string, JsonNode?>();
        var declaredParameters = DeserializeParameters(entity.ParameterConfig);
        var variableContext = await BuildVariableContext(userAccount);
        var serviceConnection = await connectionApp.GetEnabledConnection(entity.ConnectionId);

        if (string.Equals(entity.SourceType, SourceTypeRestful, StringComparison.OrdinalIgnoreCase))
        {
            if (serviceConnection == null || !string.Equals(serviceConnection.ServiceType, SourceTypeRestful, StringComparison.OrdinalIgnoreCase))
            {
                throw DtSoftException.BadRequest(L("esb.webApiRequiresWebApiConnection"), "esb.webApiRequiresWebApiConnection");
            }

            return await webApiExecutor.Execute(
                entity,
                serviceConnection,
                declaredParameters,
                inputParameters,
                variableContext,
                request.PageNum,
                request.PageSize);
        }

        if (string.Equals(entity.SourceType, SourceTypeSql, StringComparison.OrdinalIgnoreCase))
        {
            return await sqlExecutor.Execute(
                entity,
                context.Database.GetDbConnection(),
                serviceConnection,
                connectionApp.GetDefaultDbType(),
                declaredParameters,
                inputParameters,
                variableContext,
                request.PageNum,
                request.PageSize);
        }

        throw DtSoftException.BadRequest(L("esb.sourceTypeUnsupported"), "esb.sourceTypeUnsupported");
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

    private async Task ValidateConnection(EsbDataSourceAddParameter parameter)
    {
        var connection = await connectionApp.GetEnabledConnection(parameter.ConnectionId);
        if (string.Equals(parameter.SourceType, SourceTypeSql, StringComparison.OrdinalIgnoreCase))
        {
            if (connection != null && !string.Equals(connection.ServiceType, "database", StringComparison.OrdinalIgnoreCase))
            {
                throw DtSoftException.BadRequest(L("esb.sqlRequiresDatabaseConnection"), "esb.sqlRequiresDatabaseConnection");
            }

            return;
        }

        if (string.Equals(parameter.SourceType, SourceTypeRestful, StringComparison.OrdinalIgnoreCase))
        {
            if (connection == null || !string.Equals(connection.ServiceType, SourceTypeRestful, StringComparison.OrdinalIgnoreCase))
            {
                throw DtSoftException.BadRequest(L("esb.webApiRequiresWebApiConnection"), "esb.webApiRequiresWebApiConnection");
            }
        }
    }

    private void NormalizeAndValidate(EsbDataSourceAddParameter parameter)
    {
        parameter.SourceType = NormalizeSourceType(parameter.SourceType);
        parameter.ExecuteMode = NormalizeExecuteMode(parameter.ExecuteMode);

        if (!string.Equals(parameter.ExecuteMode, ExecuteModeQuery, StringComparison.OrdinalIgnoreCase))
        {
            throw DtSoftException.BadRequest(L("esb.queryModeOnly"), "esb.queryModeOnly");
        }

        if (string.Equals(parameter.SourceType, SourceTypeSql, StringComparison.OrdinalIgnoreCase))
        {
            var sql = NormalizeSql(parameter.SqlText);
            EsbSqlSafety.ValidateSafeQuerySql(sql, localizer);
            EsbSqlSafety.ValidateSqlParameters(sql, parameter.Parameters ?? [], localizer);
            return;
        }

        if (string.Equals(parameter.SourceType, SourceTypeRestful, StringComparison.OrdinalIgnoreCase))
        {
            webApiExecutor.ValidateRequestConfig(parameter.HttpConfig);
            return;
        }

        throw DtSoftException.BadRequest(L("esb.sourceTypeUnsupported"), "esb.sourceTypeUnsupported");
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
        if (string.IsNullOrWhiteSpace(value)) throw DtSoftException.BadRequest(L("esb.sqlRequired"), "esb.sqlRequired");
        return value.Trim();
    }

    private string NormalizeHttpConfig(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw DtSoftException.BadRequest(L("esb.webApiConfigRequired"), "esb.webApiConfigRequired");
        return value.Trim();
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

}
