using System.Text.RegularExpressions;
using DTSoft.Core.DbContexts;
using DTSoft.Models.Parameter.DynamicApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DTSoft.Core.DbProviders
{
    public class MySqlProvider : IDbProvider
    {
        // 验证标识符（表名、列名、数据库名）只能包含字母、数字、下划线
        private static readonly Regex IdentifierPattern = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

        public MySqlProvider(string? databaseName = null)
        {
            DatabaseName = databaseName;
        }

        public string? DatabaseName { get; }

        public bool IsMatch(string? providerName) => providerName?.Contains("mysql", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// 引用表名，并验证合法性
        /// </summary>
        public string QuoteTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be empty", nameof(tableName));
            
            if (!IdentifierPattern.IsMatch(tableName))
                throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName));
            
            return $"`{tableName}`";
        }

        /// <summary>
        /// 引用列名，并验证合法性
        /// </summary>
        public string QuoteColumnName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentException("Column name cannot be empty", nameof(columnName));
            
            if (!IdentifierPattern.IsMatch(columnName))
                throw new ArgumentException($"Invalid column name format: {columnName}", nameof(columnName));
            
            return $"`{columnName}`";
        }

        public string GetParameterPlaceholder(string parameterName) => $"@{parameterName}";

        public string GetParameterName(string baseName) => $"@{baseName}";

        /// <summary>
        /// 安全的字符串值转义，防止 SQL 注入
        /// </summary>
        public string EscapeStringValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            
            // 使用更安全的转义策略
            return value.Replace("\\", "\\\\")
                       .Replace("'", "''")
                       .Replace("\"", "\\\"")
                       .Replace("\r", "\\r")
                       .Replace("\n", "\\n")
                       .Replace("\t", "\\t");
        }

        public string GetTableExistsQuery(string tableName)
        {
            var dbName = DatabaseName ?? throw new InvalidOperationException("MySqlProvider requires database name for table existence check.");
            
            // 验证数据库名和表名
            if (!IdentifierPattern.IsMatch(dbName))
                throw new ArgumentException($"Invalid database name format: {dbName}", nameof(dbName));
            if (!IdentifierPattern.IsMatch(tableName))
                throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName));
            
            return $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '{EscapeStringValue(dbName)}' AND table_name = '{EscapeStringValue(tableName)}'";
        }

        public string GetTableColumnsWithInfoQuery(string tableName)
        {
            var dbName = DatabaseName ?? throw new InvalidOperationException("MySqlProvider requires database name for table columns query.");
            
            // 验证数据库名和表名
            if (!IdentifierPattern.IsMatch(dbName))
                throw new ArgumentException($"Invalid database name format: {dbName}", nameof(dbName));
            if (!IdentifierPattern.IsMatch(tableName))
                throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName));
            
            return $@"
                SELECT
                    COLUMN_NAME AS ColumnName,
                    IS_NULLABLE AS IsNullable
                FROM information_schema.columns
                WHERE table_schema = '{EscapeStringValue(dbName)}' AND table_name = '{EscapeStringValue(tableName)}'
            ";
        }

        public string MapFieldTypeToDbType(string fieldType) => fieldType switch
        {
            "string" => "VARCHAR(500)",
            "number" => "DECIMAL(18,2)",
            "datetime" => "DATETIME",
            "boolean" => "TINYINT(1)",
            "textarea" => "TEXT",
            "select" => "VARCHAR(200)",
            _ => "VARCHAR(500)",
        };

        /// <summary>
        /// 构建 CREATE TABLE 语句
        /// 注意：DDL 语句无法使用参数化，依赖表名和列名的严格验证来保证安全
        /// </summary>
        public string BuildCreateTableSql(string tableName, List<FieldConfig> fields)
        {
            // 表名已经在 QuoteTableName 中验证过
            var sql = $"CREATE TABLE {QuoteTableName(tableName)} (";
            sql += $"{QuoteColumnName(DynamicTableSystemColumns.Id)} BIGINT PRIMARY KEY, ";
            sql += $"{QuoteColumnName(DynamicTableSystemColumns.CreatedTime)} DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, ";
            sql += $"{QuoteColumnName(DynamicTableSystemColumns.UpdatedTime)} DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, ";
            sql += $"{QuoteColumnName(DynamicTableSystemColumns.CreatedBy)} VARCHAR(50) NOT NULL DEFAULT '', ";
            sql += $"{QuoteColumnName(DynamicTableSystemColumns.UpdatedBy)} VARCHAR(50) NOT NULL DEFAULT '', ";

            foreach (var field in fields)
            {
                // 字段名已经在 QuoteColumnName 中验证过
                var columnType = MapFieldTypeToDbType(field.FieldType);
                sql += $"{QuoteColumnName(field.FieldName)} {columnType}";
                sql += field.Required && field.FieldType != "boolean" ? " NOT NULL" : " NULL";

                if (!string.IsNullOrEmpty(field.DefaultValue))
                {
                    // DefaultValue 需要转义处理
                    var value = field.FieldType is "string" or "textarea" or "datetime" 
                        ? $"'{EscapeStringValue(field.DefaultValue)}'" 
                        : field.DefaultValue;
                    sql += $" DEFAULT {value}";
                }

                sql += ", ";
            }

            sql = sql.TrimEnd(' ', ',');
            sql += ")";
            return sql;
        }

        /// <summary>
        /// 构建 ADD COLUMN 语句
        /// 注意：DDL 语句无法使用参数化，依赖表名和列名的严格验证来保证安全
        /// </summary>
        public string BuildAddColumnSql(string tableName, FieldConfig field)
        {
            var columnType = MapFieldTypeToDbType(field.FieldType);
            var sql = $"ALTER TABLE {QuoteTableName(tableName)} ADD {QuoteColumnName(field.FieldName)} {columnType}";

            if (field.Required && field.FieldType != "boolean")
            {
                sql += " NOT NULL";
            }

            if (!string.IsNullOrEmpty(field.DefaultValue))
            {
                var value = field.FieldType is "string" or "textarea" or "datetime" 
                    ? $"'{EscapeStringValue(field.DefaultValue)}'" 
                    : field.DefaultValue;
                sql += $" DEFAULT {value}";
            }

            return sql;
        }

        /// <summary>
        /// 构建 ALTER COLUMN 语句
        /// 注意：DDL 语句无法使用参数化，依赖表名和列名的严格验证来保证安全
        /// </summary>
        public string BuildAlterColumnSql(string tableName, FieldConfig field)
        {
            var columnType = MapFieldTypeToDbType(field.FieldType);
            var sql = $"ALTER TABLE {QuoteTableName(tableName)} MODIFY {QuoteColumnName(field.FieldName)} {columnType}";

            if (field.Required && field.FieldType != "boolean")
            {
                sql += " NOT NULL";
            }
            else
            {
                sql += " NULL";
            }

            return sql;
        }

        private string BuildSearchCondition(List<FieldConfig> fields, string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return string.Empty;
            var textColumns = fields.Where(f => f.FieldType == "string").ToList();
            if (!textColumns.Any()) return string.Empty;
            var conditions = textColumns.Select(f => $"{QuoteColumnName(f.FieldName)} LIKE {GetParameterPlaceholder("keyword")}");
            return $" AND ({string.Join(" OR ", conditions)})";
        }

        private string GetSelectableFields(List<FieldConfig> fields)
        {
            var allFields = fields.Select(f => QuoteColumnName(f.FieldName)).ToList();
            if (!allFields.Any())
            {
                return $"{QuoteColumnName(DynamicTableSystemColumns.Id)}, {QuoteColumnName(DynamicTableSystemColumns.CreatedTime)}, {QuoteColumnName(DynamicTableSystemColumns.UpdatedTime)}, {QuoteColumnName(DynamicTableSystemColumns.CreatedBy)}, {QuoteColumnName(DynamicTableSystemColumns.UpdatedBy)}";
            }
            allFields.Add(QuoteColumnName(DynamicTableSystemColumns.Id));
            allFields.Add(QuoteColumnName(DynamicTableSystemColumns.CreatedTime));
            allFields.Add(QuoteColumnName(DynamicTableSystemColumns.UpdatedTime));
            allFields.Add(QuoteColumnName(DynamicTableSystemColumns.CreatedBy));
            allFields.Add(QuoteColumnName(DynamicTableSystemColumns.UpdatedBy));
            return string.Join(", ", allFields);
        }

        public string BuildSelectWithPagingSql(string tableName, List<FieldConfig> fields, string keyword, int pageNum, int pageSize)
        {
            var selectFields = GetSelectableFields(fields);
            var where = " WHERE 1=1" + BuildSearchCondition(fields, keyword);
            var offset = (pageNum - 1) * pageSize;
            return $"SELECT {selectFields} FROM {QuoteTableName(tableName)}{where} ORDER BY {QuoteColumnName(DynamicTableSystemColumns.Id)} ASC LIMIT {offset}, {pageSize}";
        }

        public string BuildCountSql(string tableName, List<FieldConfig> fields, string keyword)
        {
            var where = " WHERE 1=1" + BuildSearchCondition(fields, keyword);
            return $"SELECT COUNT(*) FROM {QuoteTableName(tableName)}{where}";
        }

        public string BuildSelectByIdSql(string tableName)
        {
            return $"SELECT * FROM {QuoteTableName(tableName)} WHERE {QuoteColumnName(DynamicTableSystemColumns.Id)} = {GetParameterPlaceholder("id")}";
        }

        public string BuildDeleteByIdSql(string tableName)
        {
            return $"DELETE FROM {QuoteTableName(tableName)} WHERE {QuoteColumnName(DynamicTableSystemColumns.Id)} = {GetParameterPlaceholder("id")}";
        }

        public string BuildInsertSql(string tableName, List<string> columns, List<string> parameterNames)
        {
            var cols = string.Join(", ", columns);
            var pars = string.Join(", ", parameterNames);
            return $"INSERT INTO {QuoteTableName(tableName)} ({cols}) VALUES ({pars}); SELECT LAST_INSERT_ID();";
        }

        public string BuildGetNewIdSql(string tableName)
        {
            return $"SELECT MAX({QuoteColumnName(DynamicTableSystemColumns.Id)}) FROM {QuoteTableName(tableName)}";
        }

        public string BuildUpdateSql(string tableName, string setClause)
        {
            return $"UPDATE {QuoteTableName(tableName)} SET {setClause} WHERE {QuoteColumnName(DynamicTableSystemColumns.Id)} = {GetParameterPlaceholder(DynamicTableSystemColumns.Id)}";
        }

        public void ConfigureDbContext(IServiceCollection services, string connectionString)
        {
            services.AddDbContext<SysDbContext>(options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
        }
    }
}
