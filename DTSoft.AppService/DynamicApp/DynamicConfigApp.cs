using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.DynamicApp;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DTSoft.AppService.DynamicApp
{
    public class DynamicConfigApp(SysDbContext context, DynamicTableService dynamicTableService, IDtSoftCache dtSoftCache)
    {
        private readonly DynamicTableService _dynamicTableService = dynamicTableService;

        /// <summary>
        /// 获取 CRUD 配置列表
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns>分页的 CRUD 配置列表</returns>
        public async Task<PagedResponse<CrudConfigResponse>> GetDynamicAppConfigs(CrudConfigQueryParameter parameter)
        {
            var query = context.SysDynamicAppConfig!.AsQueryable();
        
            // 模糊匹配配置名称、模型名称
            if (!string.IsNullOrEmpty(parameter.Keyword))
            {
                query = query.Where(c => c.ConfigName.Contains(parameter.Keyword) ||
                                         c.ModelName.Contains(parameter.Keyword));
            }
        
            // 按模块名称精确匹配
            if (!string.IsNullOrEmpty(parameter.ModelName))
            {
                query = query.Where(c => c.ModelName == parameter.ModelName);
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
            var result = new PagedResponse<CrudConfigResponse>
            {
                Data = configList.Select(c => new CrudConfigResponse
                {
                    ItemId = c.ItemId,
                    ConfigName = c.ConfigName,
                    ModelName = c.ModelName,
                    ConfigDesc = c.ConfigDesc,
                    Status = c.Status,
                    SupportCreate = c.SupportCreate,
                    SupportUpdate = c.SupportUpdate,
                    SupportDelete = c.SupportDelete,
                    SupportBatchDelete = c.SupportBatchDelete,
                    SupportImport = c.SupportImport,
                    SupportExport = c.SupportExport,
                    ApiPrefix = c.ApiPrefix,
                    Fields = string.IsNullOrEmpty(c.Fields) ? new List<FieldConfig>() :
                            JsonSerializer.Deserialize<List<FieldConfig>>(c.Fields),
                    CreateTime = c.CreateTime,
                    UpdateTime = c.UpdateTime
                }).ToList(),
                Total = total
            };
        
            return result;
        }

        /// <summary>
        /// 获取 CRUD 配置详情
        /// </summary>
        /// <param name="id">配置 ID</param>
        /// <returns>CRUD 配置详情</returns>
        public CrudConfigResponse GetCrudConfigById(long id)
        {
            var config = context.SysDynamicAppConfig!.FirstOrDefault(c => c.ItemId == id);
            if (config == null)
            {
                throw new Exception("未找到指定的 CRUD 配置");
            }

            return new CrudConfigResponse
            {
                ItemId = config.ItemId,
                ConfigName = config.ConfigName,
                ModelName = config.ModelName,
                ConfigDesc = config.ConfigDesc,
                Status = config.Status,
                SupportCreate = config.SupportCreate,
                SupportUpdate = config.SupportUpdate,
                SupportDelete = config.SupportDelete,
                SupportBatchDelete = config.SupportBatchDelete,
                SupportImport = config.SupportImport,
                SupportExport = config.SupportExport,
                ApiPrefix = config.ApiPrefix,
                Fields = string.IsNullOrEmpty(config.Fields) ? new List<FieldConfig>() :
                        JsonSerializer.Deserialize<List<FieldConfig>>(config.Fields),
                CreateTime = config.CreateTime,
                UpdateTime = config.UpdateTime
            };
        }

        /// <summary>
        /// 添加 CRUD 配置
        /// </summary>
        /// <param name="parameter">添加参数</param>
        /// <returns>添加后的 CRUD 配置</returns>
        public async Task<CrudConfigResponse> AddDynamicAppConfig(CrudConfigAddParameter parameter)
        {
            // 验证模型名称是否已存在
            var existingConfig = context.SysDynamicAppConfig!
                .FirstOrDefault(c => c.ModelName == parameter.ModelName);
            if (existingConfig != null)
            {
                throw new Exception("模型名称已存在，请使用其他模型名称");
            }
        
            // 创建 CRUD 配置对象
            var crudConfig = new SysDynamicAppConfig()
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
                ApiPrefix = parameter.ApiPrefix,
                Fields = JsonSerializer.Serialize(parameter.Fields),
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };
        
            // 添加到数据库
            context.SysDynamicAppConfig!.Add(crudConfig);
            await context.SaveChangesAsync();

            dtSoftCache.RefreshCache(DynamicConfigCacheKeys.ActiveConfig(crudConfig.ModelName));
        
            // 确保对应的数据表存在
            await _dynamicTableService.EnsureTableExistsAsync(crudConfig);
        
            // 返回添加后的配置信息
            return new CrudConfigResponse
            {
                ItemId = crudConfig.ItemId,
                ConfigName = crudConfig.ConfigName,
                ModelName = crudConfig.ModelName,
                ConfigDesc = crudConfig.ConfigDesc,
                Status = crudConfig.Status,
                SupportCreate = crudConfig.SupportCreate,
                SupportUpdate = crudConfig.SupportUpdate,
                SupportDelete = crudConfig.SupportDelete,
                SupportBatchDelete = crudConfig.SupportBatchDelete,
                SupportImport = crudConfig.SupportImport,
                SupportExport = crudConfig.SupportExport,
                ApiPrefix = crudConfig.ApiPrefix,
                Fields = parameter.Fields,
                CreateTime = crudConfig.CreateTime,
                UpdateTime = crudConfig.UpdateTime
            };
        }

        /// <summary>
        /// 更新 App 配置
        /// </summary>
        /// <param name="parameter">更新参数</param>
        /// <returns>更新后的 CRUD 配置</returns>
        public async Task<CrudConfigResponse> UpdateDynamicAppConfig(CrudConfigUpdateParameter parameter)
        {
            // 查找要更新的配置
            var existingConfig = context.SysDynamicAppConfig!
                .FirstOrDefault(c => c.ItemId == parameter.ItemId);
            if (existingConfig == null)
            {
                throw new Exception("未找到指定的 App 配置");
            }

            var oldModelName = existingConfig.ModelName;

            // 验证模型名称是否被其他配置使用
            var duplicateConfig = context.SysDynamicAppConfig!
                .FirstOrDefault(c => c.ModelName == parameter.ModelName && c.ItemId != parameter.ItemId);
            if (duplicateConfig != null)
            {
                throw new Exception("模型名称已存在，请使用其他模型名称");
            }

            // 更新配置信息
            existingConfig.ConfigName = parameter.ConfigName;
            existingConfig.ModelName = parameter.ModelName;
            existingConfig.ConfigDesc = parameter.ConfigDesc;
            existingConfig.Status = parameter.Status;
            existingConfig.SupportCreate = parameter.SupportCreate;
            existingConfig.SupportUpdate = parameter.SupportUpdate;
            existingConfig.SupportDelete = parameter.SupportDelete;
            existingConfig.SupportBatchDelete = parameter.SupportBatchDelete;
            existingConfig.SupportImport = parameter.SupportImport;
            existingConfig.SupportExport = parameter.SupportExport;
            existingConfig.ApiPrefix = parameter.ApiPrefix;
            existingConfig.Fields = JsonSerializer.Serialize(parameter.Fields);
            existingConfig.UpdateTime = DateTime.Now;

            // 保存更改
            await context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(oldModelName))
            {
                dtSoftCache.RefreshCache(DynamicConfigCacheKeys.ActiveConfig(oldModelName));
            }
            dtSoftCache.RefreshCache(DynamicConfigCacheKeys.ActiveConfig(existingConfig.ModelName));

            // 确保对应的数据表结构是最新的
            await _dynamicTableService.EnsureTableExistsAsync(existingConfig);

            // 返回更新后的配置信息
            return new CrudConfigResponse
            {
                ItemId = existingConfig.ItemId,
                ConfigName = existingConfig.ConfigName,
                ModelName = existingConfig.ModelName,
                ConfigDesc = existingConfig.ConfigDesc,
                Status = existingConfig.Status,
                SupportCreate = existingConfig.SupportCreate,
                SupportUpdate = existingConfig.SupportUpdate,
                SupportDelete = existingConfig.SupportDelete,
                SupportBatchDelete = existingConfig.SupportBatchDelete,
                SupportImport = existingConfig.SupportImport,
                SupportExport = existingConfig.SupportExport,
                ApiPrefix = existingConfig.ApiPrefix,
                Fields = parameter.Fields,
                CreateTime = existingConfig.CreateTime,
                UpdateTime = existingConfig.UpdateTime
            };
        }

        /// <summary>
        /// 删除 CRUD 配置
        /// </summary>
        /// <param name="parameter">删除参数</param>
        /// <returns>是否删除成功</returns>
        public async Task<bool> DeleteDynamicAppConfig(CrudConfigDeleteParameter parameter)
        {
            // 查找要删除的配置
            var existingConfig = context.SysDynamicAppConfig!
                .FirstOrDefault(c => c.ItemId == parameter.ItemId);
            if (existingConfig == null)
            {
                throw new Exception("未找到指定的 App 配置");
            }

            var modelName = existingConfig.ModelName;
        
            // 从数据库中删除
            context.SysDynamicAppConfig!.Remove(existingConfig);
            await context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(modelName))
            {
                dtSoftCache.RefreshCache(DynamicConfigCacheKeys.ActiveConfig(modelName));
            }
        
            return true;
        }
    }
}
