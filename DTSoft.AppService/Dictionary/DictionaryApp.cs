using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Dictionary;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace DTSoft.AppService.Dictionary;

public class DictionaryApp(SysDbContext dbContext, IDtSoftCache dtSoftCache)
{
    private static string DictionaryItemsCacheKey(string dictCode) => $"Dictionary:Items:{dictCode.Trim().ToLowerInvariant()}";

    public async Task<JsonObject> GetTypesAsync(DictionaryTypeQuery query)
    {
        var dataQuery = dbContext.SysDictionaryType!.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dataQuery = dataQuery.Where(type =>
                type.DictCode.Contains(keyword) ||
                type.DictName.Contains(keyword) ||
                (type.Description != null && type.Description.Contains(keyword)));
        }

        if (query.Enabled.HasValue)
        {
            dataQuery = dataQuery.Where(type => type.Enabled == query.Enabled.Value);
        }

        var rows = await dataQuery
            .OrderBy(type => type.Sort)
            .ThenBy(type => type.DictCode)
            .Select(type => new
            {
                type.ItemId,
                type.DictCode,
                type.DictName,
                type.Description,
                type.Enabled,
                type.Sort,
                type.CreateTime,
                type.UpdateTime,
                ItemCount = dbContext.SysDictionaryData!.Count(item => item.DictCode == type.DictCode)
            })
            .ToListAsync();

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = new JsonArray(rows.Select(row => new JsonObject
            {
                ["ItemId"] = row.ItemId,
                ["DictCode"] = row.DictCode,
                ["DictName"] = row.DictName,
                ["Description"] = row.Description,
                ["Enabled"] = row.Enabled,
                ["Sort"] = row.Sort,
                ["CreateTime"] = row.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                ["UpdateTime"] = row.UpdateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                ["ItemCount"] = row.ItemCount
            }).ToArray())
        };
    }

    public async Task<JsonObject> SaveTypeAsync(DictionaryTypeDto dto)
    {
        var normalizedCode = (dto.DictCode ?? string.Empty).Trim();
        var normalizedName = (dto.DictName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode) || string.IsNullOrWhiteSpace(normalizedName))
            return Error("字典编码和名称不能为空");

        var duplicateExists = await dbContext.SysDictionaryType!
            .AnyAsync(type => type.DictCode == normalizedCode && type.ItemId != (dto.ItemId ?? 0));
        if (duplicateExists)
            return Error("字典编码已存在");

        var now = TimeUtil.CstDateTime;
        if (dto.ItemId.HasValue && dto.ItemId.Value > 0)
        {
            var type = await dbContext.SysDictionaryType!.FirstOrDefaultAsync(type => type.ItemId == dto.ItemId.Value);
            if (type == null)
                return Error("字典不存在");

            var oldCode = type.DictCode;
            type.DictCode = normalizedCode;
            type.DictName = normalizedName;
            type.Description = dto.Description;
            type.Enabled = dto.Enabled;
            type.Sort = dto.Sort;
            type.UpdateTime = now;

            if (!string.Equals(oldCode, normalizedCode, StringComparison.Ordinal))
            {
                var items = await dbContext.SysDictionaryData!.Where(item => item.DictCode == oldCode).ToListAsync();
                foreach (var item in items)
                {
                    item.DictCode = normalizedCode;
                    item.UpdateTime = now;
                }

                dtSoftCache.RefreshCache(DictionaryItemsCacheKey(oldCode));
            }
        }
        else
        {
            dbContext.SysDictionaryType!.Add(new SysDictionaryType
            {
                ItemId = YitterHelper.NewId(),
                DictCode = normalizedCode,
                DictName = normalizedName,
                Description = dto.Description,
                Enabled = dto.Enabled,
                Sort = dto.Sort,
                CreateTime = now,
                UpdateTime = now
            });
        }

        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache(DictionaryItemsCacheKey(normalizedCode));
        return Success("保存成功");
    }

    public async Task<JsonObject> DeleteTypeAsync(long itemId)
    {
        var type = await dbContext.SysDictionaryType!.FirstOrDefaultAsync(type => type.ItemId == itemId);
        if (type == null)
            return Error("字典不存在");

        var items = await dbContext.SysDictionaryData!.Where(item => item.DictCode == type.DictCode).ToListAsync();
        dbContext.SysDictionaryData!.RemoveRange(items);
        dbContext.SysDictionaryType!.Remove(type);
        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache(DictionaryItemsCacheKey(type.DictCode));
        return Success("删除成功");
    }

    public async Task<JsonObject> SortTypesAsync(DictionaryTypeSortRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return Error("排序数据不能为空");

        if (request.Items.Select(item => item.ItemId).Distinct().Count() != request.Items.Count)
            return Error("排序数据存在重复项");

        var sortMap = request.Items.ToDictionary(item => item.ItemId, item => item.Sort);
        var itemIds = sortMap.Keys.ToList();
        var types = await dbContext.SysDictionaryType!
            .Where(type => itemIds.Contains(type.ItemId))
            .ToListAsync();
        if (types.Count != request.Items.Count)
            return Error("排序数据已变更，请刷新后重试");

        var now = TimeUtil.CstDateTime;
        foreach (var type in types)
        {
            type.Sort = sortMap[type.ItemId];
            type.UpdateTime = now;
        }

        await dbContext.SaveChangesAsync();
        return Success("排序保存成功");
    }

    public async Task<JsonObject> GetItemsAsync(DictionaryItemQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.DictCode))
            return Error("字典编码不能为空");

        var normalizedCode = (query.DictCode ?? string.Empty).Trim();
        var dataQuery = dbContext.SysDictionaryData!
            .AsNoTracking()
            .Where(item => item.DictCode == normalizedCode);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dataQuery = dataQuery.Where(item =>
                item.ItemLabel.Contains(keyword) ||
                item.ItemValue.Contains(keyword) ||
                (item.Remark != null && item.Remark.Contains(keyword)));
        }

        if (query.Enabled.HasValue)
        {
            dataQuery = dataQuery.Where(item => item.Enabled == query.Enabled.Value);
        }

        var rows = await dataQuery
            .OrderBy(item => item.Sort)
            .ThenBy(item => item.ItemLabel)
            .ToListAsync();

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = new JsonArray(rows.Select(item => new JsonObject
            {
                ["ItemId"] = item.ItemId,
                ["DictTypeId"] = item.DictTypeId,
                ["DictCode"] = item.DictCode,
                ["ItemLabel"] = item.ItemLabel,
                ["ItemValue"] = item.ItemValue,
                ["TagType"] = item.TagType,
                ["Remark"] = item.Remark,
                ["Enabled"] = item.Enabled,
                ["Sort"] = item.Sort,
                ["CreateTime"] = item.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                ["UpdateTime"] = item.UpdateTime.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToArray())
        };
    }

    public async Task<JsonObject> GetEnabledItemsByCodeAsync(string dictCode)
    {
        if (string.IsNullOrWhiteSpace(dictCode))
            return Error("字典编码不能为空");

        var normalizedCode = dictCode.Trim();
        var cacheKey = DictionaryItemsCacheKey(normalizedCode);
        var cached = await dtSoftCache.GetAsync<string>(cacheKey);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            return new JsonObject
            {
                ["success"] = true,
                ["StateCode"] = 0,
                ["data"] = JsonNode.Parse(cached) as JsonArray ?? new JsonArray()
            };
        }

        var rows = await dbContext.SysDictionaryData!
            .AsNoTracking()
            .Where(item => item.DictCode == normalizedCode && item.Enabled)
            .OrderBy(item => item.Sort)
            .ThenBy(item => item.ItemLabel)
            .Select(item => new
            {
                item.ItemLabel,
                item.ItemValue,
                item.TagType
            })
            .ToListAsync();

        var data = new JsonArray(rows.Select(item => new JsonObject
        {
            ["Label"] = item.ItemLabel,
            ["Value"] = item.ItemValue,
            ["TagType"] = item.TagType
        }).ToArray());

        await dtSoftCache.SetAsync(cacheKey, data.ToJsonString(), TimeSpan.FromMinutes(10));

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = data
        };
    }

    public async Task<JsonObject> SaveItemAsync(DictionaryItemDto dto)
    {
        var normalizedCode = (dto.DictCode ?? string.Empty).Trim();
        var label = (dto.ItemLabel ?? string.Empty).Trim();
        var value = (dto.ItemValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode) || string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value))
            return Error("字典编码、标签和值不能为空");

        var type = await dbContext.SysDictionaryType!.FirstOrDefaultAsync(type => type.DictCode == normalizedCode);
        if (type == null)
            return Error("字典不存在");

        var duplicateExists = await dbContext.SysDictionaryData!
            .AnyAsync(item => item.DictCode == normalizedCode && item.ItemValue == value && item.ItemId != (dto.ItemId ?? 0));
        if (duplicateExists)
            return Error("字典值已存在");

        var now = TimeUtil.CstDateTime;
        if (dto.ItemId.HasValue && dto.ItemId.Value > 0)
        {
            var item = await dbContext.SysDictionaryData!.FirstOrDefaultAsync(item => item.ItemId == dto.ItemId.Value);
            if (item == null)
                return Error("字典项不存在");

            item.DictTypeId = type.ItemId;
            item.DictCode = normalizedCode;
            item.ItemLabel = label;
            item.ItemValue = value;
            item.TagType = dto.TagType;
            item.Remark = dto.Remark;
            item.Enabled = dto.Enabled;
            item.Sort = dto.Sort;
            item.UpdateTime = now;
        }
        else
        {
            dbContext.SysDictionaryData!.Add(new SysDictionaryData
            {
                ItemId = YitterHelper.NewId(),
                DictTypeId = type.ItemId,
                DictCode = normalizedCode,
                ItemLabel = label,
                ItemValue = value,
                TagType = dto.TagType,
                Remark = dto.Remark,
                Enabled = dto.Enabled,
                Sort = dto.Sort,
                CreateTime = now,
                UpdateTime = now
            });
        }

        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache(DictionaryItemsCacheKey(normalizedCode));
        return Success("保存成功");
    }

    public async Task<JsonObject> DeleteItemAsync(long itemId)
    {
        var item = await dbContext.SysDictionaryData!.FirstOrDefaultAsync(item => item.ItemId == itemId);
        if (item == null)
            return Error("字典项不存在");

        var dictCode = item.DictCode;
        dbContext.SysDictionaryData!.Remove(item);
        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache(DictionaryItemsCacheKey(dictCode));
        return Success("删除成功");
    }

    public async Task<JsonObject> SortItemsAsync(DictionaryItemSortRequest request)
    {
        var normalizedCode = (request.DictCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
            return Error("字典编码不能为空");

        if (request.Items == null || request.Items.Count == 0)
            return Error("排序数据不能为空");

        if (request.Items.Select(item => item.ItemId).Distinct().Count() != request.Items.Count)
            return Error("排序数据存在重复项");

        var sortMap = request.Items.ToDictionary(item => item.ItemId, item => item.Sort);
        var itemIds = sortMap.Keys.ToList();
        var items = await dbContext.SysDictionaryData!
            .Where(item => item.DictCode == normalizedCode && itemIds.Contains(item.ItemId))
            .ToListAsync();
        if (items.Count != request.Items.Count)
            return Error("排序数据已变更，请刷新后重试");

        var now = TimeUtil.CstDateTime;
        foreach (var item in items)
        {
            item.Sort = sortMap[item.ItemId];
            item.UpdateTime = now;
        }

        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache(DictionaryItemsCacheKey(normalizedCode));
        return Success("排序保存成功");
    }

    private static JsonObject Success(string message) => new()
    {
        ["success"] = true,
        ["StateCode"] = 0,
        ["Msg"] = message
    };

    private static JsonObject Error(string message) => new()
    {
        ["success"] = false,
        ["StateCode"] = 0,
        ["Msg"] = message
    };
}
