using DTSoft.AppService.MicroApp;
using DTSoft.Core.Common;
using DTSoft.Core.Common.Excel;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.MicroApp;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DTSoftServerApp.Controllers.MicroApp
{
    /// <summary>
    /// 微应用数据接口控制器
    /// </summary>
    [Authorize]
    [ApiController]
    [Tags("微应用数据")]
    public class MicroApiController : ControllerBase
    {
        private readonly SysDbContext _context;
        private readonly MicroTableService _microTableService;
        private readonly IDtSoftCache _dtSoftCache;

        public MicroApiController(SysDbContext context, MicroTableService microTableService, IDtSoftCache dtSoftCache)
        {
            _context = context;
            _microTableService = microTableService;
            _dtSoftCache = dtSoftCache;
        }

        private async Task<SysMicroAppConfig?> GetActiveConfigAsync(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName)) return null;

            var cacheKey = MicroConfigCacheKeys.ActiveConfig(modelName);
            var cachedJson = await _dtSoftCache.GetAsync<string>(cacheKey);
            if (!string.IsNullOrWhiteSpace(cachedJson))
            {
                try
                {
                    var cached = JsonSerializer.Deserialize<SysMicroAppConfig>(cachedJson);
                    if (cached is { Status: 1 } &&
                        cached.ModelName.Equals(modelName, StringComparison.OrdinalIgnoreCase))
                    {
                        return cached;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            var config = await _context.Set<SysMicroAppConfig>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ModelName == modelName && c.Status == 1);

            if (config != null)
            {
                await _dtSoftCache.SetAsync(cacheKey, JsonSerializer.Serialize(config), TimeSpan.FromMinutes(1));
            }

            return config;
        }

        /// <summary>
        /// 查询微应用数据列表
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <param name="pageNum">页码</param>
        /// <param name="pageSize">每页条数</param>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="filters">字段级查询条件 JSON</param>
        /// <param name="sortField">排序字段</param>
        /// <param name="sortOrder">排序方向</param>
        /// <returns>数据列表</returns>
        [HttpGet("/api/{modelName}")]
        public async Task<IActionResult> GetList(
            string modelName,
            [FromQuery] int pageNum,
            [FromQuery] int pageSize,
            [FromQuery] string keyword = "",
            [FromQuery] string filters = "",
            [FromQuery] string sortField = "",
            [FromQuery] string sortOrder = "")
        {
            try
            {
                // 获取模型配置
                var config = await GetActiveConfigAsync(modelName);

                if (config == null)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "未找到对应的微应用配置"
                    });
                }

                // 确保数据表存在
                await _microTableService.EnsureTableExistsAsync(config);

                // 构建动态查询
                var result = await _microTableService.ExecuteMicroQueryAsync(
                    config,
                    pageNum,
                    pageSize,
                    keyword,
                    ParseQueryFilters(filters),
                    sortField,
                    sortOrder,
                    DtSoftHelper.GetLoginUserAccount(User));

                return Ok(new
                {
                    success = true,
                    msg = "获取成功",
                    data = result  // 确保返回的是包含list和total的对象
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    msg = $"查询失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 查询微应用数据详情
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <param name="id">数据ID</param>
        /// <returns>数据详情</returns>
        [HttpGet("/api/{modelName}/{id:long}")]
        public async Task<IActionResult> GetDetail(string modelName, long id)
        {
            try
            {
                // 获取模型配置
                var config = await GetActiveConfigAsync(modelName);

                if (config == null)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "未找到对应的微应用配置"
                    });
                }

                // 确保数据表存在
                await _microTableService.EnsureTableExistsAsync(config);

                // 构建动态查询详情
                var result = await _microTableService.ExecuteMicroDetailQueryAsync(
                    config,
                    id,
                    DtSoftHelper.GetLoginUserAccount(User));

                return Ok(new
                {
                    success = true,
                    msg = "获取成功",
                    data = (object)result
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    msg = $"获取详情失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 新增微应用数据
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <param name="data">新增数据</param>
        /// <returns>新增结果</returns>
        [HttpPost("/api/{modelName}")]
        public async Task<IActionResult> Create(string modelName, [FromBody] object data)
        {
            try
            {
                // 获取模型配置
                var config = await GetActiveConfigAsync(modelName);

                if (config == null)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "未找到对应的微应用配置"
                    });
                }

                if (!config.SupportCreate)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "该配置不支持新增操作"
                    });
                }

                // 确保数据表存在
                await _microTableService.EnsureTableExistsAsync(config);

                // 将数据转换为字典，并处理JsonElement类型
                var dataDict = ConvertObjectToDictionary(data);
                var validationErrors = ValidateMicroData(config, dataDict);
                if (validationErrors.Count > 0)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = string.Join("；", validationErrors)
                    });
                }

                // 执行微应用数据插入
                var result = await _microTableService.ExecuteMicroInsertAsync(
                    config,
                    dataDict,
                    DtSoftHelper.GetLoginUserAccount(User));

                return Ok(new
                {
                    success = true,
                    msg = "添加成功",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    msg = $"添加失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 更新微应用数据
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <param name="id">数据ID</param>
        /// <param name="data">更新数据</param>
        /// <returns>更新结果</returns>
        [HttpPut("/api/{modelName}/{id:long}")]
        public async Task<IActionResult> Update(string modelName, long id, [FromBody] object data)
        {
            try
            {
                // 获取模型配置
                var config = await GetActiveConfigAsync(modelName);

                if (config == null)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "未找到对应的微应用配置"
                    });
                }

                if (!config.SupportUpdate)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "该配置不支持更新操作"
                    });
                }

                // 确保数据表存在
                await _microTableService.EnsureTableExistsAsync(config);

                // 将数据转换为字典，并处理JsonElement类型
                var dataDict = ConvertObjectToDictionary(data);
                var validationErrors = ValidateMicroData(config, dataDict);
                if (validationErrors.Count > 0)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = string.Join("；", validationErrors)
                    });
                }

                // 执行微应用数据更新
                var result = await _microTableService.ExecuteMicroUpdateAsync(
                    config,
                    id,
                    dataDict,
                    DtSoftHelper.GetLoginUserAccount(User));

                if (!result)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "更新失败，数据不存在"
                    });
                }

                return Ok(new
                {
                    success = true,
                    msg = "更新成功"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    msg = $"更新失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 删除微应用数据
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <param name="id">数据ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("/api/{modelName}/{id:long}")]
        public async Task<IActionResult> Delete(string modelName, long id)
        {
            try
            {
                // 获取模型配置
                var config = await GetActiveConfigAsync(modelName);

                if (config == null)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "未找到对应的微应用配置",
                    });
                }

                if (!config.SupportDelete)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "该配置不支持删除操作"
                    });
                }

                // 确保数据表存在
                await _microTableService.EnsureTableExistsAsync(config);

                // 执行微应用数据删除
                var result = await _microTableService.ExecuteMicroDeleteAsync(
                    config,
                    id,
                    DtSoftHelper.GetLoginUserAccount(User));

                if (!result)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "删除失败，数据不存在"
                    });
                }

                return Ok(new
                {
                    success = true,
                    msg = "删除成功"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    msg = $"删除失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 批量删除微应用数据
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <param name="parameter">批量删除参数</param>
        /// <returns>删除结果</returns>
        [HttpPost("/api/{modelName}/batch-delete")]
        public async Task<IActionResult> BatchDelete(string modelName, [FromBody] MicroBatchDeleteParameter parameter)
        {
            try
            {
                var config = await GetActiveConfigAsync(modelName);

                if (config == null)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "未找到对应的微应用配置"
                    });
                }

                if (!config.SupportDelete || !config.SupportBatchDelete)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "该配置不支持批量删除操作"
                    });
                }

                if (parameter.Ids.Count == 0)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "请选择要删除的数据"
                    });
                }

                await _microTableService.EnsureTableExistsAsync(config);

                var rowsAffected = await _microTableService.ExecuteMicroBatchDeleteAsync(
                    config,
                    parameter.Ids,
                    DtSoftHelper.GetLoginUserAccount(User));

                return Ok(new
                {
                    success = true,
                    msg = $"删除成功，共删除 {rowsAffected} 条数据",
                    data = new { deleted = rowsAffected }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    msg = $"批量删除失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 导出微应用数据 Excel
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="filters">字段级查询条件 JSON</param>
        /// <param name="sortField">排序字段</param>
        /// <param name="sortOrder">排序方向</param>
        /// <returns>Excel 文件</returns>
        [HttpGet("/api/{modelName}/export")]
        public async Task<IActionResult> ExportExcel(
            string modelName,
            [FromQuery] string keyword = "",
            [FromQuery] string filters = "",
            [FromQuery] string sortField = "",
            [FromQuery] string sortOrder = "")
        {
            try
            {
                // 获取模型配置
                var config = await GetActiveConfigAsync(modelName);
        
                if (config == null)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "未找到对应的微应用配置"
                    });
                }
        
                // 检查是否支持导出
                if (!config.SupportExport)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "该配置不支持导出操作"
                    });
                }
        
                // 确保数据表存在
                await _microTableService.EnsureTableExistsAsync(config);
        
                // 获取字段配置
                var fields = string.IsNullOrEmpty(config.Fields) ?
                    new List<FieldConfig>() :
                    JsonSerializer.Deserialize<List<FieldConfig>>(config.Fields);
        
                // 获取所有数据（不分页）
                var result = await _microTableService.ExecuteMicroQueryAsync(
                    config,
                    1,
                    int.MaxValue,
                    keyword,
                    ParseQueryFilters(filters),
                    sortField,
                    sortOrder,
                    DtSoftHelper.GetLoginUserAccount(User));
        
                // 提取数据列表
                var resultType = result.GetType();
                var listProperty = resultType.GetProperty("list");
                var dataList = listProperty?.GetValue(result) as List<Dictionary<string, object>>;
        
                if (dataList == null || !dataList.Any())
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "没有可导出的数据"
                    });
                }
        
                // 使用 ExcelExportHelper 导出数据（使用字段配置）
                var fileName = $"{config.ConfigName}_export.xlsx";
                var excelData = await ExcelExportHelper.ExportDictionaryToExcelWithFieldConfigAsync(dataList!, fields!, fileName);
        
                // 返回 Excel 文件
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    msg = $"导出失败：{ex.Message}"
                });
            }
        }

        /// <summary>
        /// 将对象转换为字典，并处理JsonElement类型
        /// </summary>
        private Dictionary<string, object> ConvertObjectToDictionary(object obj)
        {
            var jsonString = JsonSerializer.Serialize(obj);
            var jsonDocument = JsonDocument.Parse(jsonString);
            var jsonObject = jsonDocument.RootElement;

            var result = new Dictionary<string, object>();

            foreach (var property in jsonObject.EnumerateObject())
            {
                result[property.Name] = ConvertJsonValue(property.Value)!;
            }

            return result;
        }

        /// <summary>
        /// 将JsonElement转换为基本类型
        /// </summary>
        private object? ConvertJsonValue(JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString(),
                JsonValueKind.Number => jsonElement.TryGetInt32(out int intVal) ? intVal : jsonElement.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Object => ConvertObjectToDictionary(jsonElement),
                JsonValueKind.Array => jsonElement.EnumerateArray().Select(ConvertJsonValue).ToArray(),
                _ => jsonElement.ToString()
            };
        }

        /// <summary>
        /// 解析字段级查询条件 JSON，解析失败时返回空集合。
        /// </summary>
        /// <param name="filters">字段级查询条件 JSON。</param>
        /// <returns>字段级查询条件集合。</returns>
        private static List<MicroQueryFilter> ParseQueryFilters(string filters)
        {
            if (string.IsNullOrWhiteSpace(filters))
            {
                return new List<MicroQueryFilter>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<MicroQueryFilter>>(
                           filters,
                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
                       new List<MicroQueryFilter>();
            }
            catch
            {
                return new List<MicroQueryFilter>();
            }
        }

        /// <summary>
        /// 根据微应用字段配置校验提交的数据。
        /// </summary>
        /// <param name="config">微应用配置。</param>
        /// <param name="dataDict">待校验的数据字典。</param>
        /// <returns>校验错误列表。</returns>
        private List<string> ValidateMicroData(SysMicroAppConfig config, Dictionary<string, object> dataDict)
        {
            var errors = new List<string>();
            var fields = string.IsNullOrWhiteSpace(config.Fields)
                ? new List<FieldConfig>()
                : JsonSerializer.Deserialize<List<FieldConfig>>(config.Fields) ?? new List<FieldConfig>();

            foreach (var field in fields)
            {
                if (string.IsNullOrWhiteSpace(field.FieldName))
                {
                    continue;
                }

                dataDict.TryGetValue(field.FieldName, out var rawValue);
                var value = rawValue is JsonElement jsonElement ? ConvertJsonValue(jsonElement) : rawValue;
                var textValue = value?.ToString();

                if (field.Required && string.IsNullOrWhiteSpace(textValue))
                {
                    errors.Add($"{field.Label}不能为空");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(textValue))
                {
                    continue;
                }

                if (field.MinLength.HasValue && textValue.Length < field.MinLength.Value)
                {
                    errors.Add($"{field.Label}不能少于{field.MinLength.Value}个字符");
                }

                if (field.MaxLength.HasValue && textValue.Length > field.MaxLength.Value)
                {
                    errors.Add($"{field.Label}不能超过{field.MaxLength.Value}个字符");
                }

                if (field.FieldType == "number" && decimal.TryParse(textValue, out var numberValue))
                {
                    if (field.MinValue.HasValue && numberValue < field.MinValue.Value)
                    {
                        errors.Add($"{field.Label}不能小于{field.MinValue.Value}");
                    }

                    if (field.MaxValue.HasValue && numberValue > field.MaxValue.Value)
                    {
                        errors.Add($"{field.Label}不能大于{field.MaxValue.Value}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(field.Pattern) && !IsRegexMatch(textValue, field.Pattern))
                {
                    errors.Add($"{field.Label}格式不正确");
                }
            }

            return errors;
        }

        /// <summary>
        /// 执行正则匹配，正则表达式非法时返回 false。
        /// </summary>
        /// <param name="value">待校验文本。</param>
        /// <param name="pattern">正则表达式。</param>
        /// <returns>是否匹配。</returns>
        private static bool IsRegexMatch(string value, string pattern)
        {
            try
            {
                return Regex.IsMatch(value, pattern);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 导入微应用 Excel 数据
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <param name="file">上传的Excel文件</param>
        /// <returns>导入结果</returns>
        [HttpPost("/api/{modelName}/import")]
        [RequestSizeLimit(100 * 1024 * 1024)] // 限制请求大小为100MB
        public async Task<IActionResult> ImportExcel(string modelName, IFormFile? file)
        {
            try
            {
                // 检查是否有上传的文件
                if (file == null || file.Length == 0)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "请上传Excel文件"
                    });
                }

                // 检查文件类型
                var allowedExtensions = new[] { ".xlsx", ".xls" };
                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "文件格式不正确，请上传Excel文件(.xlsx或.xls)"
                    });
                }

                // 获取模型配置
                var config = await GetActiveConfigAsync(modelName);

                if (config == null)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "未找到对应的微应用配置"
                    });
                }

                // 检查是否支持导入
                if (!config.SupportImport)
                {
                    return Ok(new
                    {
                        success = false,
                        msg = "该配置不支持导入操作"
                    });
                }

                // 确保数据表存在
                await _microTableService.EnsureTableExistsAsync(config);

                // 获取字段配置
                var fields = string.IsNullOrEmpty(config.Fields) ?
                    new List<FieldConfig>() :
                    JsonSerializer.Deserialize<List<FieldConfig>>(config.Fields);

                // 读取并解析Excel文件
                await using var fileStream = file.OpenReadStream();
                var importedData = await ExcelImportHelper.ImportAndValidateDataAsync(fileStream, fields!);

                // 批量插入数据（优化性能）
                var successCount = 0;
                var errorCount = 0;
                var errorMessages = new List<string>();

                try
                {
                    // 使用批量插入方法，一次性插入所有数据
                    successCount = await _microTableService.ExecuteMicroBatchInsertAsync(
                        config,
                        importedData,
                        DtSoftHelper.GetLoginUserAccount(User));
                }
                catch (Exception ex)
                {
                    errorCount = importedData.Count;
                    errorMessages.Add($"批量导入失败: {ex.Message}");
                }

                var total = importedData.Count;
                var resultMsg = $"导入完成！成功: {successCount}, 失败: {errorCount}";
                if (errorCount > 0)
                {
                    resultMsg += $", 错误详情: {string.Join("; ", errorMessages.Take(5))}"; // 只显示前5个错误
                }

                return Ok(new
                {
                    success = true,
                    msg = resultMsg,
                    data = new { total, success = successCount, failed = errorCount }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    msg = $"导入失败: {ex.Message}"
                });
            }
        }
    }
}
