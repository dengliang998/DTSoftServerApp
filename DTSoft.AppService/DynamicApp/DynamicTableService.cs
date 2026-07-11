using DTSoft.Core;
using DTSoft.Core.Common;
using DTSoft.Core.DbProviders;
using DTSoft.Core.DbContexts;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.DynamicApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;

namespace DTSoft.AppService.DynamicApp
{
    public class DynamicTableService
    {
        private readonly SysDbContext _context;
        private readonly IDbProvider _provider;
        private readonly ILogger<DynamicTableService> _logger;
        private readonly IMemoryCache _cache;
        private static readonly ConcurrentDictionary<long, SemaphoreSlim> EnsureLocks = new();

        public DynamicTableService(SysDbContext context, ILogger<DynamicTableService> logger, IMemoryCache cache)
        {
            _context = context;
            var databaseName = GetDatabaseName();
            _provider = DbProviderFactory.Create(_context.Database.ProviderName, databaseName);
            _logger = logger;
            _cache = cache;
        }

        public async Task EnsureTableExistsAsync(SysDynamicAppConfig config)
        {
            var cacheKey = $"DynamicTableEnsured:{config.ItemId}";
            var signature = ComputeConfigSignature(config);
            if (_cache.TryGetValue(cacheKey, out string? cachedSignature) && cachedSignature == signature)
                return;

            var gate = EnsureLocks.GetOrAdd(config.ItemId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedSignature) && cachedSignature == signature)
                    return;

            var tableName = BuildDynamicTableName(config.ModelName);
            var fields = string.IsNullOrEmpty(config.Fields)
                ? new List<FieldConfig>()
                : JsonSerializer.Deserialize<List<FieldConfig>>(config.Fields)!;

            var tableExists = await CheckTableExistsAsync(tableName);
            if (!tableExists)
            {
                await CreateDynamicTableAsync(tableName, fields);
            }
            else
            {
                await UpdateDynamicTableAsync(tableName, fields);
            }

                _cache.Set(cacheKey, signature, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromHours(6)
                });
            }
            finally
            {
                gate.Release();
            }
        }

        private static string ComputeConfigSignature(SysDynamicAppConfig config)
        {
            var fieldsJson = config.Fields ?? string.Empty;
            var updateTicks = config.UpdateTime.Ticks;
            var payload = $"{config.ItemId}|{updateTicks}|{fieldsJson}";
            var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash);
        }

        private static string BuildDynamicTableName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentException("ModelName cannot be empty", nameof(modelName));

            return $"micro_app_{modelName.Trim().ToLowerInvariant()}";
        }

        private async Task<bool> CheckTableExistsAsync(string tableName)
        {
            if (!IsValidTableName(tableName))
            {
                throw new ArgumentException("Invalid table name format", nameof(tableName));
            }

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = _provider.GetTableExistsQuery(tableName);
                var result = await command.ExecuteScalarAsync();
                return result != null && Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查表存在性时出错：{TableName}", tableName);
                throw;
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private async Task CreateDynamicTableAsync(string tableName, List<FieldConfig> fields)
        {
            if (!IsValidTableName(tableName))
            {
                throw new ArgumentException("Invalid table name format", nameof(tableName));
            }

            foreach (var field in fields)
            {
                if (!IsValidColumnName(field.FieldName))
                {
                    throw new ArgumentException($"Invalid column name format: {field.FieldName}", nameof(fields));
                }
            }

            var createTableSql = _provider.BuildCreateTableSql(tableName, fields);
            await _context.Database.ExecuteSqlRawAsync(createTableSql);
        }

        private async Task UpdateDynamicTableAsync(string tableName, List<FieldConfig> fields)
        {
            if (!IsValidTableName(tableName))
            {
                throw new ArgumentException("Invalid table name format", nameof(tableName));
            }

            foreach (var field in fields)
            {
                if (!IsValidColumnName(field.FieldName))
                {
                    throw new ArgumentException($"Invalid column name format: {field.FieldName}", nameof(fields));
                }
            }

            var existingColumns = await GetTableColumnsWithInfoAsync(tableName);
            foreach (var field in fields)
            {
                var columnName = field.FieldName;
                if (!existingColumns.TryGetValue(columnName, out var existingColumnInfo))
                {
                    var addColumnSql = _provider.BuildAddColumnSql(tableName, field);
                    await _context.Database.ExecuteSqlRawAsync(addColumnSql);
                }
                else
                {
                    if (existingColumnInfo.IsNullable && field.Required || !existingColumnInfo.IsNullable && !field.Required)
                    {
                        var alterColumnSql = _provider.BuildAlterColumnSql(tableName, field);
                        await _context.Database.ExecuteSqlRawAsync(alterColumnSql);
                    }
                }
            }
        }

        private async Task<Dictionary<string, ColumnInfo>> GetTableColumnsWithInfoAsync(string tableName)
        {
            if (!IsValidTableName(tableName))
            {
                throw new ArgumentException("Invalid table name format", nameof(tableName));
            }

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = _provider.GetTableColumnsWithInfoQuery(tableName);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var columnName = reader["ColumnName"].ToString();
                    var isNullableText = reader["IsNullable"].ToString();
                    var isNullable = isNullableText?.ToLower() is "yes" or "true" or "1";

                    if (!string.IsNullOrEmpty(columnName))
                    {
                        columns[columnName] = new ColumnInfo
                        {
                            Name = columnName,
                            IsNullable = isNullable
                        };
                    }
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return columns;
        }

        public async Task<object> ExecuteDynamicQueryAsync(SysDynamicAppConfig config, int pageNum, int pageSize, string keyword)
        {
            var tableName = BuildDynamicTableName(config.ModelName);
            var fields = string.IsNullOrEmpty(config.Fields)
                ? new List<FieldConfig>()
                : JsonSerializer.Deserialize<List<FieldConfig>>(config.Fields)!;

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            try
            {
                var countSql = _provider.BuildCountSql(tableName, fields, keyword);
                await using var countCommand = connection.CreateCommand();
                countCommand.CommandText = countSql;

                if (!string.IsNullOrEmpty(keyword))
                {
                    var param = countCommand.CreateParameter();
                    param.ParameterName = _provider.GetParameterName("keyword");
                    param.Value = $"%{keyword}%";
                    countCommand.Parameters.Add(param);
                }

                var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

                var selectSql = _provider.BuildSelectWithPagingSql(tableName, fields, keyword, pageNum, pageSize);
                await using var queryCommand = connection.CreateCommand();
                queryCommand.CommandText = selectSql;

                if (!string.IsNullOrEmpty(keyword))
                {
                    var param = queryCommand.CreateParameter();
                    param.ParameterName = _provider.GetParameterName("keyword");
                    param.Value = $"%{keyword}%";
                    queryCommand.Parameters.Add(param);
                }

                await using var reader = await queryCommand.ExecuteReaderAsync();
                var result = new List<Dictionary<string, object>>();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var columnName = reader.GetName(i);
                        var value = reader.IsDBNull(i) ? null : ConvertToBasicType(reader.GetValue(i));
                        row[columnName] = value;
                    }
                    result.Add(row!);
                }

                return new { list = result, total = totalCount };
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        public async Task<Dictionary<string, object>> ExecuteDynamicDetailQueryAsync(SysDynamicAppConfig config, long id)
        {
            var tableName = BuildDynamicTableName(config.ModelName);
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            try
            {
                var sql = _provider.BuildSelectByIdSql(tableName);
                await using var command = connection.CreateCommand();
                command.CommandText = sql;

                var param = command.CreateParameter();
                param.ParameterName = _provider.GetParameterName(DynamicTableSystemColumns.Id);
                param.Value = id;
                command.Parameters.Add(param);

                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new Dictionary<string, object>();
                }

                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : ConvertToBasicType(reader.GetValue(i));
                    row[columnName] = value!;
                }
                return row;
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        public async Task<int> ExecuteDynamicBatchInsertAsync(SysDynamicAppConfig config, List<Dictionary<string, object>> dataList, string userAccount)
        {
            if (dataList == null || dataList.Count == 0)
                return 0;

            var tableName = BuildDynamicTableName(config.ModelName);
            var now = TimeUtil.CstDateTime;
            
            // 从第一条数据中提取列名（所有数据应该有相同的列）
            var firstRow = dataList[0];
            var columns = new List<string>();
            var columnNames = new List<string>();
            
            // 添加Id列
            columns.Add(_provider.QuoteColumnName(DynamicTableSystemColumns.Id));
            columnNames.Add(DynamicTableSystemColumns.Id);
            
            foreach (var kvp in firstRow)
            {
                if (IsSystemColumn(kvp.Key))
                {
                    continue;
                }

                columns.Add(_provider.QuoteColumnName(kvp.Key));
                columnNames.Add(kvp.Key);
            }

            // 添加系统字段
            columns.Add(_provider.QuoteColumnName(DynamicTableSystemColumns.CreatedTime));
            columnNames.Add(DynamicTableSystemColumns.CreatedTime);
            columns.Add(_provider.QuoteColumnName(DynamicTableSystemColumns.UpdatedTime));
            columnNames.Add(DynamicTableSystemColumns.UpdatedTime);
            columns.Add(_provider.QuoteColumnName(DynamicTableSystemColumns.CreatedBy));
            columnNames.Add(DynamicTableSystemColumns.CreatedBy);
            columns.Add(_provider.QuoteColumnName(DynamicTableSystemColumns.UpdatedBy));
            columnNames.Add(DynamicTableSystemColumns.UpdatedBy);

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            try
            {
                // 使用事务确保批量插入的原子性
                await using var transaction = await connection.BeginTransactionAsync();
                
                int totalInserted = 0;
                
                // 根据数据库类型和字段数量动态计算批次大小
                // SQL Server最多支持2100个参数，MySQL和Oracle限制更宽松
                // 每条数据参数数量 = 字段数(ItemId + 业务字段 + 系统字段)
                int paramsPerRow = columns.Count;
                int maxBatchSize;
                
                if (_provider is SqlServerProvider)
                {
                    // SQL Server: 2100参数限制，留100个余量
                    maxBatchSize = Math.Max(1, 2000 / paramsPerRow);
                }
                else if (_provider is OracleProvider)
                {
                    // Oracle: 使用INSERT ALL语法，参数限制较宽松，但也要控制
                    maxBatchSize = Math.Max(1, 1000 / paramsPerRow);
                }
                else
                {
                    // MySQL: 参数限制较宽松，但也要考虑packet size
                    maxBatchSize = Math.Max(1, 1000 / paramsPerRow);
                }
                
                // 确保批次大小至少为1，最多不超过500
                maxBatchSize = Math.Clamp(maxBatchSize, 1, 500);
                
                int batchSize = maxBatchSize;
                
                for (int batchStart = 0; batchStart < dataList.Count; batchStart += batchSize)
                {
                    var batch = dataList.Skip(batchStart).Take(batchSize).ToList();
                    
                    // 构建批量插入 SQL
                    var allParamValues = new List<object?>();
                    var valueGroups = new List<string>();
                    int paramIndex = 0;
                    
                    foreach (var dataRow in batch)
                    {
                        var valuePlaceholders = new List<string>();
                        
                        // Id字段 - 生成雪花ID
                        var idParam = $"@p{paramIndex++}";
                        valuePlaceholders.Add(idParam);
                        allParamValues.Add(YitterHelper.NewId());
                        
                        // 数据字段
                        foreach (var columnName in columnNames.Skip(1).Take(columnNames.Count - 5))
                        {
                            var paramPlaceholder = $"@p{paramIndex++}";
                            valuePlaceholders.Add(paramPlaceholder);
                            allParamValues.Add(dataRow.ContainsKey(columnName) ? ConvertToBasicType(dataRow[columnName]) : null);
                        }
                        
                        // 系统字段
                        var createdTimeParam = $"@p{paramIndex++}";
                        var updatedTimeParam = $"@p{paramIndex++}";
                        var createdByParam = $"@p{paramIndex++}";
                        var updatedByParam = $"@p{paramIndex++}";
                        valuePlaceholders.Add(createdTimeParam);
                        valuePlaceholders.Add(updatedTimeParam);
                        valuePlaceholders.Add(createdByParam);
                        valuePlaceholders.Add(updatedByParam);
                        allParamValues.Add(now);
                        allParamValues.Add(now);
                        allParamValues.Add(userAccount ?? string.Empty);
                        allParamValues.Add(userAccount ?? string.Empty);
                        
                        valueGroups.Add($"({string.Join(", ", valuePlaceholders)})");
                    }
                    
                    var insertSql = $"INSERT INTO {_provider.QuoteTableName(tableName)} ({string.Join(", ", columns)}) VALUES {string.Join(", ", valueGroups)}";
                    
                    // Oracle 不支持多行 VALUES，需要特殊处理
                    if (_provider is OracleProvider)
                    {
                        // Oracle 使用 INSERT ALL 语法
                        var insertStatements = new List<string>();
                        foreach (var valueGroup in valueGroups)
                        {
                            insertStatements.Add($"INTO {_provider.QuoteTableName(tableName)} ({string.Join(", ", columns)}) VALUES {valueGroup}");
                        }
                        insertSql = $"INSERT ALL {string.Join(" ", insertStatements)} SELECT 1 FROM DUAL";
                    }
                    
                    await using var command = connection.CreateCommand();
                    command.CommandText = insertSql;
                    command.Transaction = transaction;
                    
                    for (int i = 0; i < allParamValues.Count; i++)
                    {
                        var param = command.CreateParameter();
                        param.ParameterName = $"@p{i}";
                        param.Value = allParamValues[i] ?? DBNull.Value;
                        command.Parameters.Add(param);
                    }
                    
                    totalInserted += await command.ExecuteNonQueryAsync();
                }
                
                await transaction.CommitAsync();
                return totalInserted;
            }
            catch
            {
                // 发生异常时事务会自动回滚
                throw;
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        public async Task<Dictionary<string, object>> ExecuteDynamicInsertAsync(SysDynamicAppConfig config, Dictionary<string, object> dataDict, string userAccount)
        {
            var tableName = BuildDynamicTableName(config.ModelName);
            var columns = new List<string>();
            var parameters = new List<string>();
            var paramValues = new List<object?>();

            // 生成雪花ID
            var snowflakeId = YitterHelper.NewId();
            columns.Add(_provider.QuoteColumnName(DynamicTableSystemColumns.Id));
            parameters.Add(_provider.GetParameterPlaceholder(DynamicTableSystemColumns.Id));
            paramValues.Add(snowflakeId);

            foreach (var kvp in dataDict)
            {
                if (IsSystemColumn(kvp.Key))
                {
                    continue;
                }

                columns.Add(_provider.QuoteColumnName(kvp.Key));
                parameters.Add(_provider.GetParameterPlaceholder(kvp.Key));
                paramValues.Add(ConvertToBasicType(kvp.Value));
            }

            columns.Add(_provider.QuoteColumnName(DynamicTableSystemColumns.CreatedTime));
            parameters.Add(_provider.GetParameterPlaceholder(DynamicTableSystemColumns.CreatedTime));
            paramValues.Add(TimeUtil.CstDateTime);

            columns.Add(_provider.QuoteColumnName(DynamicTableSystemColumns.UpdatedTime));
            parameters.Add(_provider.GetParameterPlaceholder(DynamicTableSystemColumns.UpdatedTime));
            paramValues.Add(TimeUtil.CstDateTime);

            columns.Add(_provider.QuoteColumnName(DynamicTableSystemColumns.CreatedBy));
            parameters.Add(_provider.GetParameterPlaceholder(DynamicTableSystemColumns.CreatedBy));
            paramValues.Add(userAccount ?? string.Empty);

            columns.Add(_provider.QuoteColumnName(DynamicTableSystemColumns.UpdatedBy));
            parameters.Add(_provider.GetParameterPlaceholder(DynamicTableSystemColumns.UpdatedBy));
            paramValues.Add(userAccount ?? string.Empty);

            var insertSql = _provider.BuildInsertSql(tableName, columns, parameters);

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = insertSql;

                for (int i = 0; i < paramValues.Count; i++)
                {
                    var param = command.CreateParameter();
                    var paramName = columns[i].Trim('`','[',']','"');
                    param.ParameterName = _provider.GetParameterName(paramName);
                    param.Value = paramValues[i];
                    command.Parameters.Add(param);
                }

                // 执行插入,不再需要获取自增ID
                await command.ExecuteNonQueryAsync();
                
                // 使用生成的雪花ID查询返回新插入的数据
                return await ExecuteDynamicDetailQueryAsync(config, snowflakeId);
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        public async Task<bool> ExecuteDynamicUpdateAsync(SysDynamicAppConfig config, long id, Dictionary<string, object> dataDict, string userAccount)
        {
            var tableName = BuildDynamicTableName(config.ModelName);
            var setParts = new List<string>();
            var paramValues = new List<object?>();

            foreach (var kvp in dataDict)
            {
                if (IsSystemColumn(kvp.Key))
                {
                    continue;
                }

                setParts.Add($"{_provider.QuoteColumnName(kvp.Key)} = {_provider.GetParameterPlaceholder(kvp.Key)}");
                paramValues.Add(ConvertToBasicType(kvp.Value));
            }

            setParts.Add($"{_provider.QuoteColumnName(DynamicTableSystemColumns.UpdatedTime)} = {_provider.GetParameterPlaceholder(DynamicTableSystemColumns.UpdatedTime)}");
            paramValues.Add(TimeUtil.CstDateTime);
            setParts.Add($"{_provider.QuoteColumnName(DynamicTableSystemColumns.UpdatedBy)} = {_provider.GetParameterPlaceholder(DynamicTableSystemColumns.UpdatedBy)}");
            paramValues.Add(userAccount ?? string.Empty);

            var sql = _provider.BuildUpdateSql(tableName, string.Join(", ", setParts));

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;

                for (int i = 0; i < paramValues.Count; i++)
                {
                    var paramName = setParts[i].Split('=')[0].Trim().Trim('`', '[', ']', '"');
                    var param = command.CreateParameter();
                    param.ParameterName = _provider.GetParameterName(paramName);
                    param.Value = paramValues[i];
                    command.Parameters.Add(param);
                }

                var idParam = command.CreateParameter();
                idParam.ParameterName = _provider.GetParameterName(DynamicTableSystemColumns.Id);
                idParam.Value = id;
                command.Parameters.Add(idParam);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        public async Task<bool> ExecuteDynamicDeleteAsync(SysDynamicAppConfig config, long id)
        {
            var tableName = BuildDynamicTableName(config.ModelName);
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            try
            {
                var sql = _provider.BuildDeleteByIdSql(tableName);
                await using var command = connection.CreateCommand();
                command.CommandText = sql;

                var param = command.CreateParameter();
                param.ParameterName = _provider.GetParameterName(DynamicTableSystemColumns.Id);
                param.Value = id;
                command.Parameters.Add(param);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private bool IsValidTableName(string tableName)
        {
            return !string.IsNullOrEmpty(tableName) &&
                   System.Text.RegularExpressions.Regex.IsMatch(tableName, "^[a-zA-Z_][a-zA-Z0-9_-]*$") &&
                   tableName.Length <= 128;
        }

        private bool IsValidColumnName(string columnName)
        {
            return !string.IsNullOrEmpty(columnName) &&
                   System.Text.RegularExpressions.Regex.IsMatch(columnName, "^[a-zA-Z_][a-zA-Z0-9_]*$") &&
                   columnName.Length <= 128;
        }

        private string GetDatabaseName()
        {
            var connectionString = _context.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString)) return string.Empty;

            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kvp = part.Split('=', 2);
                if (kvp.Length != 2) continue;

                var key = kvp[0].Trim().ToLower();
                if (key == "database" || key == "initial catalog")
                {
                    return kvp[1].Trim();
                }
            }

            return string.Empty;
        }

        private object? ConvertToBasicType(object? value)
        {
            if (value == null) return null;
            
            var valueType = value.GetType();
            
            // 处理 JsonElement 类型
            if (valueType.Name == "JsonElement")
            {
                var jsonElement = (JsonElement)value;
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.String => jsonElement.GetString(),
                    JsonValueKind.Number => jsonElement.GetDecimal(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => jsonElement.ToString()
                };
            }

            // 处理 JsonDocument 类型
            if (value is JsonDocument doc)
            {
                return doc.RootElement.ToString();
            }

            // 处理数据库特定的类型（如 MySqlDateTime, MySqlDecimal 等）
            // 尝试转换为基本类型
            if (value is DateTime dateTime)
                return dateTime;
            
            if (value is decimal or double or float)
                return Convert.ToDecimal(value);
            
            if (value is int or long or short)
                return Convert.ToInt64(value);
            
            if (value is bool)
                return value;
            
            if (value is string)
                return value;
            
            if (value is byte or byte[])
                return value;
            
            if (value is Guid)
                return value;
            
            // 对于其他类型，尝试转换为字符串
            return value.ToString();
        }

        private static bool IsSystemColumn(string columnName)
        {
            return columnName.Equals(DynamicTableSystemColumns.Id, StringComparison.OrdinalIgnoreCase) ||
                   columnName.Equals(DynamicTableSystemColumns.CreatedTime, StringComparison.OrdinalIgnoreCase) ||
                   columnName.Equals(DynamicTableSystemColumns.UpdatedTime, StringComparison.OrdinalIgnoreCase) ||
                   columnName.Equals(DynamicTableSystemColumns.CreatedBy, StringComparison.OrdinalIgnoreCase) ||
                   columnName.Equals(DynamicTableSystemColumns.UpdatedBy, StringComparison.OrdinalIgnoreCase);
        }

        private class ColumnInfo
        {
            public required string Name { get; set; }
            public bool IsNullable { get; init; }
        }
    }
}
