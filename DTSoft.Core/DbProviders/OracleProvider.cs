using System.Text.RegularExpressions;
using DTSoft.Core.DbContexts;
using DTSoft.Models.Parameter.MicroApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DTSoft.Core.DbProviders
{
    public class OracleProvider : IDbProvider
    {
        // 验证标识符（表名、列名、数据库名）只能包含字母、数字、下划线
        private static readonly Regex IdentifierPattern = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

        public OracleProvider(string? databaseName = null)
        {
            DatabaseName = databaseName;
        }

        public string? DatabaseName { get; }

        public bool IsMatch(string? providerName) => providerName?.Contains("oracle", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// 引用表名，并验证合法性
        /// </summary>
        public string QuoteTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be empty", nameof(tableName));
            
            if (!IdentifierPattern.IsMatch(tableName))
                throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName));
            
            return tableName; // Oracle 不使用引号，但已经验证了合法性
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
            
            return $"\"{columnName}\"";
        }

        public string GetParameterPlaceholder(string parameterName) => $":{parameterName}";

        public string GetParameterName(string baseName) => $":{baseName}";

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
            
            return $"SELECT COUNT(*) FROM user_tables WHERE table_name = '{EscapeStringValue(tableName.ToUpper())}'";
        }

        public string GetTableColumnsWithInfoQuery(string tableName)
        {
            // 验证表名
            if (!IdentifierPattern.IsMatch(tableName))
                throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName));
            
            return $@"
                SELECT
                    c.COLUMN_NAME AS ColumnName,
                    c.NULLABLE AS IsNullable,
                    c.CHAR_LENGTH AS MaxLength,
                    CASE WHEN i.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsIdentity
                FROM user_tab_columns c
                LEFT JOIN user_tab_identity_cols i ON i.TABLE_NAME = c.TABLE_NAME AND i.COLUMN_NAME = c.COLUMN_NAME
                WHERE c.table_name = '{EscapeStringValue(tableName.ToUpper())}'
            ";
        }

        public string MapFieldTypeToDbType(string fieldType) => fieldType switch
        {
            "string" => "NVARCHAR2(500)",
            "number" => "NUMBER(18,2)",
            "datetime" => "TIMESTAMP",
            "boolean" => "NUMBER(1)",
            "textarea" => "NCLOB",
            "select" => "NVARCHAR2(200)",
            "lookup" => "NVARCHAR2(500)",
            "attachment" => "NCLOB",
            _ => "NVARCHAR2(500)",
        };

        private static int NormalizeColumnLength(FieldConfig field, int defaultLength, int? existingMaxLength = null)
        {
            var length = field.ColumnLength ?? defaultLength;
            if (existingMaxLength.HasValue && existingMaxLength.Value > 0)
            {
                length = Math.Max(length, existingMaxLength.Value);
            }

            return Math.Clamp(length, 1, 2000);
        }

        private string MapFieldTypeToDbType(FieldConfig field, int? existingMaxLength = null) => field.FieldType switch
        {
            "string" => $"NVARCHAR2({NormalizeColumnLength(field, 500, existingMaxLength)})",
            "select" => $"NVARCHAR2({NormalizeColumnLength(field, 200, existingMaxLength)})",
            "radio" => $"NVARCHAR2({NormalizeColumnLength(field, 500, existingMaxLength)})",
            "checkbox" => $"NVARCHAR2({NormalizeColumnLength(field, 500, existingMaxLength)})",
            "lookup" => $"NVARCHAR2({NormalizeColumnLength(field, 500, existingMaxLength)})",
            _ => MapFieldTypeToDbType(field.FieldType),
        };

        /// <summary>
        /// 构建 CREATE TABLE 语句
        /// 注意：DDL 语句无法使用参数化，依赖表名和列名的严格验证来保证安全
        /// </summary>
        public string BuildCreateTableSql(string tableName, List<FieldConfig> fields)
        {
            var sql = $"CREATE TABLE {QuoteTableName(tableName)} (";
            sql += $"{QuoteColumnName(MicroTableSystemColumns.Id)} NUMBER(19) PRIMARY KEY, ";
            sql += $"{QuoteColumnName(MicroTableSystemColumns.CreatedTime)} TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL, ";
            sql += $"{QuoteColumnName(MicroTableSystemColumns.UpdatedTime)} TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL, ";
            sql += $"{QuoteColumnName(MicroTableSystemColumns.CreatedBy)} NVARCHAR2(50) DEFAULT '' NOT NULL, ";
            sql += $"{QuoteColumnName(MicroTableSystemColumns.UpdatedBy)} NVARCHAR2(50) DEFAULT '' NOT NULL, ";

            foreach (var field in fields)
            {
                var columnType = MapFieldTypeToDbType(field);
                sql += $"{QuoteColumnName(field.FieldName)} {columnType}";

                if (field.Required && field.FieldType != "boolean")
                {
                    sql += " NOT NULL";
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

            if (field.Required && field.FieldType != "boolean")
            {
                sql += " NOT NULL";
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
            var sql = $"ALTER TABLE {QuoteTableName(tableName)} MODIFY ({QuoteColumnName(field.FieldName)} {columnType}";

            if (field.Required && field.FieldType != "boolean")
            {
                sql += " NOT NULL";
            }

            sql += ")";
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
            var startRow = (pageNum - 1) * pageSize + 1;
            var endRow = pageNum * pageSize;
            return $"SELECT * FROM ( SELECT inner_query.*, ROWNUM rn FROM ( SELECT {selectFields} FROM {QuoteTableName(tableName)}{whereClause} {orderByClause} ) inner_query WHERE ROWNUM <= {endRow} ) WHERE rn >= {startRow}";
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
            return $"INSERT INTO {QuoteTableName(tableName)} ({cols}) VALUES ({pars})";
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
            services.AddDbContext<SysDbContext>(options => options.UseOracle(connectionString));
        }
    }
}
