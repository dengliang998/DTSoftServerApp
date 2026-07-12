using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Plugin.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Nodes;

namespace DTSoft.Core.Common;

/// <summary>
/// DTSoft系统类
/// </summary>
public sealed class DtSoftHelper(SysDbContext dbContext, UserCacheHelper userCacheHelper, IDtSoftCache dtSoftCache) : IDtSoftHelper
{
    private static string IsAdminCacheKey(string userAcc) => $"Auth:IsAdmin:{userAcc.Trim().ToLowerInvariant()}";
    private static string RoleNameCacheKey(long roleId) => $"Role:Name:{roleId}";
    private static string InRoleCacheKey(long roleId, string userAcc) => $"Auth:InRole:{roleId}:{userAcc.Trim().ToLowerInvariant()}";

    /// <summary>
    /// 判断用户是否具有管理员权限
    /// </summary>
    /// <param name="userAcc"></param>
    /// <returns></returns>
    public bool IsAdmin(string userAcc)
    {
        if (string.IsNullOrWhiteSpace(userAcc)) return false;

        var cached = dtSoftCache.GetAsync<string>(IsAdminCacheKey(userAcc)).GetAwaiter().GetResult();
        if (cached == "1") return true;
        if (cached == "0") return false;

        // 查询用户所属的角色，并检查是否包含 "Administrator" 角色
        var roleMembers = dbContext.SysRoleMember!
            .Where(b => b.UserAcc == userAcc)
            .ToList();
        
        if (!roleMembers.Any())
        {
            return false;
        }
        
        // 获取所有角色 ID
        var roleIds = roleMembers.Select(rm => rm.RoleId).ToList();
        
        // 检查这些角色中是否有 Administrator 角色
        var adminRole = dbContext.SysRole!
            .FirstOrDefault(r => r.RoleName == "Administrator" && roleIds.Contains(r.ItemId));
        
        var isAdmin = adminRole != null;
        dtSoftCache.SetAsync(IsAdminCacheKey(userAcc), isAdmin ? "1" : "0", TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        return isAdmin;
    }

    /// <summary>
    /// 获取角色名称
    /// </summary>
    /// <param name="roleId">角色编号</param>
    /// <returns></returns>
    public string? GetRoleName(long roleId)
    {
        var cached = dtSoftCache.GetAsync<string>(RoleNameCacheKey(roleId)).GetAwaiter().GetResult();
        if (cached is not null) return cached;

        var name = dbContext.SysRole!
            .AsNoTracking()
            .Where(b => b.ItemId == roleId)
            .Select(b => b.RoleName)
            .FirstOrDefault() ?? string.Empty;

        dtSoftCache.SetAsync(RoleNameCacheKey(roleId), name, TimeSpan.FromMinutes(10)).GetAwaiter().GetResult();
        return name;
    }

    /// <summary>
    /// 账号是否包含在角色内
    /// </summary>
    /// <param name="roleId">角色编号</param>
    /// <param name="userAcc">账号</param>
    /// <returns></returns>
    public bool IsContainRole(long roleId, string userAcc)
    {
        if (string.IsNullOrWhiteSpace(userAcc)) return false;

        var cached = dtSoftCache.GetAsync<string>(InRoleCacheKey(roleId, userAcc)).GetAwaiter().GetResult();
        if (cached == "1") return true;
        if (cached == "0") return false;

        // 检查用户是否属于指定的角色
        var roleMember = dbContext.SysRoleMember!
            .FirstOrDefault(rm => rm.UserAcc == userAcc && rm.RoleId == roleId);

        var inRole = roleMember != null;
        dtSoftCache.SetAsync(InRoleCacheKey(roleId, userAcc), inRole ? "1" : "0", TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        return inRole;
    }

    /// <summary>
    /// 获取当前登录的用户账号（基于JWT令牌的新方法）
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public string GetLoginUserAccountFromJwt(string token)
    {
        try
        {
            // 解析JWT令牌获取用户信息
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            var accountId = jsonToken.Claims.FirstOrDefault(claim =>
                claim.Type == ClaimTypes.NameIdentifier ||
                claim.Type == JwtRegisteredClaimNames.NameId ||
                claim.Type == JwtRegisteredClaimNames.Sub ||
                claim.Type == JwtRegisteredClaimNames.UniqueName ||
                claim.Type == ClaimTypes.Name ||
                claim.Type == "name")?.Value;
            return accountId ?? "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    /// <summary>
    /// 获取当前登录的用户账号
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public static string GetLoginUserAccount(ClaimsPrincipal user) => user.Claims.FirstOrDefault(a => a.Type == ClaimTypes.NameIdentifier)!.Value;

    /// <summary>
    /// 检查账号是否被禁用
    /// </summary>
    /// <param name="userAcc"></param>
    /// <returns></returns>
    public async Task<JsonObject> CheckAccStatus(string userAcc)
    {
        var rv = new JsonObject();

        if (string.IsNullOrEmpty(userAcc))
        {
            rv["StateCode"] = 0;
            rv["success"] = false;
            rv["Msg"] = "账号不能为空";
            return rv;
        }

        var user = await userCacheHelper.GetUserByAccountAsync(userAcc);
        if ((user is { Disable: true }).Equals(true))
        {
            rv["StateCode"] = 0;
            rv["success"] = false;
            rv["Msg"] = "请求失败，账号已被禁用！";
        }
        else
        {
            rv["StateCode"] = 0;
            rv["success"] = true;
            rv["Msg"] = "";
        }

        return rv;
    }
}
