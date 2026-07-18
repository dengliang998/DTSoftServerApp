using System.Data;
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
public class EsbServiceConnectionApp(SysDbContext context)
{
    private const string ServiceTypeDatabase = "database";
    private const string ServiceTypeWebApi = "webapi";

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

        var duplicated = await context.SysEsbServiceConnection!.AnyAsync(item => item.Code == parameter.Code.Trim());
        if (duplicated) throw new Exception("连接编码已存在");

        var now = DateTime.Now;
        var entity = new SysEsbServiceConnection
        {
            ItemId = YitterHelper.NewId(),
            Code = parameter.Code.Trim(),
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
        if (entity == null) throw new Exception("未找到指定的 ESB 服务连接");

        var duplicated = await context.SysEsbServiceConnection!
            .AnyAsync(item => item.Code == parameter.Code.Trim() && item.ItemId != parameter.ItemId);
        if (duplicated) throw new Exception("连接编码已存在");

        entity.Code = parameter.Code.Trim();
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
        if (entity == null) throw new Exception("未找到指定的 ESB 服务连接");

        var used = await context.SysEsbDataSource!.AnyAsync(item => item.ConnectionId == id);
        if (used) throw new Exception("该连接已被 ESB 数据源使用，不能删除");

        context.SysEsbServiceConnection!.Remove(entity);
        await context.SaveChangesAsync();
    }

    public async Task TestConnection(EsbServiceConnectionTestParameter parameter)
    {
        var serviceType = NormalizeServiceType(parameter.ServiceType);
        if (serviceType != ServiceTypeDatabase)
        {
            throw new Exception("当前版本仅支持测试数据库连接");
        }

        if (parameter.ItemId is null or <= 0)
        {
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
            var dbType = NormalizeDbType(parameter.DbType);
            var connectionString = NormalizeConnectionString(parameter.ConnectionString);
            await TestExternalConnection(dbType, connectionString, NormalizeTimeoutSeconds(parameter.TimeoutSeconds));
            return;
        }

        if (entity.ServiceType != ServiceTypeDatabase)
        {
            throw new Exception("当前版本仅支持测试数据库连接");
        }

        await TestExternalConnection(entity.DbType, entity.ConnectionString, entity.TimeoutSeconds);
    }

    public async Task<SysEsbServiceConnection?> GetEnabledConnection(long? connectionId)
    {
        if (connectionId is null or 0) return null;

        var entity = await context.SysEsbServiceConnection!
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ItemId == connectionId.Value && item.Status == 1);
        if (entity == null) throw new Exception("未找到启用的 ESB 服务连接");

        return entity;
    }

    public string GetDefaultDbType()
    {
        return EsbDbConnectionFactory.NormalizeDbType(context.Database.ProviderName ?? "sqlserver");
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
            command.CommandText = EsbDbConnectionFactory.GetTestQuery(GetDefaultDbType());
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

    private static async Task TestExternalConnection(string? dbType, string? connectionString, int timeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new Exception("数据库连接字符串不能为空");

        await using var connection = EsbDbConnectionFactory.CreateConnection(dbType, connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = EsbDbConnectionFactory.GetTestQuery(dbType);
        command.CommandTimeout = timeoutSeconds;
        await command.ExecuteScalarAsync();
    }

    private EsbServiceConnectionResponse BuildDefaultConnectionResponse()
    {
        return new EsbServiceConnectionResponse
        {
            ItemId = 0,
            Code = "default",
            Name = "默认系统库",
            ServiceType = ServiceTypeDatabase,
            DbType = GetDefaultDbType(),
            ConnectionString = null,
            WebApiConfig = null,
            Status = 1,
            TimeoutSeconds = 30,
            Remark = "使用当前系统数据库连接",
            IsDefault = true
        };
    }

    private static void NormalizeAndValidate(EsbServiceConnectionAddParameter parameter)
    {
        parameter.ServiceType = NormalizeServiceType(parameter.ServiceType);
        parameter.DbType = NormalizeDbType(parameter.DbType);
        parameter.ConnectionString = NormalizeConnectionString(parameter.ConnectionString);
        parameter.WebApiConfig = NormalizeWebApiConfig(parameter.WebApiConfig);

        if (parameter.ServiceType == ServiceTypeDatabase)
        {
            parameter.DbType = NormalizeRequiredDbType(parameter.DbType);
            if (string.IsNullOrWhiteSpace(parameter.ConnectionString)) throw new Exception("数据库连接字符串不能为空");
            return;
        }

        if (parameter.ServiceType == ServiceTypeWebApi)
        {
            if (string.IsNullOrWhiteSpace(parameter.WebApiConfig)) throw new Exception("WebApi 配置不能为空");
            return;
        }

        throw new Exception("不支持的服务连接类型");
    }

    private static string NormalizeServiceType(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? ServiceTypeDatabase : value.Trim().ToLowerInvariant();
        return normalized is ServiceTypeDatabase or ServiceTypeWebApi ? normalized : throw new Exception("不支持的服务连接类型");
    }

    private static string? NormalizeDbType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return EsbDbConnectionFactory.NormalizeDbType(value);
    }

    private static string NormalizeRequiredDbType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new Exception("数据库类型不能为空");
        return EsbDbConnectionFactory.NormalizeDbType(value);
    }

    private static string? NormalizeConnectionString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeWebApiConfig(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
