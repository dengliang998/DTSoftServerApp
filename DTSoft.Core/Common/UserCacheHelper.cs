using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DTSoft.Core.Common;

/// <summary>
/// 用户缓存辅助类
/// </summary>
public class UserCacheHelper(IDtSoftCache dtSoftCache, SysDbContext dbContext)
{
    private const string UserListCacheKey = "SYS_USER";
    private const string UserKeyPrefix = "SYS_USER:";
    private static readonly TimeSpan UserCacheDuration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 获取所有用户缓存数据
    /// </summary>
    /// <returns>用户列表</returns>
    public async Task<List<SysUser>> GetAllUsersAsync()
    {
        return await dtSoftCache.GetOrCreateAsync(UserListCacheKey, UserCacheDuration, () => dbContext.SysUser.AsNoTracking().ToList());
    }

    private static string GetUserCacheKey(string account) => $"{UserKeyPrefix}{account}";

    /// <summary>
    /// 根据账号获取单个用户
    /// </summary>
    /// <param name="account">用户账号</param>
    /// <returns>用户对象，如果不存在返回null</returns>
    public async Task<SysUser?> GetUserByAccountAsync(string? account)
    {
        if (string.IsNullOrEmpty(account))
            return null;

        var cacheKey = GetUserCacheKey(account);
        var cached = await dtSoftCache.GetAsync<SysUser>(cacheKey);
        if (cached is not null)
            return cached;

        var user = await dbContext.SysUser.AsNoTracking().FirstOrDefaultAsync(u => u.Account == account);
        if (user is not null)
        {
            await dtSoftCache.SetAsync(cacheKey, user, UserCacheDuration);
        }

        return user;
    }

    /// <summary>
    /// 根据账号列表获取用户列表
    /// </summary>
    /// <param name="accounts">账号列表</param>
    /// <returns>匹配的用户列表</returns>
    public async Task<List<SysUser>> GetUsersByAccountsAsync(List<string?> accounts)
    {
        var accountList = accounts
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (accountList.Count == 0)
            return [];

        var accountKeyMap = accountList.ToDictionary(a => a, GetUserCacheKey, StringComparer.OrdinalIgnoreCase);
        var cached = await dtSoftCache.GetMultipleAsync<SysUser>(accountKeyMap.Values);

        var result = new List<SysUser>(accountList.Count);
        var missingAccounts = new List<string>();

        foreach (var account in accountList)
        {
            var key = accountKeyMap[account];
            if (cached.TryGetValue(key, out var cachedUser) && cachedUser is not null)
            {
                result.Add(cachedUser);
            }
            else
            {
                missingAccounts.Add(account);
            }
        }

        if (missingAccounts.Count > 0)
        {
            var dbUsers = await dbContext.SysUser
                .AsNoTracking()
                .Where(u => u.Account != null && missingAccounts.Contains(u.Account))
                .ToListAsync();

            result.AddRange(dbUsers);

            var toCache = dbUsers
                .Where(u => !string.IsNullOrEmpty(u.Account))
                .ToDictionary(u => GetUserCacheKey(u.Account!), u => u);

            if (toCache.Count > 0)
            {
                await dtSoftCache.SetMultipleAsync(toCache, UserCacheDuration);
            }
        }

        return result;
    }

    /// <summary>
    /// 刷新用户缓存（按账号精确失效）
    /// </summary>
    public async Task RefreshUserCacheAsync(string? account = null)
    {
        if (!string.IsNullOrWhiteSpace(account))
        {
            await dtSoftCache.RemoveAsync(GetUserCacheKey(account));
        }

        // 保留旧的“全量用户列表”缓存失效能力（如果未来还用到）
        dtSoftCache.RefreshCache(UserListCacheKey);
    }
}
