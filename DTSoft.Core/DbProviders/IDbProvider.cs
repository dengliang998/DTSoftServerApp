using DTSoft.Models.Parameter.DynamicApp;
using Microsoft.Extensions.DependencyInjection;

namespace DTSoft.Core.DbProviders
{
    public interface IDbProvider
    {
        bool IsMatch(string? providerName);

        string QuoteTableName(string tableName);

        string QuoteColumnName(string columnName);

        string GetParameterPlaceholder(string parameterName);

        string GetParameterName(string baseName);

        string EscapeStringValue(string value);

        string GetTableExistsQuery(string tableName);

        string GetTableColumnsWithInfoQuery(string tableName);

        string BuildCreateTableSql(string tableName, List<FieldConfig> fields);

        string BuildAddColumnSql(string tableName, FieldConfig field);

        string BuildAlterColumnSql(string tableName, FieldConfig field);

        string BuildSelectWithPagingSql(string tableName, List<FieldConfig> fields, string keyword, int pageNum, int pageSize);

        string BuildCountSql(string tableName, List<FieldConfig> fields, string keyword);

        string BuildSelectByIdSql(string tableName);

        string BuildDeleteByIdSql(string tableName);

        string BuildInsertSql(string tableName, List<string> columns, List<string> parameterNames);

        string BuildGetNewIdSql(string tableName);

        string BuildUpdateSql(string tableName, string setClause);

        string MapFieldTypeToDbType(string fieldType);

        void ConfigureDbContext(IServiceCollection services, string connectionString);
    }
}
