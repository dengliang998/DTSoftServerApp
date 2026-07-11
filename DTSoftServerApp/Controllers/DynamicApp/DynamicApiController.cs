using DTSoft.AppService.DynamicApp;
using DTSoft.Core.Common.Excel;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.DynamicApp;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DTSoftServerApp.Controllers.DynamicApp
{
    /// <summary>
    /// 微应用数据接口控制器
    /// </summary>
    [Authorize]
    [ApiController]
    [Tags("微应用数据")]
    public class DynamicApiController : ControllerBase
    {
        private readonly SysDbContext _context;
        private readonly DynamicTableService _dynamicTableService;
        private readonly IDtSoftCache _dtSoftCache;

        public DynamicApiController(SysDbContext context, DynamicTableService dynamicTableService, IDtSoftCache dtSoftCache)
        {
            _context = context;
            _dynamicTableService = dynamicTableService;
            _dtSoftCache = dtSoftCache;
        }

        private async Task<SysDynamicAppConfig?> GetActiveConfigAsync(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName)) return null;

            var cacheKey = DynamicConfigCacheKeys.ActiveConfig(modelName);
            var cachedJson = await _dtSoftCache.GetAsync<string>(cacheKey);
            if (!string.IsNullOrWhiteSpace(cachedJson))
            {
                try
                {
                    var cached = JsonSerializer.Deserialize<SysDynamicAppConfig>(cachedJson);
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

            var config = await _context.Set<SysDynamicAppConfig>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ModelName == modelName && c.Status == 1);

            if (config != null)
            {
                await _dtSoftCache.SetAsync(cacheKey, JsonSerializer.Serialize(config), TimeSpan.FromMinutes(1));
            }

            return config;
        }

        /// <summary>
        /// 根据配置动态生成微应用查询列表接口
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <param name="pageNum">页码</param>
        /// <param name="pageSize">每页条数</param>
        /// <param name="keyword">搜索关键词</param>
        /// <returns>数据列表</returns>
        [HttpGet("/api/{modelName}")]
        public async Task<IActionResult> GetList(string modelName, [FromQuery] int pageNum, [FromQuery] int pageSize, [FromQuery] string keyword = "")
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
                await _dynamicTableService.EnsureTableExistsAsync(config);

                // 构建动态查询
                var result = await _dynamicTableService.ExecuteDynamicQueryAsync(config, pageNum, pageSize, keyword);

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
        /// 根据配置动态生成微应用详情接口
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
                await _dynamicTableService.EnsureTableExistsAsync(config);

                // 构建动态查询详情
                var result = await _dynamicTableService.ExecuteDynamicDetailQueryAsync(config, id);

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
        /// 根据配置动态生成微应用新增数据接口
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
                await _dynamicTableService.EnsureTableExistsAsync(config);

                // 将数据转换为字典，并处理JsonElement类型
                var dataDict = ConvertObjectToDictionary(data);

                // 执行动态插入
                var result = await _dynamicTableService.ExecuteDynamicInsertAsync(config, dataDict);

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
        /// 根据配置动态生成微应用更新数据接口
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
                await _dynamicTableService.EnsureTableExistsAsync(config);

                // 将数据转换为字典，并处理JsonElement类型
                var dataDict = ConvertObjectToDictionary(data);

                // 执行动态更新
                var result = await _dynamicTableService.ExecuteDynamicUpdateAsync(config, id, dataDict);

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
        /// 根据配置动态生成微应用删除数据接口
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
                await _dynamicTableService.EnsureTableExistsAsync(config);

                // 执行动态删除
                var result = await _dynamicTableService.ExecuteDynamicDeleteAsync(config, id);

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
        /// 根据配置动态生成微应用导出 Excel 接口
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <param name="keyword">搜索关键词</param>
        /// <returns>Excel 文件</returns>
        [HttpGet("/api/{modelName}/export")]
        public async Task<IActionResult> ExportExcel(string modelName, [FromQuery] string keyword = "")
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
                        msg = "未找到对应的 App 配置"
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
                await _dynamicTableService.EnsureTableExistsAsync(config);
        
                // 获取字段配置
                var fields = string.IsNullOrEmpty(config.Fields) ?
                    new List<FieldConfig>() :
                    JsonSerializer.Deserialize<List<FieldConfig>>(config.Fields);
        
                // 获取所有数据（不分页）
                var result = await _dynamicTableService.ExecuteDynamicQueryAsync(config, 1, int.MaxValue, keyword);
        
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
        /// 根据配置动态生成微应用 Excel 数据导入接口
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
                await _dynamicTableService.EnsureTableExistsAsync(config);

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
                    successCount = await _dynamicTableService.ExecuteDynamicBatchInsertAsync(config, importedData);
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
