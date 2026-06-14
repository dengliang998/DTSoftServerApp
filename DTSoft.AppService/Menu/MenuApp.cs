using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Menu;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DTSoft.AppService.Menu;

public class MenuApp(SysDbContext dbContext, IDtSoftCache dtSoftCache, DtSoftHelper dtSoftHelper)
{
    private const string MenuUrlMapCacheKey = "MenuUrlMap";

    private sealed class MenuUrlInfo
    {
        public string? PMenuName { get; init; }
        public string? MenuName { get; init; }
        public string? PageUrl { get; init; }
    }

    private async Task<long> GetRoleIdByNameAsync(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return 0;

        var cacheKey = $"RoleIdByName:{roleName.Trim().ToLowerInvariant()}";
        var cached = await dtSoftCache.GetAsync<string>(cacheKey);
        if (long.TryParse(cached, out var cachedId))
            return cachedId;

        var role = await dbContext.SysRole!
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleName == roleName);
        var id = role?.ItemId ?? 0;

        await dtSoftCache.SetAsync(cacheKey, id.ToString(), TimeSpan.FromMinutes(1));
        return id;
    }

    private string BuildMenuUrlMapJson()
    {
        var sysSystemUrl = dbContext.SysSystemUrl;
        var sysMenu = dbContext.SysMenu;

        if (sysSystemUrl == null || sysMenu == null)
            return "{}";

        var rows = sysSystemUrl
            .AsNoTracking()
            .Join(sysMenu.AsNoTracking(), a => a.MenuId, b => b.ItemId, (a, b) => new
            {
                a.PageCode,
                a.MenuName,
                a.PageUrl,
                PMenuName = b.MenuName
            })
            .ToList();

        var map = new Dictionary<string, MenuUrlInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.PageCode)) continue;
            map[row.PageCode] = new MenuUrlInfo
            {
                PMenuName = row.PMenuName,
                MenuName = row.MenuName,
                PageUrl = row.PageUrl
            };
        }

        return JsonSerializer.Serialize(map);
    }

    private async Task<List<MenuData>> MenuData()
    {
        //缓存菜单数据
        return await dtSoftCache.GetOrCreateAsync("MenuList", TimeSpan.FromMinutes(1), () =>
        {
            // 检查相关DbSet是否为null
            if (dbContext.SysMenu == null || dbContext.SysMenuAuthority == null || dbContext.SysRoleMember == null)
            {
                return new List<MenuData>(); // 返回空列表而不是抛出异常
            }

            // 查询所有菜单及其权限信息
            var query = from a in dbContext.SysMenu
                join b in dbContext.SysMenuAuthority on a.ItemId equals b.MenuID
                join c in dbContext.SysRoleMember on b.RoleID equals c.RoleId into roleMember
                from x in roleMember.DefaultIfEmpty()
                select new MenuData
                {
                    ItemId = a.ItemId,
                    Pid = a.Pid,
                    MenuName = a.MenuName,
                    MenuPath = a.MenuPath,
                    Order = a.Order,
                    UserAcc = x.UserAcc ?? "",
                    RoleId = b.RoleID,
                    Icon = a.Icon,
                    IsHidden = a.IsHidden,
                    MType = a.MType
                };
            return query.ToList();
        });
    }

    /// <summary>
    /// 获取菜单
    /// </summary>
    public async Task<JsonObject> GetMenuAsync(string userAcc)
    {
        // 参数验证
        if (string.IsNullOrWhiteSpace(userAcc))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "参数 UserAcc 不能为空"
            };
        }
    
        // 获取菜单数据
        var menuData = await MenuData();
    
        var everyoneRoleId = await GetRoleIdByNameAsync("Everyone");
    
        // 管理员可以访问所有菜单，普通用户根据权限获取菜单
        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = DtMenu.GetMenu(userAcc, menuData, everyoneRoleId, dtSoftHelper.IsAdmin(userAcc))
        };
    }

    /// <summary>
    /// 获取一级菜单
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public JsonObject GetFatherMenu(MenuList obj)
    {
        JsonObject rv = new();
        JsonArray data = new();
        rv["StateCode"] = 0;
        rv["success"] = true;

        var result = dbContext.SysMenu.Where(b => b.Pid == 0);
        foreach (var rows in result)
        {
            JsonObject item = new();
            data.Add(item);
            item["id"] = rows.ItemId;
            item["MenuName"] = rows.MenuName;
            //判断是否是管理员
            if (obj.RoleId.Equals(1))
            {
                item["disabled"] = true;
            }
        }
        rv["data"] = data;
        
        return rv;
    }

    /// <summary>
    /// 获取子菜单
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public JsonObject GetChildrenMenu(MenuList obj)
    {
        JsonObject rv = new();
        JsonArray data = new();
        rv["StateCode"] = 0;
        rv["success"] = true;

        var result = dbContext.SysMenu.Where(b => b.Pid == obj.MenuId![0]);
        foreach (var rows in result)
        {
            JsonObject item = new();
            data.Add(item);
            item["id"] = rows.ItemId;
            item["MenuName"] = rows.MenuName;
            //判断是否是管理员
            if (obj.RoleId.Equals(1))
            {
                item["disabled"] = true;
            }
        }
        rv["data"] = data;
        
        return rv;
    }

    /// <summary>
    /// 获取角色和菜单的对应关系
    /// </summary>
    /// <param name="RoleId"></param>
    /// <returns></returns>
    public JsonObject GetRoleMenuMap(long RoleId)
    {
        JsonObject rv = new();
        JsonArray data = new();
        rv["StateCode"] = 0;
        rv["success"] = true;

        var sysMenuAuthority = dbContext.SysMenuAuthority;
        var sysMenu = dbContext.SysMenu;

        if (sysMenuAuthority == null)
        {
            rv["StateCode"] = 1;
            rv["success"] = false;
            rv["Msg"] = "数据访问对象未初始化";
            return rv;
        }

        var result = sysMenuAuthority
            .Join(sysMenu, a => a.MenuID, b => b.ItemId, (a, b) => new { a.MenuID, a.RoleID, pid = b.Pid })
            .Where(b => b.RoleID == RoleId);
        foreach (var rows in result)
        {
            JsonObject item = new();
            data.Add(item);
            item["pid"] = rows.pid;
            item["MenuID"] = rows.MenuID;
        }
        rv["data"] = data;
        
        return rv;
    }

    /// <summary>
    /// 更新角色对应的菜单权限
    /// </summary>
    /// <param name="menulist"></param>
    /// <param name="loginUserAcc"></param>
    /// <returns></returns>
    public async Task<JsonObject> UpdateMenuAuthority(MenuList menulist, string loginUserAcc)
    {
        JsonObject rv = new()
        {
            ["StateCode"] = 0,
            ["success"] = true
        };
    
        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            rv["success"] = false;
            rv["Msg"] = "操作失败：该账号没有修改权限";
            return rv;
        }
        if (!menulist.RoleId.Equals(1))
        {
            //移除角色下所有的菜单权限
            await dbContext.SysMenuAuthority!
                .Where(b => b.RoleID == menulist.RoleId)
                .ExecuteDeleteAsync();
            //添加菜单权限
            foreach (var menu in menulist.MenuId!)
            {
                SysMenuAuthority menus = new()
                {
                    ItemId = YitterHelper.NewId(),
                    RoleID = menulist.RoleId,
                    MenuID = menu
                };
                dbContext.SysMenuAuthority!.Add(menus);
            }
            dtSoftCache.RefreshCache("MenuList");
            await dbContext.SaveChangesAsync();
            rv["Msg"] = "操作成功";
        }
        else
        {
            rv["success"] = false;
            rv["Msg"] = "操作失败：Administrator 不允许操作";
        }
            
        return rv;
    }

    /// <summary>
    /// 获取自定义菜单 URL
    /// </summary>
    /// <param name="pageCode"></param>
    /// <returns></returns>
    public JsonObject GetMenuUrl(string pageCode)
    {
        JsonObject rv = new()
        {
            ["StateCode"] = 0,
            ["success"] = true
        };
    
        if (string.IsNullOrEmpty(pageCode))
        {
            rv["success"] = false;
            rv["Msg"] = "PageCode 不能为空";
            return rv;
        }
        var sysSystemUrl = dbContext.SysSystemUrl;
        var sysMenu = dbContext.SysMenu;
    
        if (sysSystemUrl == null)
        {
            rv["StateCode"] = 1;
            rv["success"] = false;
            rv["Msg"] = "数据访问对象未初始化";
            return rv;
        }
    
        var mapJson = dtSoftCache.GetOrCreateAsync(MenuUrlMapCacheKey, TimeSpan.FromMinutes(5), BuildMenuUrlMapJson)
            .GetAwaiter()
            .GetResult();

        Dictionary<string, MenuUrlInfo>? map = null;
        try
        {
            map = JsonSerializer.Deserialize<Dictionary<string, MenuUrlInfo>>(mapJson);
        }
        catch
        {
            // ignore
        }

        if (map == null || !map.TryGetValue(pageCode, out var info) || info == null)
        {
            rv["success"] = false;
            rv["Msg"] = "配置错误，未找到菜单地址";
            return rv;
        }

        rv["PMenuName"] = info.PMenuName;
        rv["MenuName"] = info.MenuName;
        rv["PageUrl"] = info.PageUrl;
            
        return rv;
    }

    /// <summary>
    /// 检查是否有菜单访问权限
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public async Task<JsonObject> CheckMenuPermissionsAsync(CheckMenuPermissions obj)
    {
        JsonObject rv = new()
        {
            ["StateCode"] = 0,
            ["success"] = true,
            ["Permissions"] = false
        };

        if (!string.IsNullOrEmpty(obj.Account) && !string.IsNullOrEmpty(obj.FullName))
        {
            // 检查用户是否为管理员
            bool isAdmin = dtSoftHelper.IsAdmin(obj.Account);
            
            var everyoneRoleId = await GetRoleIdByNameAsync("Everyone");
            
            bool result = DtMenu.CheckMenuPermissions(obj.Account, obj.FullName, await MenuData(), everyoneRoleId, isAdmin);
            rv["Permissions"] = result;
        }
        else
        {
            rv["success"] = false;
            rv["Msg"] = "参数不能为空";
        }
        
        return rv;
    }

    /// <summary>
    /// 添加菜单
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="loginUserAcc"></param>
    /// <returns></returns>
    public async Task<JsonObject> AddMenu(AddMenu obj, string loginUserAcc)
    {
        JsonObject rv = new()
        {
            ["StateCode"] = 0,
            ["success"] = true
        };
    
        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            rv["success"] = false;
            rv["Msg"] = "操作失败：该账号没有修改权限";
            return rv;
        }
    
        if (obj.Pid is not 0)
        {
            SysMenu? dbmenu = dbContext.SysMenu.FirstOrDefault(p => p.ItemId.Equals(obj.Pid));
            if (dbmenu is null)
            {
                rv["success"] = false;
                rv["Msg"] = "pid 错误";
                return rv;
            }
        }
    
        SysMenu menu = new()
        {
            ItemId = YitterHelper.NewId(),
            Pid = obj.Pid,
            MenuName = obj.MenuName,
            MenuPath = obj.Type.Equals(MenuType.Internal) ? obj.MenuPath : $"JumpPage?PageCode={obj.MenuPath}",
            Order = obj.Order,
            Icon = obj.Icon,
            IsHidden = obj.IsHidden,
            MType = obj.MType
        };
        dbContext.SysMenu.Add(menu);
    
        await dbContext.SaveChangesAsync();
    
        if (obj.Type is MenuType.External)
        {
            SysSystemUrl sysurl = new()
            {
                ItemId = YitterHelper.NewId(),
                PageCode = obj.SystemUrlBase.PageCode,
                MenuName = obj.SystemUrlBase.MenuName,
                PageUrl = obj.SystemUrlBase.PageUrl,
                MenuId = menu.ItemId
            };
            dbContext.SysSystemUrl!.Add(sysurl);
        }
    
        //默认权限 - 动态获取管理员角色 ID
        var adminRoleId = await GetRoleIdByNameAsync("Administrator");
        if (adminRoleId != 0)
        {
            dbContext.SysMenuAuthority!.Add(new SysMenuAuthority { ItemId = YitterHelper.NewId(), RoleID = adminRoleId, MenuID = menu.ItemId });
        }
        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache("MenuList");
        dtSoftCache.RefreshCache(MenuUrlMapCacheKey);
            
        return rv;
    }

    /// <summary>
    /// 修改菜单
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="loginUserAcc"></param>
    /// <returns></returns>
    public async Task<JsonObject> UpdateMenu(UpdateMenuParameter obj, string loginUserAcc)
    {
        JsonObject rv = new()
        {
            ["StateCode"] = 0,
            ["success"] = true
        };
    
        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            rv["success"] = false;
            rv["Msg"] = "操作失败：该账号没有修改权限";
            return rv;
        }
    
        SysMenu? menu = dbContext.SysMenu.FirstOrDefault(p => p.ItemId.Equals(obj.ItemId));
        if (menu is null)
        {
            rv["success"] = false;
            rv["Msg"] = "MenuID 错误";
            return rv;
        }
    
        // 更新菜单信息
        menu.MenuName = obj.MenuName;
        menu.Order = obj.Order;
        menu.MenuPath = obj.MenuPath;
        menu.Icon = obj.Icon;
        menu.IsHidden = obj.IsHidden;
        menu.Pid = obj.Pid;
        menu.MType = obj.MType;
    
        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache("MenuList");
        dtSoftCache.RefreshCache(MenuUrlMapCacheKey);
	            
        return rv;
    }

    /// <summary>
    /// 删除菜单
    /// </summary>
    /// <param name="menuId"></param>
    /// <param name="loginUserAcc"></param>
    /// <returns></returns>
    public async Task<JsonObject> DelMenu(long menuId,string loginUserAcc)
    {
        JsonObject rv = new()
        {
            ["StateCode"] = 0,
            ["success"] = true
        };
    
        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            rv["success"] = false;
            rv["Msg"] = "操作失败：该账号没有修改权限";
            return rv;
        }
        SysMenu? menu = await dbContext.SysMenu.FirstOrDefaultAsync(p => p.ItemId.Equals(menuId));
        if (menu is null)
        {
            rv["success"] = false;
            rv["Msg"] = "MenuID 错误";
            return rv;
        }
    
        // 检查是否存在子级菜单
        var childMenus = await dbContext.SysMenu.AnyAsync(p => p.Pid.Equals(menuId));
        if (childMenus)
        {
            rv["success"] = false;
            rv["Msg"] = "操作失败：该菜单下存在子级菜单，请先删除子级菜单";
            return rv;
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            //移除菜单权限（批量删除）
            await dbContext.SysMenuAuthority!
                .Where(p => p.MenuID.Equals(menuId))
                .ExecuteDeleteAsync();

            //移除外部地址（批量删除）
            await dbContext.SysSystemUrl!
                .Where(p => p.MenuId.Equals(menuId))
                .ExecuteDeleteAsync();

            //移除菜单
            dbContext.SysMenu.Remove(menu);
            await dbContext.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        dtSoftCache.RefreshCache("MenuList");
        dtSoftCache.RefreshCache(MenuUrlMapCacheKey);
		            
        return rv;
    }
}
