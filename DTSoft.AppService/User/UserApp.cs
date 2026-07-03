using DTSoft.AppService.Attachment;
using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Attachment;
using DTSoft.Models.Parameter.Base;
using DTSoft.Models.Parameter.User;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace DTSoft.AppService.User;

public class UserApp(
    UserCacheHelper userCacheHelper,
    SysDbContext dbContext,
    DtSoftHelper dtSoftHelper,
    ConfigHelper configHelper,
    AttachmentApp att)
{
    public async Task<JsonObject> GetUserInfoAsync(string userAcc)
    {
        // 业务验证
        if (string.IsNullOrEmpty(userAcc))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "参数 UserAcc 错误"
            };
        }
    
        // 从缓存中查找指定用户
        var user = await userCacheHelper.GetUserByAccountAsync(userAcc);
    
        if (user != null)
        {
            return new JsonObject
            {
                ["success"] = true,
                ["StateCode"] = 0,
                ["Account"] = user.Account,
                ["DisplayName"] = user.DisplayName,
                ["Avatar"] = $"/api/User/GetUserAvatar?Account={user.Account}"
            };
        }
        else
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "用户不存在"
            };
        }
    }

    /// <summary>
    /// 根据账号获取用户详细信息
    /// </summary>
    public async Task<JsonObject> GetUserDetailByAccountAsync(string account)
    {
        var user = await userCacheHelper.GetUserByAccountAsync(account);
    
        if (user == null)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "用户不存在"
            };
        }

        // 获取用户的部门信息
        var userMember = await dbContext.SysUserMember!
            .FirstOrDefaultAsync(ud => ud.UserAcc == account);

        // 获取用户直属主管信息
        var supervisorAcc = await dbContext.SysUserSupervisor!
            .AsNoTracking()
            .Where(x => x.UserAcc == account)
            .Select(x => x.SupervisorAcc)
            .FirstOrDefaultAsync();

        var supervisorDisplayName = string.IsNullOrWhiteSpace(supervisorAcc)
            ? null
            : await dbContext.SysUser.AsNoTracking()
                .Where(x => x.Account == supervisorAcc)
                .Select(x => x.DisplayName)
                .FirstOrDefaultAsync();

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["Account"] = user.Account,
            ["DisplayName"] = user.DisplayName,
            ["Sex"] = user.Sex,
            ["Avatar"] = user.Avatar,
            ["Disable"] = user.Disable,
            ["OuId"] = userMember?.OuId,
            ["Position"] = user.Position,
            ["Email"] = user.Email,
            ["SupervisorAcc"] = supervisorAcc,
            ["SupervisorDisplayName"] = supervisorDisplayName
        };
    }

    public async Task<JsonObject> GetUserListAsync(Para obj)
    {
        // 必须传递部门ID，否则不显示用户
        if (!obj.OuId.HasValue || obj.OuId.Value <= 0)
        {
            return new JsonObject
            {
                ["success"] = true,
                ["StateCode"] = 0,
                ["Total"] = 0,
                ["data"] = new JsonArray()
            };
        }

        var OuId = obj.OuId.Value;

        var query =
            from u in dbContext.SysUser.AsNoTracking()
            join m in dbContext.SysUserMember!.AsNoTracking()
                on u.Account equals m.UserAcc
            join s in dbContext.SysUserSupervisor!.AsNoTracking()
                on u.Account equals s.UserAcc into supJoin
            from s in supJoin.DefaultIfEmpty()
            join su in dbContext.SysUser.AsNoTracking()
                on s.SupervisorAcc equals su.Account into supUserJoin
            from su in supUserJoin.DefaultIfEmpty()
            where m.OuId == OuId && u.Account != "admin"
            select new
            {
                u.Account,
                u.DisplayName,
                u.Sex,
                u.Avatar,
                u.Disable,
                OuId = m.OuId,
                u.Position,
                u.Email,
                s.SupervisorAcc,
                SupervisorDisplayName = su.DisplayName
            };

        if (!string.IsNullOrWhiteSpace(obj.Keyword))
        {
            var keyword = obj.Keyword.Trim();
            query = query.Where(x =>
                EF.Functions.Like(x.Account!, $"%{keyword}%") ||
                EF.Functions.Like(x.DisplayName!, $"%{keyword}%"));
        }

        var total = await query.CountAsync();
        var result = await query
            .OrderBy(x => x.Account)
            .Skip(obj.PageSize * (obj.PageNum - 1))
            .Take(obj.PageSize)
            .ToListAsync();

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["Total"] = total,
            ["data"] = new JsonArray(result.Select(rows => new JsonObject
            {
                ["Account"] = rows.Account,
                ["DisplayName"] = rows.DisplayName,
                ["Sex"] = rows.Sex,
                ["Avatar"] = rows.Avatar,
                ["Disable"] = rows.Disable,
                ["OuId"] = rows.OuId,
                ["Position"] = rows.Position,
                ["Email"] = rows.Email,
                ["SupervisorAcc"] = rows.SupervisorAcc,
                ["SupervisorDisplayName"] = rows.SupervisorDisplayName
            }).ToArray())
        };
    }

    public async Task<JsonObject> CreateUser(UserDto userDto)
    {
        // 业务验证
        if (string.IsNullOrEmpty(userDto.Account) || string.IsNullOrEmpty(userDto.PassWord))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "账号和密码不能为空"
            };
        }

        // 检查用户是否存在
        var existingUser = await userCacheHelper.GetUserByAccountAsync(userDto.Account);
        if (existingUser != null)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = $"账号：{userDto.Account}已存在"
            };
        }

        //密码加密处理，默认激活账号
        userDto.PassWord = Encrypt.Encrypt_MD5(userDto.PassWord);
        userDto.Disable = false;

        dbContext.SysUser.Add(new SysUser
        {
            Account = userDto.Account,
            PassWord = userDto.PassWord,
            DisplayName = userDto.DisplayName,
            Sex = userDto.Sex,
            Avatar = userDto.Avatar,
            Position = userDto.Position,
            Email = userDto.Email
        });

        await dbContext.SaveChangesAsync();
        
        // 添加部门关联
        if (userDto.OuId.HasValue && userDto.OuId.Value > 0)
        {
            dbContext.SysUserMember!.Add(new SysUserMember
            {
                ItemId = YitterHelper.NewId(),
                OuId = userDto.OuId.Value,
                UserAcc = userDto.Account
            });
            await dbContext.SaveChangesAsync();
        }

        // 添加直属主管关联（可选）
        var upsertSupervisorResult = await UpsertSupervisorAsync(userDto.Account!, userDto.SupervisorAcc);
        if (!upsertSupervisorResult.Success)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = upsertSupervisorResult.ErrorMessage ?? "主管维护失败"
            };
        }

        if (!string.IsNullOrWhiteSpace(userDto.SupervisorAcc))
        {
            await dbContext.SaveChangesAsync();
        }
        
        await userCacheHelper.RefreshUserCacheAsync(userDto.Account);

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["Msg"] = "账号创建成功"
        };
    }

    public async Task<JsonObject> ModifyUserInfo(UserDto user, string loginUserAcc, bool updateSupervisor)
    {
        // 权限验证
        if (!dtSoftHelper.IsAdmin(loginUserAcc) && !user.Account!.ToLower().Equals(loginUserAcc))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "该账号没有修改权限"
            };
        }

        var data = await dbContext.SysUser.FirstOrDefaultAsync(p => p.Account == user.Account);
        
        if (data == null)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "用户不存在"
            };
        }

        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            // 删除旧头像文件
            string path = $"{configHelper.UserDataPath}/{data.Avatar}";
            if (File.Exists(path) && !data.Avatar!.Equals(user.Avatar))
            {
                File.Delete(path);
            }
            
            data.DisplayName = user.DisplayName;
            data.Sex = user.Sex;
            data.Avatar = user.Avatar;
            data.Position = user.Position;
            data.Email = user.Email;

            dbContext.SysUser.Attach(data);
        }
        else
        {
            data.Disable = user.Disable;
            dbContext.SysUser.Attach(data);
        }
        
        // 更新部门关联
        if (user.OuId.HasValue)
        {
            // 删除现有部门关联（批量删除，避免加载实体）
            await dbContext.SysUserMember!
                .Where(ud => ud.UserAcc == user.Account)
                .ExecuteDeleteAsync();
            
            // 添加新部门关联
            if (user.OuId.Value > 0)
            {
                dbContext.SysUserMember!.Add(new SysUserMember
                {
                    ItemId = YitterHelper.NewId(),
                    OuId = user.OuId.Value,
                    UserAcc = user.Account
                });
            }
        }

        // 更新直属主管关联（仅当请求里显式携带 SupervisorAcc 字段时才更新）
        if (updateSupervisor)
        {
            var upsertSupervisorResult = await UpsertSupervisorAsync(user.Account!, user.SupervisorAcc);
            if (!upsertSupervisorResult.Success)
            {
                return new JsonObject
                {
                    ["success"] = false,
                    ["StateCode"] = 0,
                    ["Msg"] = upsertSupervisorResult.ErrorMessage ?? "主管维护失败"
                };
            }
        }

        await dbContext.SaveChangesAsync();
        
        await userCacheHelper.RefreshUserCacheAsync(user.Account);

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["Msg"] = "修改成功"
        };
    }

    public async Task<JsonObject> DelUser(string account, string loginUserAcc)
    {
        // 权限验证
        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "该账户没有删除权限"
            };
        }

        // 检查该用户是否在角色内
        var inRole = await dbContext.SysRoleMember!.AnyAsync(b => b.UserAcc == account);
        if (inRole)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "必须先删除角色成员才能删除"
            };
        }

        // 删除部门关联
        await dbContext.SysUserMember!
            .Where(b => b.UserAcc == account)
            .ExecuteDeleteAsync();

        // 删除主管关联（作为下属或作为主管）
        await dbContext.SysUserSupervisor!
            .Where(b => b.UserAcc == account || b.SupervisorAcc == account)
            .ExecuteDeleteAsync();

        // 从数据库获取用户数据
        var data = await dbContext.SysUser.FirstOrDefaultAsync(p => p.Account == account);
        if (data == null)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "用户不存在"
            };
        }

        // 删除头像文件
        string path = $"{configHelper.UserDataPath}/{data.Avatar}";
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        // 从数据库删除实际记录
        dbContext.SysUser.Remove(data);
        await dbContext.SaveChangesAsync();
        await userCacheHelper.RefreshUserCacheAsync(account);

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["Msg"] = "删除成功"
        };
    }

    public JsonObject UploadAvatar(FileUploadApi objFile)
    {
        var attachment = att.CreateFile(new BaseFileParameter() { Files = objFile.Files, Path = configHelper.UserDataPathUpload });
        
        return new JsonObject
        {
            ["success"] = attachment.Success,
            ["StateCode"] = 0,
            ["AvatarID"] = attachment.Success ? attachment.FileFullName : null
        };
    }

    public async Task<JsonObject> GetUserAvatar(string account)
    {
        // 从缓存获取用户数据
        var sysUser = await userCacheHelper.GetUserByAccountAsync(account);

        if (sysUser == null)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["msg"] = "账号错误"
            };
        }

        if (sysUser.Avatar == null)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["FilePath"] = "",
                ["msg"] = "用户没有头像"
            };
        }

        string filePath = Path.Combine(configHelper.UserDataPath, sysUser.Avatar);
        if (File.Exists(filePath))
        {
            return new JsonObject
            {
                ["success"] = true,
                ["StateCode"] = 0,
                ["FilePath"] = filePath
            };
        }
        else
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["FilePath"] = "",
                ["msg"] = "文件不存在或已被删除"
            };
        }
    }

    public async Task<JsonObject> ResetPassword(ResetPassword obj, string loginUserAcc)
    {
        // 权限验证
        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "该账号没有重置密码权限"
            };
        }

        // 从数据库获取用户数据
        var user = await dbContext.SysUser.FirstOrDefaultAsync(p => p.Account == obj.Account);
        
        if (user == null)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "用户不存在"
            };
        }

        user.PassWord = Encrypt.Encrypt_MD5(obj.PassWord);
        await dbContext.SaveChangesAsync();
        await userCacheHelper.RefreshUserCacheAsync(obj.Account);

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["Msg"] = "密码重置成功"
        };
    }

    public async Task<JsonObject> ModifyPassword(ModifyPassword obj)
    {
        // 字段验证
        if (string.IsNullOrEmpty(obj.NewPassWord) || string.IsNullOrEmpty(obj.OldPassWord) || string.IsNullOrEmpty(obj.Account))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "修改失败，必要字段不能为空！"
            };
        }

        // 从数据库获取用户数据
        var user = await dbContext.SysUser.FirstOrDefaultAsync(p => p.Account == obj.Account);
        
        if (user == null || user.PassWord != Encrypt.Encrypt_MD5(obj.OldPassWord))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "旧密码错误！"
            };
        }

        user.PassWord = Encrypt.Encrypt_MD5(obj.NewPassWord);
        await dbContext.SaveChangesAsync();
        await userCacheHelper.RefreshUserCacheAsync(obj.Account);

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["Msg"] = "密码修改成功"
        };
    }

    private sealed record UpsertSupervisorResult(bool Success, string? ErrorMessage);

    private async Task<UpsertSupervisorResult> UpsertSupervisorAsync(string userAcc, string? supervisorAcc)
    {
        if (string.IsNullOrWhiteSpace(userAcc))
        {
            return new UpsertSupervisorResult(false, "参数 UserAcc 错误");
        }

        supervisorAcc = string.IsNullOrWhiteSpace(supervisorAcc) ? null : supervisorAcc.Trim();

        // 不传主管：表示清空直属主管
        if (supervisorAcc is null)
        {
            await dbContext.SysUserSupervisor!
                .Where(x => x.UserAcc == userAcc)
                .ExecuteDeleteAsync();
            return new UpsertSupervisorResult(true, null);
        }

        if (string.Equals(userAcc, supervisorAcc, StringComparison.OrdinalIgnoreCase))
        {
            return new UpsertSupervisorResult(false, "直属主管不能是自己");
        }

        // 主管账号必须存在
        var supervisorExists = await dbContext.SysUser.AsNoTracking().AnyAsync(x => x.Account == supervisorAcc);
        if (!supervisorExists)
        {
            return new UpsertSupervisorResult(false, $"直属主管账号不存在：{supervisorAcc}");
        }

        // 防止形成循环主管链
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { userAcc };
        var cursor = supervisorAcc;
        for (var i = 0; i < 50; i++)
        {
            if (!visited.Add(cursor))
            {
                return new UpsertSupervisorResult(false, "设置失败：会形成循环的主管链");
            }

            var next = await dbContext.SysUserSupervisor!
                .AsNoTracking()
                .Where(x => x.UserAcc == cursor)
                .Select(x => x.SupervisorAcc)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(next)) break;
            cursor = next;
        }

        // 删除旧关联后插入新关联（保证一个用户最多一条）
        await dbContext.SysUserSupervisor!
            .Where(x => x.UserAcc == userAcc)
            .ExecuteDeleteAsync();

        dbContext.SysUserSupervisor!.Add(new SysUserSupervisor
        {
            ItemId = YitterHelper.NewId(),
            UserAcc = userAcc,
            SupervisorAcc = supervisorAcc
        });

        // 注意：不在这里 SaveChanges，交由调用方统一提交
        return new UpsertSupervisorResult(true, null);
    }
}
