using System.Text.RegularExpressions;
using DTSoft.Core.DbContexts;
using DTSoft.Models.Parameter.MicroApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DTSoft.Core.DbProviders
{
    public class SqlServerProvider : IDbProvider
    {
        // 验证标识符（表名、列名、数据库名）只能包含字母、数字、下划线
        private static readonly Regex IdentifierPattern = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

        public SqlServerProvider(string? databaseName = null)
        {
            DatabaseName = databaseName;
        }

        public string? DatabaseName { get; }

        public bool IsMatch(string? providerName) => providerName?.Contains("sqlserver", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// 引用表名，并验证合法性
        /// </summary>
        public string QuoteTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be empty", nameof(tableName));
            
            if (!IdentifierPattern.IsMatch(tableName))
                throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName));
            
            return $"[{tableName}]";
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
            
            return $"[{columnName}]";
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
            return value.Replace("'", "''")
                       .Replace("\\", "\\\\")
                       .Replace("\r", "\\r")
                       .Replace("\n", "\\n")
                       .Replace("\t", "\\t");
        }

        public string GetTableExistsQuery(string tableName)
        {
            // 验证表名
            if (!IdentifierPattern.IsMatch(tableName))
                throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName));
            
            return $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{EscapeStringValue(tableName)}'";
        }

        public string GetTableColumnsWithInfoQuery(string tableName)
        {
            // 验证表名
            if (!IdentifierPattern.IsMatch(tableName))
                throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName));
            
            return $@"
                SELECT
                    COLUMN_NAME AS ColumnName,
                    IS_NULLABLE AS IsNullable,
                    CHARACTER_MAXIMUM_LENGTH AS MaxLength
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = '{EscapeStringValue(tableName)}'
            ";
        }

        public string MapFieldTypeToDbType(string fieldType) => fieldType switch
        {
            "string" => "NVARCHAR(500)",
            "number" => "DECIMAL(18,2)",
            "datetime" => "DATETIME2",
            "boolean" => "BIT",
            "textarea" => "NTEXT",
            "select" => "NVARCHAR(200)",
            "attachment" => "NVARCHAR(MAX)",
            _ => "NVARCHAR(500)",
        };

        private static int NormalizeColumnLength(FieldConfig field, int defaultLength, int? existingMaxLength = null)
        {
            var length = field.ColumnLength ?? defaultLength;
            if (existingMaxLength.HasValue && existingMaxLength.Value > 0)
            {
                length = Math.Max(length, existingMaxLength.Value);
            }

            return Math.Clamp(length, 1, 4000);
        }

        private string MapFieldTypeToDbType(FieldConfig field, int? existingMaxLength = null) => field.FieldType switch
        {
            "string" => $"NVARCHAR({NormalizeColumnLength(field, 500, existingMaxLength)})",
            "select" => $"NVARCHAR({NormalizeColumnLength(field, 200, existingMaxLength)})",
            "radio" => $"NVARCHAR({NormalizeColumnLength(field, 500, existingMaxLength)})",
            "checkbox" => $"NVARCHAR({NormalizeColumnLength(field, 500, existingMaxLength)})",
            _ => MapFieldTypeToDbType(field.FieldType),
        };

        /// <summary>
        /// 构建 CREATE TABLE 语句
        /// 注意：DDL 语句无法使用参数化，依赖表名和列名的严格验证来保证安全
        /// </summary>
        public string BuildCreateTableSql(string tableName, List<FieldConfig> fields)
        {
            var sql = $"CREATE TABLE {QuoteTableName(tableName)} (";
            sql += $"{QuoteColumnName(MicroTableSystemColumns.Id)} BIGINT PRIMARY KEY, ";
            sql += $"{QuoteColumnName(MicroTableSystemColumns.CreatedTime)} datetime2 NOT NULL DEFAULT GETDATE(), ";
            sql += $"{QuoteColumnName(MicroTableSystemColumns.UpdatedTime)} datetime2 NOT NULL DEFAULT GETDATE(), ";
            sql += $"{QuoteColumnName(MicroTableSystemColumns.CreatedBy)} NVARCHAR(50) NOT NULL DEFAULT '', ";
            sql += $"{QuoteColumnName(MicroTableSystemColumns.UpdatedBy)} NVARCHAR(50) NOT NULL DEFAULT '', ";

            foreach (var field in fields)
            {
                var columnType = MapFieldTypeToDbType(field);
                sql += $"{QuoteColumnName(field.FieldName)} {columnType}";
                sql += field.Required && field.FieldType != "boolean" ? " NOT NULL" : " NULL";

                if (!string.IsNullOrEmpty(field.DefaultValue))
                {
                    var value = field.FieldType is "string" or "textarea" or "datetime" or "attachment"
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
            var columnType = MapFieldTypeToDbType(field);
            var sql = $"ALTER TABLE {QuoteTableName(tableName)} ADD {QuoteColumnName(field.FieldName)} {columnType}";
            sql += field.Required && field.FieldType != "boolean" ? " NOT NULL" : " NULL";

            if (!string.IsNullOrEmpty(field.DefaultValue))
            {
                var value = field.FieldType is "string" or "textarea" or "datetime" or "attachment"
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
        public string BuildAlterColumnSql(string tableName, FieldConfig field, int? existingMaxLength = null)
        {
            var columnType = MapFieldTypeToDbType(field, existingMaxLength);
            var sql = $"ALTER TABLE {QuoteTableName(tableName)} ALTER COLUMN {QuoteColumnName(field.FieldName)} {columnType}";
            sql += field.Required && field.FieldType != "boolean" ? " NOT NULL" : " NULL";
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
                return $"{QuoteColumnName(MicroTableSystemColumns.Id)}, {QuoteColumnName(MicroTableSystemColumns.CreatedTime)}, {QuoteColumnName(MicroTableSystemColumns.UpdatedTime)}, {QuoteColumnName(MicroTableSystemColumns.CreatedBy)}, {QuoteColumnName(MicroTableSystemColumns.UpdatedBy)}";
            }
            allFields.Add(QuoteColumnName(MicroTableSystemColumns.Id));
            allFields.Add(QuoteColumnName(MicroTableSystemColumns.CreatedTime));
            allFields.Add(QuoteColumnName(MicroTableSystemColumns.UpdatedTime));
            allFields.Add(QuoteColumnName(MicroTableSystemColumns.CreatedBy));
            allFields.Add(QuoteColumnName(MicroTableSystemColumns.UpdatedBy));
            return string.Join(", ", allFields);
        }

        public string BuildSelectWithPagingSql(string tableName, List<FieldConfig> fields, string whereClause, string orderByClause, int pageNum, int pageSize)
        {
            var selectFields = GetSelectableFields(fields);
            var offset = (pageNum - 1) * pageSize;
            return $"SELECT {selectFields} FROM {QuoteTableName(tableName)}{whereClause} {orderByClause} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
        }

        public string BuildCountSql(string tableName, string whereClause)
        {
            return $"SELECT COUNT(*) FROM {QuoteTableName(tableName)}{whereClause}";
        }

        public string BuildSelectByIdSql(string tableName)
        {
            return $"SELECT * FROM {QuoteTableName(tableName)} WHERE {QuoteColumnName(MicroTableSystemColumns.Id)} = {GetParameterPlaceholder(MicroTableSystemColumns.Id)}";
        }

        public string BuildDeleteByIdSql(string tableName)
        {
            return $"DELETE FROM {QuoteTableName(tableName)} WHERE {QuoteColumnName(MicroTableSystemColumns.Id)} = {GetParameterPlaceholder(MicroTableSystemColumns.Id)}";
        }

        public string BuildDeleteByIdsSql(string tableName, List<string> parameterNames)
        {
            return $"DELETE FROM {QuoteTableName(tableName)} WHERE {QuoteColumnName(MicroTableSystemColumns.Id)} IN ({string.Join(", ", parameterNames)})";
        }

        public string BuildInsertSql(string tableName, List<string> columns, List<string> parameterNames)
        {
            var cols = string.Join(", ", columns);
            var pars = string.Join(", ", parameterNames);
            return $"INSERT INTO {QuoteTableName(tableName)} ({cols}) VALUES ({pars}); SELECT SCOPE_IDENTITY();";
        }

        public string BuildGetNewIdSql(string tableName)
        {
            return $"SELECT MAX({QuoteColumnName(MicroTableSystemColumns.Id)}) FROM {QuoteTableName(tableName)}";
        }

        public string BuildUpdateSql(string tableName, string setClause)
        {
            return $"UPDATE {QuoteTableName(tableName)} SET {setClause} WHERE {QuoteColumnName(MicroTableSystemColumns.Id)} = {GetParameterPlaceholder(MicroTableSystemColumns.Id)}";
        }

        public void ConfigureDbContext(IServiceCollection services, string connectionString)
        {
            services.AddDbContext<SysDbContext>(options => options.UseSqlServer(connectionString));
        }
    }
}
