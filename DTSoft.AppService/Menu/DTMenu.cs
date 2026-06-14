using DTSoft.Models.Parameter.Menu;
using System.Text.Json.Nodes;

namespace DTSoft.AppService.Menu;

public abstract class DtMenu
{
    /// <summary>
    /// 获取菜单树
    /// </summary>
    /// <param name="userAcc"></param>
    /// <param name="menus"></param>
    /// <param name="everyoneRoleId">Everyone 角色的 ID（动态生成）</param>
    /// <param name="isAdmin"></param>
    /// <returns></returns>
    public static JsonArray GetMenu(string userAcc, IEnumerable<MenuData> menus, long everyoneRoleId = 0, bool isAdmin = false)
    {
        JsonArray data = new();
        var menuDatas = menus as MenuData[] ?? menus.ToArray();

        // 根据是否为管理员来决定获取菜单的逻辑
        IEnumerable<MenuData> result;
        if (isAdmin)
        {
            // 管理员获取所有顶级菜单
            result = menuDatas.Where(b => b.Pid.Equals(0))
                .DistinctBy(p => p.ItemId)
                .OrderBy(o => o.Order);
        }
        else
        {
            // 普通用户获取有权限的顶级菜单
            result = menuDatas.Where(b => b.Pid.Equals(0)
                                          && (b.UserAcc.Equals(userAcc, StringComparison.OrdinalIgnoreCase)
                                              || b.RoleId.Equals(everyoneRoleId)))
                .DistinctBy(p => p.ItemId)
                .OrderBy(o => o.Order);
        }

        foreach (var rows in result)
        {
            JsonObject item = new();
            data.Add(item);
            item["id"] = rows.ItemId;
            item["pid"] = rows.Pid;
            item["MenuName"] = rows.MenuName;
            item["path"] = rows.MenuPath;
            item["order"] = rows.Order;
            item["Icon"] = rows.Icon;
            item["IsHidden"] = rows.IsHidden;
            item["MType"] = rows.MType;

            GetMenuChildren(item, menuDatas, userAcc, rows.ItemId, everyoneRoleId, isAdmin);
        }

        return data;
    }

    /// <summary>
    /// 递归查询子菜单
    /// </summary>
    /// <param name="item"></param>
    /// <param name="query"></param>
    /// <param name="userAcc"></param>
    /// <param name="id"></param>
    /// <param name="everyoneRoleId">Everyone 角色的 ID（动态生成）</param>
    /// <param name="isAdmin"></param>
    private static void GetMenuChildren(JsonNode item, IEnumerable<MenuData> query, string userAcc, long id, long everyoneRoleId = 0, bool isAdmin = false)
    {
        JsonArray children = new();
        var menuDatas = query as MenuData[] ?? query.ToArray();

        IEnumerable<MenuData> dataRows;
        if (isAdmin)
        {
            // 管理员获取所有子菜单
            dataRows = menuDatas.Where(b => b.Pid.Equals(id))
                .DistinctBy(p => p.ItemId)
                .OrderBy(o => o.Order);
        }
        else
        {
            // 普通用户获取有权限的子菜单
            dataRows = menuDatas.Where(b =>
                    (b.UserAcc.Equals(userAcc, StringComparison.OrdinalIgnoreCase) || b.RoleId.Equals(everyoneRoleId))
                    && b.Pid.Equals(id))
                .DistinctBy(p => p.ItemId)
                .OrderBy(o => o.Order);
        }

        foreach (var crows in dataRows)
        {
            JsonObject citem = new();
            children.Add(citem);
            citem["id"] = crows.ItemId;
            citem["pid"] = crows.Pid;
            citem["MenuName"] = crows.MenuName;
            citem["path"] = crows.MenuPath;
            citem["order"] = crows.Order;
            citem["Icon"] = crows.Icon;
            citem["IsHidden"] = crows.IsHidden;
            citem["MenuType"] = crows.MType;

            GetMenuChildren(citem, menuDatas, userAcc, crows.ItemId, everyoneRoleId, isAdmin);
        }

        if (!children.Count.Equals(0))
            item["children"] = children;
    }

    /// <summary>
    /// 检查菜单访问权限
    /// </summary>
    /// <param name="userAcc"></param>
    /// <param name="fullMenu"></param>
    /// <param name="menus"></param>
    /// <param name="everyoneRoleId">Everyone 角色的 ID（动态生成）</param>
    /// <param name="isAdmin"></param>
    /// <returns></returns>
    public static bool CheckMenuPermissions(string userAcc, string fullMenu, IEnumerable<MenuData> menus,
        long everyoneRoleId = 0, bool isAdmin = false)
    {
        // 如果是管理员，直接返回 true
        if (isAdmin)
        {
            return true;
        }
    
        // 检查是否为 Everyone 角色（公共权限）
        var result = menus.Where(b =>
            (b.UserAcc.Equals(userAcc, StringComparison.OrdinalIgnoreCase) || b.RoleId.Equals(everyoneRoleId))
            && b.Pid != 0 && b.MenuPath!.Equals(fullMenu));
        return result.Any();
    }
}

