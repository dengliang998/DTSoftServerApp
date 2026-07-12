using DTSoft.Core;
using DTSoft.Core.Common;
using DTSoft.Core.DbProviders;
using DTSoft.Core.DbContexts;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.MicroApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;

namespace DTSoft.AppService.MicroApp
{
    /// <summary>
    /// 微应用数据表结构维护与数据访问服务。
    /// </summary>
    public class MicroTableService
    {
        private readonly SysDbContext _context;
        private readonly IDbProvider _provider;
        private readonly ILogger<MicroTableService> _logger;
        private readonly IMemoryCache _cache;
        private static readonly ConcurrentDictionary<long, SemaphoreSlim> EnsureLocks = new();

        public MicroTableService(SysDbContext context, ILogger<MicroTableService> logger, IMemoryCache cache)
        {
            _context = context;
            var databaseName = GetDatabaseName();
            _provider = Core.DbProviders.DbProviderFactory.Create(_context.Database.ProviderName, databaseName);
            _logger = logger;
            _cache = cache;
        }

        /// <summary>
        /// 根据微应用配置创建或更新对应的数据表结构。
        /// </summary>
        /// <param name="config">微应用配置。</param>
        public async Task EnsureTableExistsAsync(SysMicroAppConfig config)
        {
            var cacheKey = $"MicroTableEnsured:{config.ItemId}";
            var signature = ComputeConfigSignature(config);
            if (_cache.TryGetValue(cacheKey, out string? cachedSignature) && cachedSignature == signature)
                return;

            var gate = EnsureLocks.GetOrAdd(config.ItemId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedSignature) && cachedSignature == signature)
                    return;

                var tableName = BuildMicroTableName(config.ModelName);
                var fields = string.IsNullOrEmpty(config.Fields)
                    ? new List<FieldConfig>()
                    : JsonSerializer.Deserialize<List<FieldConfig>>(config.Fields)!;

                var tableExists = await CheckTableExistsAsync(tableName);
                if (!tableExists)
                {
                    await CreateMicroTableAsync(tableName, fields);
                }
                else
                {
                    await UpdateMicroTableAsync(tableName, fields);
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

        private static string ComputeConfigSignature(SysMicroAppConfig config)
        {
            var fieldsJson = config.Fields ?? string.Empty;
            var updateTicks = config.UpdateTime.Ticks;
            var payload = $"{config.ItemId}|{updateTicks}|{fieldsJson}";
            var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash);
        }

        private static string BuildMicroTableName(string modelName)
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

        private async Task CreateMicroTableAsync(string tableName, List<FieldConfig> fields)
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

        private async Task UpdateMicroTableAsync(string tableName, List<FieldConfig> fields)
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
                        var nullabilityChanged =
                            existingColumnInfo.IsNullable && field.Required ||
                            !existingColumnInfo.IsNullable && !field.Required;
                        var shouldExpandLength = ShouldExpandColumnLength(field, existingColumnInfo.MaxLength);

                        if (nullabilityChanged || shouldExpandLength)
                        {
                            var alterColumnSql = _provider.BuildAlterColumnSql(tableName, field, existingColumnInfo.MaxLength);
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
                        int? maxLength = null;
                        if (reader["MaxLength"] != DBNull.Value && int.TryParse(reader["MaxLength"].ToString(), out var parsedMaxLength))
                        {
                            maxLength = parsedMaxLength;
                        }

                        columns[columnName] = new ColumnInfo
                        {
                            Name = columnName,
                            IsNullable = isNullable,
                            MaxLength = maxLength
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

        /// <summary>
        /// 执行微应用分页查询，支持关键词、字段筛选、排序和数据范围过滤。
        /// </summary>
        /// <param name="config">微应用配置。</param>
        /// <param name="pageNum">页码。</param>
        /// <param name="pageSize">每页条数。</param>
        /// <param name="keyword">全局关键词。</param>
        /// <param name="filters">字段级查询条件。</param>
        /// <param name="sortField">排序字段。</param>
        /// <param name="sortOrder">排序方向。</param>
        /// <param name="userAccount">当前登录用户账号。</param>
        /// <returns>包含列表和总数的查询结果。</returns>
        public async Task<object> ExecuteMicroQueryAsync(
            SysMicroAppConfig config,
            int pageNum,
            int pageSize,
            string keyword,
            List<MicroQueryFilter>? filters,
            string? sortField,
            string? sortOrder,
            string userAccount)
        {
            var tableName = BuildMicroTableName(config.ModelName);
            var fields = string.IsNullOrEmpty(config.Fields)
                ? new List<FieldConfig>()
                : JsonSerializer.Deserialize<List<FieldConfig>>(config.Fields)!;
            var scopedAccounts = await GetScopedAccountsAsync(config.DataScope, userAccount);
            var queryContext = BuildQueryContext(fields, keyword, filters, scopedAccounts);
            var orderByClause = BuildOrderByClause(fields, sortField, sortOrder);

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            try
            {
                var countSql = _provider.BuildCountSql(tableName, queryContext.WhereClause);
                await using var countCommand = connection.CreateCommand();
                countCommand.CommandText = countSql;
                AddParameters(countCommand, queryContext.Parameters);

                var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

                var selectSql = _provider.BuildSelectWithPagingSql(tableName, fields, queryContext.WhereClause, orderByClause, pageNum, pageSize);
                await using var queryCommand = connection.CreateCommand();
                queryCommand.CommandText = selectSql;
                AddParameters(queryCommand, queryContext.Parameters);

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

        /// <summary>
        /// 根据数据 ID 查询微应用表详情，并应用数据范围过滤。
        /// </summary>
        /// <param name="config">微应用配置。</param>
        /// <param name="id">数据 ID。</param>
        /// <param name="userAccount">当前登录用户账号。</param>
        /// <returns>微应用数据详情，不存在或无权限时返回空字典。</returns>
        public async Task<Dictionary<string, object>> ExecuteMicroDetailQueryAsync(SysMicroAppConfig config, long id, string userAccount = "")
        {
            var tableName = BuildMicroTableName(config.ModelName);
            var scopedAccounts = await GetScopedAccountsAsync(config.DataScope, userAccount);
            var dataScopeContext = BuildDataScopeContext(scopedAccounts);
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            try
            {
                var sql = $"SELECT * FROM {_provider.QuoteTableName(tableName)} WHERE {_provider.QuoteColumnName(MicroTableSystemColumns.Id)} = {_provider.GetParameterPlaceholder(MicroTableSystemColumns.Id)}{dataScopeContext.WhereSuffix}";
                await using var command = connection.CreateCommand();
                command.CommandText = sql;

                var param = command.CreateParameter();
                param.ParameterName = _provider.GetParameterName(MicroTableSystemColumns.Id);
                param.Value = id;
                command.Parameters.Add(param);
                AddParameters(command, dataScopeContext.Parameters);

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

        /// <summary>
        /// 批量写入微应用数据，并自动补齐系统字段。
        /// </summary>
        /// <param name="config">微应用配置。</param>
        /// <param name="dataList">待写入的数据列表。</param>
        /// <param name="userAccount">当前登录用户账号。</param>
        /// <returns>成功写入的数据条数。</returns>
        public async Task<int> ExecuteMicroBatchInsertAsync(SysMicroAppConfig config, List<Dictionary<string, object>> dataList, string userAccount)
        {
            if (dataList == null || dataList.Count == 0)
                return 0;

            var tableName = BuildMicroTableName(config.ModelName);
            var now = TimeUtil.CstDateTime;
            
            // 从第一条数据中提取列名（所有数据应该有相同的列）
            var firstRow = dataList[0];
            var columns = new List<string>();
            var columnNames = new List<string>();
            
            // 添加Id列
            columns.Add(_provider.QuoteColumnName(MicroTableSystemColumns.Id));
            columnNames.Add(MicroTableSystemColumns.Id);
            
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
            columns.Add(_provider.QuoteColumnName(MicroTableSystemColumns.CreatedTime));
            columnNames.Add(MicroTableSystemColumns.CreatedTime);
            columns.Add(_provider.QuoteColumnName(MicroTableSystemColumns.UpdatedTime));
            columnNames.Add(MicroTableSystemColumns.UpdatedTime);
            columns.Add(_provider.QuoteColumnName(MicroTableSystemColumns.CreatedBy));
            columnNames.Add(MicroTableSystemColumns.CreatedBy);
            columns.Add(_provider.QuoteColumnName(MicroTableSystemColumns.UpdatedBy));
            columnNames.Add(MicroTableSystemColumns.UpdatedBy);

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

        /// <summary>
        /// 新增一条微应用数据，并返回新增后的数据详情。
        /// </summary>
        /// <param name="config">微应用配置。</param>
        /// <param name="dataDict">待写入的数据。</param>
        /// <param name="userAccount">当前登录用户账号。</param>
        /// <returns>新增后的数据详情。</returns>
        public async Task<Dictionary<string, object>> ExecuteMicroInsertAsync(SysMicroAppConfig config, Dictionary<string, object> dataDict, string userAccount)
        {
            var tableName = BuildMicroTableName(config.ModelName);
            var columns = new List<string>();
            var parameters = new List<string>();
            var paramValues = new List<object?>();

            // 生成雪花ID
            var snowflakeId = YitterHelper.NewId();
            columns.Add(_provider.QuoteColumnName(MicroTableSystemColumns.Id));
            parameters.Add(_provider.GetParameterPlaceholder(MicroTableSystemColumns.Id));
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

            columns.Add(_provider.QuoteColumnName(MicroTableSystemColumns.CreatedTime));
            parameters.Add(_provider.GetParameterPlaceholder(MicroTableSystemColumns.CreatedTime));
            paramValues.Add(TimeUtil.CstDateTime);

            columns.Add(_provider.QuoteColumnName(MicroTableSystemColumns.UpdatedTime));
            parameters.Add(_provider.GetParameterPlaceholder(MicroTableSystemColumns.UpdatedTime));
            paramValues.Add(TimeUtil.CstDateTime);

            columns.Add(_provider.QuoteColumnName(MicroTableSystemColumns.CreatedBy));
            parameters.Add(_provider.GetParameterPlaceholder(MicroTableSystemColumns.CreatedBy));
            paramValues.Add(userAccount ?? string.Empty);

            columns.Add(_provider.QuoteColumnName(MicroTableSystemColumns.UpdatedBy));
            parameters.Add(_provider.GetParameterPlaceholder(MicroTableSystemColumns.UpdatedBy));
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
                return await ExecuteMicroDetailQueryAsync(config, snowflakeId, userAccount!);
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        /// <summary>
        /// 根据数据 ID 更新微应用数据，并应用数据范围过滤。
        /// </summary>
        /// <param name="config">微应用配置。</param>
        /// <param name="id">数据 ID。</param>
        /// <param name="dataDict">待更新的数据。</param>
        /// <param name="userAccount">当前登录用户账号。</param>
        /// <returns>是否更新成功。</returns>
        public async Task<bool> ExecuteMicroUpdateAsync(SysMicroAppConfig config, long id, Dictionary<string, object> dataDict, string userAccount)
        {
            var tableName = BuildMicroTableName(config.ModelName);
            var setParts = new List<string>();
            var paramValues = new List<object?>();
            var scopedAccounts = await GetScopedAccountsAsync(config.DataScope, userAccount);
            var dataScopeContext = BuildDataScopeContext(scopedAccounts);

            foreach (var kvp in dataDict)
            {
                if (IsSystemColumn(kvp.Key))
                {
                    continue;
                }

                setParts.Add($"{_provider.QuoteColumnName(kvp.Key)} = {_provider.GetParameterPlaceholder(kvp.Key)}");
                paramValues.Add(ConvertToBasicType(kvp.Value));
            }

            setParts.Add($"{_provider.QuoteColumnName(MicroTableSystemColumns.UpdatedTime)} = {_provider.GetParameterPlaceholder(MicroTableSystemColumns.UpdatedTime)}");
            paramValues.Add(TimeUtil.CstDateTime);
            setParts.Add($"{_provider.QuoteColumnName(MicroTableSystemColumns.UpdatedBy)} = {_provider.GetParameterPlaceholder(MicroTableSystemColumns.UpdatedBy)}");
            paramValues.Add(userAccount ?? string.Empty);

            var sql = _provider.BuildUpdateSql(tableName, string.Join(", ", setParts)) + dataScopeContext.WhereSuffix;

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
                idParam.ParameterName = _provider.GetParameterName(MicroTableSystemColumns.Id);
                idParam.Value = id;
                command.Parameters.Add(idParam);
                AddParameters(command, dataScopeContext.Parameters);

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

        /// <summary>
        /// 根据数据 ID 删除微应用表数据，并应用数据范围过滤。
        /// </summary>
        /// <param name="config">微应用配置。</param>
        /// <param name="id">数据 ID。</param>
        /// <param name="userAccount">当前登录用户账号。</param>
        /// <returns>是否删除成功。</returns>
        public async Task<bool> ExecuteMicroDeleteAsync(SysMicroAppConfig config, long id, string userAccount)
        {
            var tableName = BuildMicroTableName(config.ModelName);
            var scopedAccounts = await GetScopedAccountsAsync(config.DataScope, userAccount);
            var dataScopeContext = BuildDataScopeContext(scopedAccounts);
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            try
            {
                var sql = _provider.BuildDeleteByIdSql(tableName) + dataScopeContext.WhereSuffix;
                await using var command = connection.CreateCommand();
                command.CommandText = sql;

                var param = command.CreateParameter();
                param.ParameterName = _provider.GetParameterName(MicroTableSystemColumns.Id);
                param.Value = id;
                command.Parameters.Add(param);
                AddParameters(command, dataScopeContext.Parameters);

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

        /// <summary>
        /// 批量删除微应用表数据，并应用数据范围过滤。
        /// </summary>
        /// <param name="config">微应用配置。</param>
        /// <param name="ids">待删除的数据 ID 列表。</param>
        /// <param name="userAccount">当前登录用户账号。</param>
        /// <returns>实际删除的数据条数。</returns>
        public async Task<int> ExecuteMicroBatchDeleteAsync(SysMicroAppConfig config, List<long> ids, string userAccount)
        {
            if (ids == null || ids.Count == 0)
            {
                return 0;
            }

            var tableName = BuildMicroTableName(config.ModelName);
            var scopedAccounts = await GetScopedAccountsAsync(config.DataScope, userAccount);
            var dataScopeContext = BuildDataScopeContext(scopedAccounts);
            var normalizedIds = ids.Distinct().ToList();
            var idParameterNames = normalizedIds
                .Select((_, index) => _provider.GetParameterPlaceholder($"id{index}"))
                .ToList();

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            try
            {
                var sql = _provider.BuildDeleteByIdsSql(tableName, idParameterNames) + dataScopeContext.WhereSuffix;
                await using var command = connection.CreateCommand();
                command.CommandText = sql;

                for (var i = 0; i < normalizedIds.Count; i++)
                {
                    var param = command.CreateParameter();
                    param.ParameterName = _provider.GetParameterName($"id{i}");
                    param.Value = normalizedIds[i];
                    command.Parameters.Add(param);
                }

                AddParameters(command, dataScopeContext.Parameters);
                return await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        /// <summary>
        /// 构建微应用查询的 WHERE 条件和参数集合。
        /// </summary>
        /// <param name="fields">字段配置列表。</param>
        /// <param name="keyword">全局关键词。</param>
        /// <param name="filters">字段级查询条件。</param>
        /// <param name="scopedAccounts">数据范围允许访问的创建人账号列表，null 表示不过滤。</param>
        /// <returns>查询上下文。</returns>
        private QueryContext BuildQueryContext(
            List<FieldConfig> fields,
            string keyword,
            List<MicroQueryFilter>? filters,
            List<string>? scopedAccounts)
        {
            var whereParts = new List<string> { "1=1" };
            var parameters = new List<QueryParameter>();
            var searchableFields = fields
                .Where(f => !string.IsNullOrWhiteSpace(f.FieldName))
                .GroupBy(f => f.FieldName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var textFields = fields
                    .Where(f => !string.IsNullOrWhiteSpace(f.FieldName) &&
                                IsValidColumnName(f.FieldName) &&
                                f.FieldType is "string" or "textarea" or "select" or "radio")
                    .ToList();

                if (textFields.Count > 0)
                {
                    var keywordParamName = "keyword";
                    var conditions = textFields
                        .Select(f => $"{_provider.QuoteColumnName(f.FieldName)} LIKE {_provider.GetParameterPlaceholder(keywordParamName)}");
                    whereParts.Add($"({string.Join(" OR ", conditions)})");
                    parameters.Add(new QueryParameter(keywordParamName, $"%{keyword.Trim()}%"));
                }
            }

            if (filters != null)
            {
                var filterIndex = 0;
                foreach (var filter in filters)
                {
                    if (string.IsNullOrWhiteSpace(filter.FieldName) ||
                        !searchableFields.TryGetValue(filter.FieldName, out var field) ||
                        !IsValidColumnName(field.FieldName))
                    {
                        continue;
                    }

                    var mode = NormalizeQueryMode(filter.Mode ?? field.QueryMode);
                    if (mode == "none")
                    {
                        continue;
                    }

                    var columnName = _provider.QuoteColumnName(field.FieldName);
                    if (mode == "range")
                    {
                        var startValue = ConvertToBasicType(filter.StartValue);
                        var endValue = ConvertToBasicType(filter.EndValue);

                        if (!IsEmptyQueryValue(startValue))
                        {
                            var paramName = $"filter{filterIndex++}";
                            whereParts.Add($"{columnName} >= {_provider.GetParameterPlaceholder(paramName)}");
                            parameters.Add(new QueryParameter(paramName, startValue));
                        }

                        if (!IsEmptyQueryValue(endValue))
                        {
                            var paramName = $"filter{filterIndex++}";
                            whereParts.Add($"{columnName} <= {_provider.GetParameterPlaceholder(paramName)}");
                            parameters.Add(new QueryParameter(paramName, endValue));
                        }

                        continue;
                    }

                    var value = ConvertToBasicType(filter.Value);
                    if (IsEmptyQueryValue(value))
                    {
                        continue;
                    }

                    var valueParamName = $"filter{filterIndex++}";
                    var operatorText = mode == "fuzzy" ? "LIKE" : "=";
                    whereParts.Add($"{columnName} {operatorText} {_provider.GetParameterPlaceholder(valueParamName)}");
                    parameters.Add(new QueryParameter(valueParamName, mode == "fuzzy" ? $"%{value}%" : value));
                }
            }

            var dataScopeContext = BuildDataScopeContext(scopedAccounts);
            parameters.AddRange(dataScopeContext.Parameters);

            return new QueryContext($" WHERE {string.Join(" AND ", whereParts)}{dataScopeContext.WhereSuffix}", parameters);
        }

        /// <summary>
        /// 构建数据范围过滤 SQL 片段和参数集合。
        /// </summary>
        /// <param name="scopedAccounts">允许访问的创建人账号列表，null 表示全部数据。</param>
        /// <returns>数据范围过滤上下文。</returns>
        private DataScopeContext BuildDataScopeContext(List<string>? scopedAccounts)
        {
            if (scopedAccounts == null)
            {
                return new DataScopeContext(string.Empty, new List<QueryParameter>());
            }

            if (scopedAccounts.Count == 0)
            {
                return new DataScopeContext(" AND 1=0", new List<QueryParameter>());
            }

            var parameters = new List<QueryParameter>();
            var placeholders = new List<string>();
            for (var i = 0; i < scopedAccounts.Count; i++)
            {
                var paramName = $"scopeUser{i}";
                placeholders.Add(_provider.GetParameterPlaceholder(paramName));
                parameters.Add(new QueryParameter(paramName, scopedAccounts[i]));
            }

            var whereSuffix =
                $" AND {_provider.QuoteColumnName(MicroTableSystemColumns.CreatedBy)} IN ({string.Join(", ", placeholders)})";
            return new DataScopeContext(whereSuffix, parameters);
        }

        /// <summary>
        /// 根据数据权限范围解析当前用户可访问的创建人账号集合。
        /// </summary>
        /// <param name="dataScope">数据权限范围。</param>
        /// <param name="userAccount">当前登录用户账号。</param>
        /// <returns>null 表示全部数据；空列表表示无可访问数据。</returns>
        private async Task<List<string>?> GetScopedAccountsAsync(string? dataScope, string userAccount)
        {
            var normalizedScope = dataScope?.Trim().ToLowerInvariant();
            if (normalizedScope is null or "" or "all")
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(userAccount))
            {
                return new List<string>();
            }

            if (normalizedScope == "self")
            {
                return new List<string> { userAccount };
            }

            if (normalizedScope != "department")
            {
                return null;
            }

            var ouIds = await _context.SysUserMember!
                .AsNoTracking()
                .Where(m => m.UserAcc == userAccount)
                .Select(m => m.OuId)
                .Distinct()
                .ToListAsync();

            if (ouIds.Count == 0)
            {
                return new List<string> { userAccount };
            }

            var departmentAccounts = await _context.SysUserMember!
                .AsNoTracking()
                .Where(m => ouIds.Contains(m.OuId) && m.UserAcc != null)
                .Select(m => m.UserAcc!)
                .Distinct()
                .ToListAsync();

            if (!departmentAccounts.Contains(userAccount, StringComparer.OrdinalIgnoreCase))
            {
                departmentAccounts.Add(userAccount);
            }

            return departmentAccounts;
        }

        /// <summary>
        /// 构建经过字段配置校验的排序 SQL 片段。
        /// </summary>
        /// <param name="fields">字段配置列表。</param>
        /// <param name="sortField">排序字段。</param>
        /// <param name="sortOrder">排序方向。</param>
        /// <returns>ORDER BY SQL 片段。</returns>
        private string BuildOrderByClause(List<FieldConfig> fields, string? sortField, string? sortOrder)
        {
            var field = fields.FirstOrDefault(f =>
                f.Sortable &&
                !string.IsNullOrWhiteSpace(f.FieldName) &&
                f.FieldName.Equals(sortField, StringComparison.OrdinalIgnoreCase));

            var orderColumn = field != null && IsValidColumnName(field.FieldName)
                ? field.FieldName
                : MicroTableSystemColumns.Id;

            var direction = sortOrder?.Trim().ToLowerInvariant() is "desc" or "descending"
                ? "DESC"
                : "ASC";

            return $"ORDER BY {_provider.QuoteColumnName(orderColumn)} {direction}";
        }

        /// <summary>
        /// 向数据库命令追加查询参数。
        /// </summary>
        /// <param name="command">数据库命令。</param>
        /// <param name="parameters">参数集合。</param>
        private void AddParameters(DbCommand command, List<QueryParameter> parameters)
        {
            foreach (var item in parameters)
            {
                var param = command.CreateParameter();
                param.ParameterName = _provider.GetParameterName(item.Name);
                param.Value = item.Value ?? DBNull.Value;
                command.Parameters.Add(param);
            }
        }

        /// <summary>
        /// 标准化字段查询模式，非法值返回 none。
        /// </summary>
        /// <param name="mode">原始查询模式。</param>
        /// <returns>标准化后的查询模式。</returns>
        private static string NormalizeQueryMode(string? mode)
        {
            return mode?.Trim().ToLowerInvariant() switch
            {
                "exact" => "exact",
                "fuzzy" => "fuzzy",
                "range" => "range",
                _ => "none"
            };
        }

        /// <summary>
        /// 判断查询值是否为空。
        /// </summary>
        /// <param name="value">待判断的查询值。</param>
        /// <returns>是否为空查询值。</returns>
        private static bool IsEmptyQueryValue(object? value)
        {
            return value == null ||
                   value == DBNull.Value ||
                   value is string text && string.IsNullOrWhiteSpace(text);
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
            return columnName.Equals(MicroTableSystemColumns.Id, StringComparison.OrdinalIgnoreCase) ||
                   columnName.Equals(MicroTableSystemColumns.CreatedTime, StringComparison.OrdinalIgnoreCase) ||
                   columnName.Equals(MicroTableSystemColumns.UpdatedTime, StringComparison.OrdinalIgnoreCase) ||
                   columnName.Equals(MicroTableSystemColumns.CreatedBy, StringComparison.OrdinalIgnoreCase) ||
                   columnName.Equals(MicroTableSystemColumns.UpdatedBy, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SupportsColumnLength(FieldConfig field)
        {
            return field.FieldType is "string" or "select" or "radio" or "checkbox";
        }

        private static bool ShouldExpandColumnLength(FieldConfig field, int? existingMaxLength)
        {
            if (!SupportsColumnLength(field))
            {
                return false;
            }

            var desiredLength = field.ColumnLength ?? field.FieldType switch
            {
                "select" => 200,
                _ => 500
            };

            if (existingMaxLength.HasValue && existingMaxLength.Value > 0)
            {
                return desiredLength > existingMaxLength.Value;
            }

            return true;
        }

        private class ColumnInfo
        {
            public required string Name { get; set; }
            public bool IsNullable { get; init; }
            public int? MaxLength { get; init; }
        }

        private record QueryParameter(string Name, object? Value);

        private record QueryContext(string WhereClause, List<QueryParameter> Parameters);

        private record DataScopeContext(string WhereSuffix, List<QueryParameter> Parameters);
    }
}
