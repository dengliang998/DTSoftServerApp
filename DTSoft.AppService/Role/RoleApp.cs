using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Base;
using DTSoft.Models.Parameter.Role;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace DTSoft.AppService.Role;

public class RoleApp(SysDbContext dbContext, DtSoftHelper dtSoftHelper, UserCacheHelper userCacheHelper)
{
    /// <summary>
    /// 获取角色列表
    /// </summary>
    public JsonObject GetRoleList(Para obj)
    {
        IQueryable<SysRole>? sysRole = dbContext.SysRole;
        
        if (sysRole == null)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "系统错误",
                ["data"] = new JsonArray(),
                ["Total"] = 0
            };
        }
        
        if (!string.IsNullOrEmpty(obj.Keyword))
        {
            sysRole = sysRole.Where(b => b.RoleName!.Contains(obj.Keyword));
        }
        
        var total = sysRole.Count();
        var result = sysRole.OrderBy(o => o.ItemId)
                           .Skip(obj.PageSize * (obj.PageNum - 1))
                           .Take(obj.PageSize);

        var children = new JsonArray();
        foreach (var rows in result)
        {
            children.Add(new JsonObject
            {
                ["id"] = rows.ItemId,
                ["RoleName"] = rows.RoleName
            });
        }

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["Total"] = total,
            ["data"] = children
        };
    }

    /// <summary>
    /// 获取角色
    /// </summary>
    public async Task<JsonObject> GetRoleAsync(long RoleId)
    {
        if (dbContext.SysRole == null)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "系统错误"
            };
        }
        
        var sysRole = await dbContext.SysRole.Where(b => b.ItemId.Equals(RoleId)).FirstOrDefaultAsync();
        
        if (sysRole != null)
        {
            return new JsonObject
            {
                ["success"] = true,
                ["StateCode"] = 0,
                ["id"] = sysRole.ItemId,
                ["RoleName"] = sysRole.RoleName
            };
        }
        else
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "角色不存在"
            };
        }
    }

    /// <summary>
    /// 创建角色
    /// </summary>
    /// <param name="role"></param>
    /// <returns></returns>
    public async Task<JsonObject> CreateRole(RoleBase role)
    {
        JsonObject rv = new()
        {
            ["StateCode"] = 0,
            ["success"] = true
        };

        if (string.IsNullOrEmpty(role.RoleName))
        {
            rv["success"] = false;
            rv["Msg"] = "角色名称不能为空";
            return rv;
        }
        var data = dbContext.SysRole!.Where(b => b.RoleName == role.RoleName.Trim());
        if (!data.Any())
        {
            dbContext.SysRole!.Add(new SysRole { ItemId = YitterHelper.NewId(), RoleName = role.RoleName });
            await dbContext.SaveChangesAsync();
            rv["Msg"] = "角色创建成功";
        }
        else
        {
            rv["success"] = false;
            rv["Msg"] = $"角色：{role.RoleName}已存在";
        }
        
        return rv;
    }

    /// <summary>
    /// 修改角色信息
    /// </summary>
    /// <param name="role"></param>
    /// <param name="loginUserAcc"></param>
    /// <returns></returns>
    public async Task<JsonObject> ModifyRoleInfo(ModifyRole role, string loginUserAcc)
    {
        JsonObject rv = new()
        {
            ["StateCode"] = 0,
            ["success"] = true
        };

        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            rv["success"] = false;
            rv["Msg"] = "该账号没有修改的权限";
            return rv;
        }
        
        // 检查是否为系统角色（Administrator 或 Everyone）
        var targetRole = dbContext.SysRole!.FirstOrDefault(r => r.ItemId == role.ItemId);
        if (targetRole != null && (targetRole.RoleName == "Administrator" || targetRole.RoleName == "Everyone"))
        {
            rv["success"] = false;
            rv["Msg"] = "系统角色不能修改";
            return rv;
        }
        if (string.IsNullOrEmpty(role.RoleName))
        {
            rv["success"] = false;
            rv["Msg"] = "角色名称不能为空";
            return rv;
        }
        //查询角色是否存在
        var sysRole = dbContext.SysRole;
        if (!sysRole!.Any(p => p.RoleName == role.RoleName))
        {
            //更新角色
            var data = dbContext.SysRole!.FirstOrDefault(p => p.ItemId == role.ItemId);
            if (data != null)
            {
                data.RoleName = role.RoleName;
                await dbContext.SaveChangesAsync();
                rv["Msg"] = "修改成功";
            }
            else
            {
                rv["success"] = false;
                rv["Msg"] = "角色不存在";
            }
        }
        else
        {
            rv["success"] = false;
            rv["Msg"] = "角色名称重复或者未更新";
        }
        
        return rv;
    }

    /// <summary>
    /// 删除角色
    /// </summary>
    /// <param name="roleId"></param>
    /// <param name="loginUserAcc"></param>
    /// <returns></returns>
    public async Task<JsonObject> DelRole(long roleId, string loginUserAcc)
    {
        JsonObject rv = new()
        {
            ["StateCode"] = 0,
            ["success"] = true
        };

        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            rv["success"] = false;
            rv["Msg"] = "该账号没有删除权限";
            return rv;
        }
        
        // 检查是否为系统角色（Administrator 或 Everyone）
        var targetRole = await dbContext.SysRole!
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ItemId == roleId);
        if (targetRole != null && (targetRole.RoleName == "Administrator" || targetRole.RoleName == "Everyone"))
        {
            rv["success"] = false;
            rv["Msg"] = "系统角色不能删除";
            return rv;
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await dbContext.SysMenuAuthority!
                .Where(p => p.RoleID == roleId)
                .ExecuteDeleteAsync();

            await dbContext.SysRoleMember!
                .Where(p => p.RoleId == roleId)
                .ExecuteDeleteAsync();

            await dbContext.SysRole!
                .Where(p => p.ItemId == roleId)
                .ExecuteDeleteAsync();

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        rv["Msg"] = "删除成功";
        
        return rv;
    }

    /// <summary>
    /// 获取角色成员列表
    /// </summary>
    /// <param name="roleId"></param>
    /// <returns></returns>
    public async Task<JsonObject> GetRoleMemberListAsync(long roleId)
    {
        JsonObject rv = new();
        JsonArray children = new();
        rv["StateCode"] = 0;
        rv["success"] = true;

        // 查询角色成员，然后手动关联用户显示名
        var roleMembers = dbContext.SysRoleMember!.Where(b => b.RoleId == roleId);

        // 提取所有需要的用户账号
        var userAccounts = roleMembers.Select(m => m.UserAcc).Distinct().ToList();
        var users = await userCacheHelper.GetUsersByAccountsAsync(userAccounts);
        var userLookup = users.ToDictionary(u => u.Account!, u => u.DisplayName!);

        foreach (var member in roleMembers)
        {
            JsonObject item = new();
            children.Add(item);
            item["Account"] = member.UserAcc;

            // 从缓存中查找对应的用户显示名
            if (userLookup.TryGetValue(member.UserAcc!, out var displayName))
            {
                item["DisplayName"] = displayName;
            }
            else
            {
                item["DisplayName"] = member.UserAcc;
            }
        }
        rv["data"] = children;
        
        return rv;
    }

    /// <summary>
    /// 成员添加到角色内
    /// </summary>
    /// <param name="role"></param>
    /// <param name="loginUserAcc"></param>
    /// <returns></returns>
    public async Task<JsonObject> AddRoleMember(RoleMenber role, string loginUserAcc)
    {
        JsonObject rv = new()
        {
            ["StateCode"] = 0,
            ["success"] = true
        };

        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            rv["success"] = false;
            rv["Msg"] = "该账号没有添加成员权限";
            return rv;
        }
        //删除所有现有成员
        var dataRoleMember = dbContext.SysRoleMember!.Where(b => b.RoleId == role.ItemId);
        foreach (SysRoleMember obj in dataRoleMember)
        {
            dbContext.SysRoleMember!.Remove(obj);
        }
        //添加提交的所有成员
        if (role.RoleMember!.Count > 0)//提交的数据如果是空的则相当于清空角色成员
        {
            foreach (RoleMember item in role.RoleMember)
            {
                dbContext.SysRoleMember!.Add(new SysRoleMember { ItemId = YitterHelper.NewId(), RoleId = role.ItemId, UserAcc = item.UserAcc });
            }
        }
        await dbContext.SaveChangesAsync();
        rv["Msg"] = "添加成功";
        
        return rv;
    }
}
