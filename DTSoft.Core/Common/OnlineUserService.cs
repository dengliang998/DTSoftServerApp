using DTSoft.Core.Interfaces;

namespace DTSoft.Core.Common;

public sealed class OnlineUserInfo
{
    public string Account { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime LastActiveTime { get; set; }
}

public class OnlineUserService(IDtSoftCache dtSoftCache, UserCacheHelper userCacheHelper)
{
    private const string OnlineUsersCacheKey = "SYS_ONLINE_USERS";
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    public async Task MarkActiveAsync(string? account)
    {
        if (string.IsNullOrWhiteSpace(account))
            return;

        var normalizedAccount = account.Trim();
        var user = await userCacheHelper.GetUserByAccountAsync(normalizedAccount);
        if (user == null || user.Disable)
            return;

        var now = TimeUtil.CstDateTime;
        var displayName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? normalizedAccount
            : user.DisplayName!;

        await SyncLock.WaitAsync();
        try
        {
            var onlineUsers = await GetOnlineUserIndexAsync();
            RemoveExpiredUsers(onlineUsers, now);

            onlineUsers[normalizedAccount] = new OnlineUserInfo
            {
                Account = user.Account ?? normalizedAccount,
                DisplayName = displayName,
                LastActiveTime = now
            };

            await dtSoftCache.SetAsync(OnlineUsersCacheKey, onlineUsers, CacheDuration);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    public async Task<List<OnlineUserInfo>> GetOnlineUsersAsync()
    {
        var now = TimeUtil.CstDateTime;

        await SyncLock.WaitAsync();
        try
        {
            var onlineUsers = await GetOnlineUserIndexAsync();
            var changed = RemoveExpiredUsers(onlineUsers, now);
            if (changed)
            {
                await dtSoftCache.SetAsync(OnlineUsersCacheKey, onlineUsers, CacheDuration);
            }

            return onlineUsers.Values
                .OrderByDescending(user => user.LastActiveTime)
                .ToList();
        }
        finally
        {
            SyncLock.Release();
        }
    }

    public async Task RemoveAsync(string? account)
    {
        if (string.IsNullOrWhiteSpace(account))
            return;

        await SyncLock.WaitAsync();
        try
        {
            var onlineUsers = await GetOnlineUserIndexAsync();
            if (onlineUsers.Remove(account.Trim()))
            {
                await dtSoftCache.SetAsync(OnlineUsersCacheKey, onlineUsers, CacheDuration);
            }
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private async Task<Dictionary<string, OnlineUserInfo>> GetOnlineUserIndexAsync()
    {
        var onlineUsers = await dtSoftCache.GetAsync<Dictionary<string, OnlineUserInfo>>(OnlineUsersCacheKey);
        return onlineUsers == null
            ? new Dictionary<string, OnlineUserInfo>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, OnlineUserInfo>(onlineUsers, StringComparer.OrdinalIgnoreCase);
    }

    private static bool RemoveExpiredUsers(Dictionary<string, OnlineUserInfo> onlineUsers, DateTime now)
    {
        var threshold = now - OnlineWindow;
        var expiredAccounts = onlineUsers
            .Where(item => item.Value.LastActiveTime < threshold)
            .Select(item => item.Key)
            .ToList();

        foreach (var account in expiredAccounts)
        {
            onlineUsers.Remove(account);
        }

        return expiredAccounts.Count > 0;
    }
}
