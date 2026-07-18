using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.MicroApp;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DTSoft.AppService.MicroApp
{
    public class MicroConfigApp(SysDbContext context, MicroTableService microTableService, IDtSoftCache dtSoftCache)
    {
        private readonly MicroTableService _microTableService = microTableService;

        /// <summary>
        /// 获取微应用配置列表
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns>分页的微应用配置列表</returns>
        public async Task<PagedResponse<MicroConfigResponse>> GetMicroAppConfigs(MicroConfigQueryParameter parameter)
        {
            await _microTableService.EnsureMicroConfigSubTablesColumnAsync();

            var query = context.SysMicroAppConfig!.AsQueryable();
        
            // 模糊匹配配置名称、模型名称、微应用路径
            if (!string.IsNullOrEmpty(parameter.Keyword))
            {
                query = query.Where(c => c.ConfigName.Contains(parameter.Keyword) ||
                                         c.ModelName.Contains(parameter.Keyword) ||
                                         (c.ApiPrefix != null && c.ApiPrefix.Contains(parameter.Keyword)));
            }
        
            // 按模型名称精确匹配
            if (!string.IsNullOrEmpty(parameter.ModelName))
            {
                query = query.Where(c => c.ModelName == parameter.ModelName);
            }

            if (!string.IsNullOrEmpty(parameter.MicroAppPath))
            {
                query = query.Where(c => c.ApiPrefix == parameter.MicroAppPath);
            }
        
            // 计算总数
            var total = await query.CountAsync();
        
            // 分页查询 - 如果分页参数为空，则返回所有结果
            var configs = query.OrderByDescending(o=>o.CreateTime).AsQueryable();
            if (parameter is { PageNum: > 0, PageSize: > 0 })
            {
                configs = configs
                    .Skip((parameter.PageNum.Value - 1) * parameter.PageSize.Value)
                    .Take(parameter.PageSize.Value);
            }
        
            var configList = await configs.ToListAsync();
        
            // 转换为响应对象
            var result = new PagedResponse<MicroConfigResponse>
            {
                Data = configList.Select(config => MicroConfigSchema.ToResponse(config)).ToList(),
                Total = total
            };
        
            return result;
        }

        /// <summary>
        /// 获取微应用配置详情
        /// </summary>
        /// <param name="id">配置 ID</param>
        /// <returns>微应用配置详情</returns>
        public MicroConfigResponse GetMicroConfigById(long id)
        {
            _microTableService.EnsureMicroConfigSubTablesColumnAsync().GetAwaiter().GetResult();

            var config = context.SysMicroAppConfig!.FirstOrDefault(c => c.ItemId == id);
            if (config == null)
            {
                throw new Exception("未找到指定的微应用配置");
            }

            return MicroConfigSchema.ToResponse(config);
        }

        /// <summary>
        /// 添加微应用配置
        /// </summary>
        /// <param name="parameter">添加参数</param>
        /// <returns>添加后的微应用配置</returns>
        public async Task<MicroConfigResponse> AddMicroAppConfig(MicroConfigAddParameter parameter)
        {
            await _microTableService.EnsureMicroConfigSubTablesColumnAsync();

            // 验证模型名称是否已存在
            var existingConfig = context.SysMicroAppConfig!
                .FirstOrDefault(c => c.ModelName == parameter.ModelName);
            if (existingConfig != null)
            {
                throw new Exception("模型名称已存在，请使用其他模型名称");
            }
        
            if (!string.IsNullOrWhiteSpace(parameter.MicroAppPath))
            {
                var existingPathConfig = context.SysMicroAppConfig!
                    .FirstOrDefault(c => c.ApiPrefix == parameter.MicroAppPath);
                if (existingPathConfig != null)
                {
                    throw new Exception("微应用路径已存在，请使用其他路径");
                }
            }

            var normalizedSubTables = MicroConfigSchema.NormalizeSubTables(parameter.SubTables);

            // 创建微应用配置对象
            var microConfig = new SysMicroAppConfig()
            {
                ItemId = YitterHelper.NewId(),
                ConfigName = parameter.ConfigName,
                ModelName = parameter.ModelName,
                ConfigDesc = parameter.ConfigDesc,
                Status = parameter.Status,
                SupportCreate = parameter.SupportCreate,
                SupportUpdate = parameter.SupportUpdate,
                SupportDelete = parameter.SupportDelete,
                SupportBatchDelete = parameter.SupportBatchDelete,
                SupportImport = parameter.SupportImport,
                SupportExport = parameter.SupportExport,
                ShowSubTablesInList = parameter.ShowSubTablesInList,
                DataScope = MicroConfigSchema.NormalizeDataScope(parameter.DataScope),
                FormColumns = MicroConfigSchema.NormalizeFormColumns(parameter.FormColumns),
                QueryColumns = MicroConfigSchema.NormalizeQueryColumns(parameter.QueryColumns),
                ApiPrefix = string.IsNullOrWhiteSpace(parameter.MicroAppPath) ? parameter.ModelName : parameter.MicroAppPath,
                Fields = JsonSerializer.Serialize(parameter.Fields),
                SubTables = JsonSerializer.Serialize(normalizedSubTables),
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };
        
            // 添加到数据库
            context.SysMicroAppConfig!.Add(microConfig);
            await context.SaveChangesAsync();

            dtSoftCache.RefreshCache(MicroConfigCacheKeys.ActiveConfig(microConfig.ModelName));
        
            // 确保对应的数据表存在
            await _microTableService.EnsureTableExistsAsync(microConfig);
        
            // 返回添加后的配置信息
            return MicroConfigSchema.ToResponse(
                microConfig,
                parameter.Fields,
                normalizedSubTables);
        }

        /// <summary>
        /// 更新微应用配置
        /// </summary>
        /// <param name="parameter">更新参数</param>
        /// <returns>更新后的微应用配置</returns>
        public async Task<MicroConfigResponse> UpdateMicroAppConfig(MicroConfigUpdateParameter parameter)
        {
            await _microTableService.EnsureMicroConfigSubTablesColumnAsync();

            // 查找要更新的配置
            var existingConfig = context.SysMicroAppConfig!
                .FirstOrDefault(c => c.ItemId == parameter.ItemId);
            if (existingConfig == null)
            {
                throw new Exception("未找到指定的微应用配置");
            }

            if (!string.Equals(existingConfig.ModelName, parameter.ModelName, StringComparison.Ordinal))
            {
                throw new Exception("数据模型创建后不允许修改");
            }

            var targetMicroAppPath = string.IsNullOrWhiteSpace(parameter.MicroAppPath)
                ? parameter.ModelName
                : parameter.MicroAppPath;

            var duplicatePathConfig = context.SysMicroAppConfig!
                .FirstOrDefault(c => c.ApiPrefix == targetMicroAppPath && c.ItemId != parameter.ItemId);
            if (duplicatePathConfig != null)
            {
                throw new Exception("微应用路径已存在，请使用其他路径");
            }

            var normalizedSubTables = MicroConfigSchema.NormalizeSubTables(parameter.SubTables);

            // 更新配置信息
            existingConfig.ConfigName = parameter.ConfigName;
            existingConfig.ConfigDesc = parameter.ConfigDesc;
            existingConfig.Status = parameter.Status;
            existingConfig.SupportCreate = parameter.SupportCreate;
            existingConfig.SupportUpdate = parameter.SupportUpdate;
            existingConfig.SupportDelete = parameter.SupportDelete;
            existingConfig.SupportBatchDelete = parameter.SupportBatchDelete;
            existingConfig.SupportImport = parameter.SupportImport;
            existingConfig.SupportExport = parameter.SupportExport;
            existingConfig.ShowSubTablesInList = parameter.ShowSubTablesInList;
            existingConfig.DataScope = MicroConfigSchema.NormalizeDataScope(parameter.DataScope);
            existingConfig.FormColumns = MicroConfigSchema.NormalizeFormColumns(parameter.FormColumns);
            existingConfig.QueryColumns = MicroConfigSchema.NormalizeQueryColumns(parameter.QueryColumns);
            existingConfig.ApiPrefix = targetMicroAppPath;
            existingConfig.Fields = JsonSerializer.Serialize(parameter.Fields);
            existingConfig.SubTables = JsonSerializer.Serialize(normalizedSubTables);
            existingConfig.UpdateTime = DateTime.Now;

            // 保存更改
            await context.SaveChangesAsync();

            dtSoftCache.RefreshCache(MicroConfigCacheKeys.ActiveConfig(existingConfig.ModelName));

            // 确保对应的数据表结构是最新的
            await _microTableService.EnsureTableExistsAsync(existingConfig);

            // 返回更新后的配置信息
            return MicroConfigSchema.ToResponse(
                existingConfig,
                parameter.Fields,
                normalizedSubTables);
        }

        /// <summary>
        /// 删除微应用配置
        /// </summary>
        /// <param name="parameter">删除参数</param>
        /// <returns>是否删除成功</returns>
        public async Task<bool> DeleteMicroAppConfig(MicroConfigDeleteParameter parameter)
        {
            await _microTableService.EnsureMicroConfigSubTablesColumnAsync();

            // 查找要删除的配置
            var existingConfig = context.SysMicroAppConfig!
                .FirstOrDefault(c => c.ItemId == parameter.ItemId);
            if (existingConfig == null)
            {
                throw new Exception("未找到指定的微应用配置");
            }

            var modelName = existingConfig.ModelName;
        
            // 从数据库中删除
            context.SysMicroAppConfig!.Remove(existingConfig);
            await context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(modelName))
            {
                dtSoftCache.RefreshCache(MicroConfigCacheKeys.ActiveConfig(modelName));
            }
        
            return true;
        }

    }
}
