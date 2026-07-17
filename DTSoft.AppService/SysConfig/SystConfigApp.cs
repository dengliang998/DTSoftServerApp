using DTSoft.AppService.Attachment;
using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Attachment;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace DTSoft.AppService.SysConfig;

public class SysConfigApp(SysDbContext dbContext, ConfigHelper configHelper, AttachmentApp att, IDtSoftCache dtSoftCache)
{
    private const string SysConfigCacheKey = "SysConfig:Info";

    /// <summary>
    /// 设置系统信息
    /// </summary>
    public async Task<JsonObject> SetSysConfig(Models.Parameter.SysConfig.Config systemInfo)
    {
        Models.Entities.SysConfig? sysConfig = dbContext.SysConfig!
            .OrderBy(p => p.ItemId)
            .FirstOrDefault();
        
        if (sysConfig is null)
        {
            Models.Entities.SysConfig info = new()
            {
                SystemName = systemInfo.SystemName
            };
            
            // 如果有上传文件，处理文件
            if (systemInfo.LoginImg != null)
            {
                string filePath = configHelper.RootPath;
                var attachment = att.CreateFile(new BaseFileParameter() { Files = systemInfo.LoginImg, Path = filePath });
                info.LoginImg = attachment.FileFullName;
            }
            
            dbContext.SysConfig!.Add(info);
        }
        else
        {
            sysConfig.SystemName = systemInfo.SystemName;
            
            // 如果有上传文件，处理文件
            if (systemInfo.LoginImg != null)
            {
                string filePath = configHelper.RootPath;
                var attachment = att.CreateFile(new BaseFileParameter() { Files = systemInfo.LoginImg, Path = filePath });
                sysConfig.LoginImg = attachment.FileFullName;
            }
            
            dbContext.SysConfig!.Update(sysConfig);
        }
        
        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache(SysConfigCacheKey);

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0
        };
    }

    /// <summary>
    /// 获取系统信息
    /// </summary>
    public JsonObject GetSysConfig()
    {
        var dataJson = dtSoftCache.GetOrCreateAsync(SysConfigCacheKey, TimeSpan.FromMinutes(5), BuildSysConfigDataJson)
            .GetAwaiter()
            .GetResult();

        JsonObject data;
        try
        {
            data = JsonNode.Parse(dataJson) as JsonObject ?? new JsonObject();
        }
        catch
        {
            data = new JsonObject();
        }

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = data
        };
    }

    private string BuildSysConfigDataJson()
    {
        Models.Entities.SysConfig? systemInfo = dbContext.SysConfig!
            .AsNoTracking()
            .OrderBy(p => p.ItemId)
            .FirstOrDefault();

        // 读取文件并转为Base64
        string? loginImgBase64 = null;
        if (systemInfo != null && !string.IsNullOrEmpty(systemInfo.LoginImg))
        {
            var filePath = Path.Combine(configHelper.RootPath, systemInfo.LoginImg);
            if (File.Exists(filePath))
            {
                var bytes = File.ReadAllBytes(filePath);
                var ext = Path.GetExtension(systemInfo.LoginImg).TrimStart('.').ToLower();
                var mimeType = ext switch
                {
                    "png" => "image/png",
                    "gif" => "image/gif",
                    "svg" => "image/svg+xml",
                    "webp" => "image/webp",
                    _ => "image/jpeg"
                };
                loginImgBase64 = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
            }
        }

        var obj = new JsonObject
        {
            ["systemName"] = systemInfo?.SystemName,
            ["loginImg"] = loginImgBase64
        };

        return obj.ToJsonString();
    }

    /// <summary>
    /// 初始化系统数据库
    /// </summary>
    public JsonObject InitializationSystem()
    {
        // 检查数据库是否已存在（不抛出异常）
        if (IsDatabaseInitialized())
        {
            return new JsonObject
            {
                ["StateCode"] = 0,
                ["success"] = true,
                ["Msg"] = "数据库已存在，跳过初始化！"
            };
        }

        try
        {
            // 创建数据库（这会创建数据库和表结构）
            bool created = dbContext.Database.EnsureCreated();

            // 检查是否创建成功或数据库已存在，然后插入初始数据
            if (dbContext.Database.CanConnect())
            {
                // 再次检查是否已有数据，防止重复初始化
                if (dbContext.SysUser.Any(u => u.Account == "admin"))
                {
                    return new JsonObject
                    {
                        ["StateCode"] = 0,
                        ["success"] = true,
                        ["Msg"] = "数据库已存在，跳过初始化！"
                    };
                }

                // 添加初始数据
                AddInitialData();

                return new JsonObject
                {
                    ["StateCode"] = 0,
                    ["success"] = true,
                    ["Msg"] = "系统初始化成功！"
                };
            }
            else
            {
                return new JsonObject
                {
                    ["StateCode"] = 1,
                    ["success"] = false,
                    ["Msg"] = "数据库连接失败！"
                };
            }
        }
        catch (Exception ex) when (IsDatabaseNotFound(ex))
        { // 捕获数据库不存在的特定错误并处理
            try
            {
                // 直接尝试创建数据库
                dbContext.Database.EnsureCreated();

                // 检查是否创建成功，然后插入初始数据
                if (dbContext.Database.CanConnect())
                {
                    // 再次检查是否已有数据，防止重复初始化
                    if (dbContext.SysUser.Any(u => u.Account == "admin"))
                    {
                        return new JsonObject
                        {
                            ["StateCode"] = 0,
                            ["success"] = true,
                            ["Msg"] = "数据库已存在，跳过初始化！"
                        };
                    }

                    // 添加初始数据
                    AddInitialData();

                    return new JsonObject
                    {
                        ["StateCode"] = 0,
                        ["success"] = true,
                        ["Msg"] = "系统初始化成功！"
                    };
                }
                else
                {
                    return new JsonObject
                    {
                        ["StateCode"] = 1,
                        ["success"] = false,
                        ["Msg"] = "数据库连接失败！"
                    };
                }
            }
            catch (Exception innerEx)
            {
                return new JsonObject
                {
                    ["StateCode"] = 1,
                    ["success"] = false,
                    ["Msg"] = innerEx.Message
                };
            }
        }
        catch (Exception ex)
        {
            return new JsonObject
            {
                ["StateCode"] = 1,
                ["success"] = false,
                ["Msg"] = ex.Message
            };
        }
    }

    /// <summary>
    /// 添加初始数据
    /// </summary>
    private void AddInitialData()
    {
        //添加用户
        SysUser user = new()
        {
            Account = "admin",
            PassWord = Encrypt.HashPassword("admin123"),
            DisplayName = "系统管理员",
            Disable = false
        };
        dbContext.SysUser.Add(user);
        
        // 先保存用户
        dbContext.SaveChanges();

        //添加角色 - 使用 ItemId 而不是自增 ID
        var adminRole = new SysRole
        {
            ItemId = YitterHelper.NewId(),  // 管理员角色
            RoleName = "Administrator"
        };
        
        var everyoneRole = new SysRole
        {
            ItemId = YitterHelper.NewId(),  // 普通用户角色
            RoleName = "Everyone"
        };
        
        dbContext.SysRole!.AddRange(adminRole, everyoneRole);
        dbContext.SaveChanges();  // 保存角色以生成 ItemId

        //把用户添加到角色（使用实际的角色 ItemId）
        SysRoleMember rolemember = new()
        {
            ItemId = YitterHelper.NewId(),  // 生成唯一 ID
            RoleId = adminRole.ItemId,  // 使用实际生成的 ItemId
            UserAcc = "admin"
        };
        dbContext.SysRoleMember!.Add(rolemember);
        dbContext.SaveChanges();  // 保存角色成员关系

        //创建菜单 - 使用 ItemId，并正确处理层级关系
        var menuList = new List<SysMenu>();
        
        // 一级菜单
        var userManagement = new SysMenu { ItemId = YitterHelper.NewId(), Pid = 0, MenuName = "组织管理", Icon = "User", MType = 0 };
        var attachmentManagement = new SysMenu { ItemId = YitterHelper.NewId(), Pid = 0, MenuName = "附件管理", Icon = "UploadFilled", MType = 0 };
        var roleManagement = new SysMenu { ItemId = YitterHelper.NewId(), Pid = 0, MenuName = "角色管理", Icon = "UserFilled", MType = 0 };
        var adminPanel = new SysMenu { ItemId = YitterHelper.NewId(), Pid = 0, MenuName = "后台管理", Icon = "Grid", MType = 0 };
        
        // 二级菜单
        var userList = new SysMenu { ItemId = YitterHelper.NewId(), Pid = userManagement.ItemId, MenuName = "组织架构", MenuPath = "user/organization", Icon = "List", MType = 0 };
        var attachmentList = new SysMenu { ItemId = YitterHelper.NewId(), Pid = attachmentManagement.ItemId, MenuName = "附件列表", MenuPath = "attachment/attachmentlist", Icon = "Paperclip", MType = 0 };
        var roleList = new SysMenu { ItemId = YitterHelper.NewId(), Pid = roleManagement.ItemId, MenuName = "角色列表", MenuPath = "role/rolesmenu", Icon = "List", MType = 0 };
        var systemManagement = new SysMenu { ItemId = YitterHelper.NewId(), Pid = adminPanel.ItemId, MenuName = "系统管理", Icon = "Setting", MType = 0 };
        var menuManagement = new SysMenu { ItemId = YitterHelper.NewId(), Pid = adminPanel.ItemId, MenuName = "菜单管理", Icon = "Menu", MType = 0 };
        var systemIntegration = new SysMenu { ItemId = YitterHelper.NewId(), Pid = adminPanel.ItemId, MenuName = "系统集成", Icon = "Connection", MType = 0 };
        
        // 三级菜单
        var systemLog = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = "系统日志", MenuPath = "log/logaction", Icon = "List", MType = 0 };
        var appConfig = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = "微应用配置", MenuPath = "MicroApp/MicroApiConfig", Icon = "Coin", MType = 0 };
        var systemSettingsPage = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = "系统设置", MenuPath = "common/systemsettings", Icon = "Setting", MType = 0 };
        var onlineUsers = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemManagement.ItemId, MenuName = "在线用户", MenuPath = "common/onlineusers", Icon = "User", MType = 0 };
        var menuMaintenance = new SysMenu { ItemId = YitterHelper.NewId(), Pid = menuManagement.ItemId, MenuName = "菜单维护", MenuPath = "common/menus", Icon = "Menu", MType = 0 };
        var apiKeyManagement = new SysMenu { ItemId = YitterHelper.NewId(), Pid = systemIntegration.ItemId, MenuName = "第三方集成", MenuPath = "apikey/management", Icon = "Key", MType = 0 };
        
        menuList.AddRange([userManagement, userList, attachmentManagement, attachmentList, roleManagement, roleList, adminPanel, systemManagement, menuManagement, systemIntegration, apiKeyManagement, systemSettingsPage, systemLog, appConfig, onlineUsers, menuMaintenance]);
        
        // 批量添加菜单
        dbContext.SysMenu.AddRange(menuList);
        dbContext.SaveChanges();  // 保存所有菜单以生成 ItemId
        
        // 菜单授权（使用实际的菜单 ItemId）
        foreach (var item in menuList)
        {
            dbContext.SysMenuAuthority!.Add(new SysMenuAuthority 
            { 
                ItemId = YitterHelper.NewId(),  // 为每个授权记录生成唯一 ID
                RoleID = adminRole.ItemId,  // 使用实际的角色 ItemId
                MenuID = item.ItemId 
            });
        }
        dbContext.SaveChanges();
        
        // 注意：admin 是超级管理员账号，不属于任何部门
        // 部门数据为空，需要在部门管理功能中手动创建
    }

    /// <summary>
    /// 检查数据库是否已经初始化
    /// </summary>
    /// <returns></returns>
    private bool IsDatabaseInitialized()
    {
        try
        {
            // 尝试打开数据库连接
            dbContext.Database.OpenConnection();
            dbContext.Database.CloseConnection();

            // 检查管理员用户是否存在
            var adminUser = dbContext.SysUser.FirstOrDefault(u => u.Account == "admin");
            if (adminUser != null)
            {
                return true; // 如果管理员用户存在，则认为数据库已初始化
            }

            // 检查数据库是否存在相关表并是否有数据
            var userCount = dbContext.SysUser.Count();
            if (dbContext.SysRole == null)
            {
                return false;
            }
            var roleCount = dbContext.SysRole.Count();

            // 如果用户表或角色表中有数据，则认为数据库已初始化
            return userCount > 0 || roleCount > 0;
        }
        catch (Exception ex) when (IsDatabaseNotFound(ex))
        {
            // 数据库不存在
            return false;
        }
        catch (Exception)
        {
            // 其他异常也认为是未初始化
            return false;
        }
    }

    private static bool IsDatabaseNotFound(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("Unknown database", StringComparison.OrdinalIgnoreCase) ||
               (message.Contains("database", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)) ||
               message.Contains("Cannot open database", StringComparison.OrdinalIgnoreCase);
    }
}
