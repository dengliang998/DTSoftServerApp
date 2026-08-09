using System.Globalization;

namespace DTSoft.Core.DbProviders;

internal static class DbProviderMessages
{
    internal static string Text(string key, params object[] args)
    {
        var english = CultureInfo.CurrentUICulture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var text = key switch
        {
            "db.providerRequired" => english
                ? "Database provider name is required. Specify 'MySql', 'SqlServer', 'Oracle', or 'PostgreSql'."
                : "数据库提供程序名称不能为空。请指定 'MySql'、'SqlServer'、'Oracle' 或 'PostgreSql'。",
            "db.providerUnsupported" => english
                ? "Unsupported database provider: {0}. Supported types include: {1}"
                : "不支持的数据库提供程序：{0}。支持的类型包括：{1}",
            "db.typeRequired" => english ? "Database type is required." : "数据库类型不能为空",
            "db.typeUnsupported" => english ? "Unsupported database type: {0}" : "不支持的数据库类型：{0}",
            "db.tableNameRequired" => english ? "Table name cannot be empty." : "表名不能为空。",
            "db.columnNameRequired" => english ? "Column name cannot be empty." : "列名不能为空。",
            "db.databaseNameRequiredForTableExists" => english
                ? "MySqlProvider requires database name for table existence check."
                : "MySqlProvider 检查表是否存在时需要数据库名称。",
            "db.databaseNameRequiredForColumns" => english
                ? "MySqlProvider requires database name for table columns query."
                : "MySqlProvider 查询表字段时需要数据库名称。",
            "db.connectionStringMissing" => english
                ? "Database connection string is not configured. Configure ConnectionStrings:Default."
                : "数据库连接字符串未配置，请配置 ConnectionStrings:Default。",
            _ => key
        };

        return args.Length == 0 ? text : string.Format(CultureInfo.CurrentUICulture, text, args);
    }
}
