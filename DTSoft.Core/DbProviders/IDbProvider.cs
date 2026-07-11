using DTSoft.Models.Parameter.MicroApp;
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

        string BuildSelectWithPagingSql(string tableName, List<FieldConfig> fields, string whereClause, string orderByClause, int pageNum, int pageSize);

        string BuildCountSql(string tableName, string whereClause);

        string BuildSelectByIdSql(string tableName);

        string BuildDeleteByIdSql(string tableName);

        string BuildDeleteByIdsSql(string tableName, List<string> parameterNames);

        string BuildInsertSql(string tableName, List<string> columns, List<string> parameterNames);

        string BuildGetNewIdSql(string tableName);

        string BuildUpdateSql(string tableName, string setClause);

        string MapFieldTypeToDbType(string fieldType);

        void ConfigureDbContext(IServiceCollection services, string connectionString);
    }
}
