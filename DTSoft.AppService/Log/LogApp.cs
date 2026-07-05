using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Log;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace DTSoft.AppService.Log;

public class LogApp(SysDbContext dbContext, UserCacheHelper userCacheHelper)
{
    /// <summary>
    /// 获取日志记录列表
    /// </summary>
    public async Task<JsonObject> GetLogActionListAsync(LogAction obj)
    {
        // 先查询日志数据，然后手动关联用户显示名
        IQueryable<SysActionLog> logs = (dbContext.SysActionLog ?? throw new InvalidOperationException("SysActionLog 数据集未初始化"))
            .AsNoTracking();
    
        // 筛选条件
        if (obj.LogDateStart.HasValue)
            logs = logs.Where(b => b.LogDate >= obj.LogDateStart.Value);

        if (obj.LogDateEnd.HasValue)
        {
            var logDateEnd = obj.LogDateEnd.Value;
            if (logDateEnd.TimeOfDay == TimeSpan.Zero)
            {
                var exclusiveEndDate = logDateEnd.Date.AddDays(1);
                logs = logs.Where(b => b.LogDate < exclusiveEndDate);
            }
            else
            {
                logs = logs.Where(b => b.LogDate <= logDateEnd);
            }
        }

        var userAcc = NormalizeKeyword(obj.UserAcc);
        if (userAcc != null)
        {
            var matchedUserAccounts = await GetMatchedUserAccountsAsync(userAcc);
            logs = matchedUserAccounts.Count > 0
                ? logs.Where(b => b.UserAcc != null && (b.UserAcc.Contains(userAcc) || matchedUserAccounts.Contains(b.UserAcc)))
                : logs.Where(b => b.UserAcc != null && b.UserAcc.Contains(userAcc));
        }

        var clientIp = NormalizeKeyword(obj.ClientIP);
        if (clientIp != null)
            logs = logs.Where(b => b.ClientIP != null && b.ClientIP.Contains(clientIp));

        var actionName = NormalizeKeyword(obj.ActionName);
        if (actionName != null)
            logs = logs.Where(b => b.ActionName != null && b.ActionName.Contains(actionName));

        var param = NormalizeKeyword(obj.Param);
        if (param != null)
            logs = logs.Where(b => b.Param != null && b.Param.Contains(param));

        var result = NormalizeKeyword(obj.Result);
        if (result != null)
            logs = logs.Where(b => b.Result != null && b.Result.Contains(result));

        var keyword = NormalizeKeyword(obj.Keyword);
        if (keyword != null)
        {
            var matchedUserAccounts = await GetMatchedUserAccountsAsync(keyword);
            logs = matchedUserAccounts.Count > 0
                ? logs.Where(b =>
                    (b.UserAcc != null && (b.UserAcc.Contains(keyword) || matchedUserAccounts.Contains(b.UserAcc))) ||
                    (b.ClientIP != null && b.ClientIP.Contains(keyword)) ||
                    (b.ActionName != null && b.ActionName.Contains(keyword)) ||
                    (b.Param != null && b.Param.Contains(keyword)) ||
                    (b.Result != null && b.Result.Contains(keyword)))
                : logs.Where(b =>
                    (b.UserAcc != null && b.UserAcc.Contains(keyword)) ||
                    (b.ClientIP != null && b.ClientIP.Contains(keyword)) ||
                    (b.ActionName != null && b.ActionName.Contains(keyword)) ||
                    (b.Param != null && b.Param.Contains(keyword)) ||
                    (b.Result != null && b.Result.Contains(keyword)));
        }
    
        // 应用排序（必须在分页之前）
        var orderedLogs = logs.OrderByDescending(x => x.ItemId);
        var total = await orderedLogs.CountAsync();
    
        // 应用分页
        var pagedLogs = await orderedLogs.Skip(obj.PageSize * (obj.PageNum - 1)).Take(obj.PageSize).ToListAsync();
    
        // 提取所有需要的用户账号（排除 null 值）
        var userAccounts = pagedLogs.Where(l => l.UserAcc != null).Select(l => l.UserAcc).Distinct().ToList();
        var users = await userCacheHelper.GetUsersByAccountsAsync(userAccounts);
        var userLookup = users
            .Where(u => !string.IsNullOrEmpty(u.Account))
            .GroupBy(u => u.Account!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DisplayName ?? g.Key, StringComparer.OrdinalIgnoreCase);
    
        var children = new JsonArray();
        foreach (var log in pagedLogs)
        {
            var item = new JsonObject
            {
                ["LogDate"] = log.LogDate.ToString("yyyy-MM-dd HH:mm:ss")
            };
    
            // 从缓存中查找对应的用户显示名
            if (log.UserAcc != null && userLookup.TryGetValue(log.UserAcc, out var displayName))
            {
                item["UserAcc"] = displayName;
            }
            else
            {
                item["UserAcc"] = log.UserAcc;
            }
    
            item["ActionName"] = log.ActionName;
            item["ClientIP"] = log.ClientIP;
            item["Param"] = log.Param;
            item["RequestType"] = log.RequestType;
            item["Result"] = log.Result;
                
            children.Add(item);
        }
    
        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["Total"] = total,
            ["data"] = children
        };
    }

    private async Task<List<string>> GetMatchedUserAccountsAsync(string keyword)
    {
        return await dbContext.SysUser
            .AsNoTracking()
            .Where(u =>
                (u.Account != null && u.Account.Contains(keyword)) ||
                (u.DisplayName != null && u.DisplayName.Contains(keyword)))
            .Select(u => u.Account!)
            .ToListAsync();
    }

    private static string? NormalizeKeyword(string? value)
    {
        value = value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
