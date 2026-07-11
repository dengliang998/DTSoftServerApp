using System.Text.RegularExpressions;
using DTSoft.Core.DbContexts;
using DTSoft.Models.Parameter.DynamicApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DTSoft.Core.DbProviders
{
    public class PostgreSqlProvider : IDbProvider
    {
        private static readonly Regex IdentifierPattern = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

        public PostgreSqlProvider(string? databaseName = null)
        {
            DatabaseName = databaseName;
        }

        public string? DatabaseName { get; }

        public bool IsMatch(string? providerName) =>
            providerName?.Contains("postgres", StringComparison.OrdinalIgnoreCase) == true ||
            providerName?.Contains("npgsql", StringComparison.OrdinalIgnoreCase) == true;

        public string QuoteTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be empty", nameof(tableName));

            if (!IdentifierPattern.IsMatch(tableName))
                throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName));

            return $"\"{tableName}\"";
        }

        public string QuoteColumnName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentException("Column name cannot be empty", nameof(columnName));

            if (!IdentifierPattern.IsMatch(columnName))
                throw new ArgumentException($"Invalid column name format: {columnName}", nameof(columnName));

            return $"\"{columnName}\"";
        }

        public string GetParameterPlaceholder(string parameterName) => $"@{parameterName}";

        public string GetParameterName(string baseName) => $"@{baseName}";

        public string EscapeStringValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("'", "''")
                .Replace("\\", "\\\\")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        public string GetTableExistsQuery(string tableName)
        {
            if (!IdentifierPattern.IsMatch(tableName))
                throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName));

            return $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = current_schema() AND table_name = '{EscapeStringValue(tableName)}'";
        }

        public string GetTableColumnsWithInfoQuery(string tableName)
        {
            if (!IdentifierPattern.IsMatch(tableName))
                throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName));

            return $@"
                SELECT
                    COLUMN_NAME AS ColumnName,
                    IS_NULLABLE AS IsNullable
                FROM information_schema.columns
                WHERE table_schema = current_schema() AND table_name = '{EscapeStringValue(tableName)}'
            ";
        }

        public string MapFieldTypeToDbType(string fieldType) => fieldType switch
        {
            "string" => "VARCHAR(500)",
            "number" => "NUMERIC(18,2)",
            "datetime" => "TIMESTAMP",
            "boolean" => "BOOLEAN",
            "textarea" => "TEXT",
            "select" => "VARCHAR(200)",
            _ => "VARCHAR(500)",
        };

        public string BuildCreateTableSql(string tableName, List<FieldConfig> fields)
        {
            var sql = $"CREATE TABLE {QuoteTableName(tableName)} (";
            sql += $"{QuoteColumnName(DynamicTableSystemColumns.Id)} BIGINT PRIMARY KEY, ";
            sql += $"{QuoteColumnName(DynamicTableSystemColumns.CreatedTime)} TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, ";
            sql += $"{QuoteColumnName(DynamicTableSystemColumns.UpdatedTime)} TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, ";
            sql += $"{QuoteColumnName(DynamicTableSystemColumns.CreatedBy)} VARCHAR(50) NOT NULL DEFAULT '', ";
            sql += $"{QuoteColumnName(DynamicTableSystemColumns.UpdatedBy)} VARCHAR(50) NOT NULL DEFAULT '', ";

            foreach (var field in fields)
            {
                var columnType = MapFieldTypeToDbType(field.FieldType);
                sql += $"{QuoteColumnName(field.FieldName)} {columnType}";
                sql += field.Required && field.FieldType != "boolean" ? " NOT NULL" : " NULL";

                if (!string.IsNullOrEmpty(field.DefaultValue))
                {
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

        public string BuildAddColumnSql(string tableName, FieldConfig field)
        {
            var columnType = MapFieldTypeToDbType(field.FieldType);
            var sql = $"ALTER TABLE {QuoteTableName(tableName)} ADD COLUMN {QuoteColumnName(field.FieldName)} {columnType}";
            sql += field.Required && field.FieldType != "boolean" ? " NOT NULL" : " NULL";

            if (!string.IsNullOrEmpty(field.DefaultValue))
            {
                var value = field.FieldType is "string" or "textarea" or "datetime"
                    ? $"'{EscapeStringValue(field.DefaultValue)}'"
                    : field.DefaultValue;
                sql += $" DEFAULT {value}";
            }

            return sql;
        }

        public string BuildAlterColumnSql(string tableName, FieldConfig field)
        {
            var sql = $"ALTER TABLE {QuoteTableName(tableName)} ALTER COLUMN {QuoteColumnName(field.FieldName)} ";
            sql += field.Required && field.FieldType != "boolean" ? "SET NOT NULL" : "DROP NOT NULL";
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
            return $"SELECT {selectFields} FROM {QuoteTableName(tableName)}{where} ORDER BY {QuoteColumnName(DynamicTableSystemColumns.Id)} ASC LIMIT {pageSize} OFFSET {offset}";
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
            return $"INSERT INTO {QuoteTableName(tableName)} ({cols}) VALUES ({pars})";
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
            services.AddDbContext<SysDbContext>(options => options.UseNpgsql(connectionString));
        }
    }
}
