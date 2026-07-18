using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace DTSoft.Core.DbProviders;

/// <summary>
/// ESB 外部数据库连接工厂。
/// </summary>
public static class EsbDbConnectionFactory
{
    public static DbConnection CreateConnection(string? dbType, string connectionString)
    {
        return NormalizeDbType(dbType) switch
        {
            "sqlserver" => new SqlConnection(connectionString),
            "mysql" => new MySqlConnector.MySqlConnection(connectionString),
            "postgresql" => new NpgsqlConnection(connectionString),
            "oracle" => new OracleConnection(connectionString),
            var unsupported => throw new NotSupportedException($"不支持的数据库类型：{unsupported}")
        };
    }

    public static string NormalizeDbType(string? dbType)
    {
        if (string.IsNullOrWhiteSpace(dbType)) throw new ArgumentException("数据库类型不能为空", nameof(dbType));

        var normalized = dbType.Trim().ToLowerInvariant();
        if (normalized.Contains("sqlserver") || normalized.Contains("sql server") || normalized.Contains("microsoft.entityframeworkcore.sqlserver"))
        {
            return "sqlserver";
        }

        if (normalized.Contains("mysql") || normalized.Contains("pomelo"))
        {
            return "mysql";
        }

        if (normalized.Contains("postgresql") || normalized.Contains("postgres") || normalized.Contains("npgsql"))
        {
            return "postgresql";
        }

        if (normalized.Contains("oracle"))
        {
            return "oracle";
        }

        throw new NotSupportedException($"不支持的数据库类型：{dbType}");
    }

    public static string GetParameterPrefix(string? dbType)
    {
        return NormalizeDbType(dbType) == "oracle" ? ":" : "@";
    }

    public static string GetTestQuery(string? dbType)
    {
        return NormalizeDbType(dbType) == "oracle" ? "SELECT 1 FROM DUAL" : "SELECT 1";
    }
}
