using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Base;
using DTSoft.Models.Parameter.Ou;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace DTSoft.AppService.Ou;

/// <summary>
/// 部门管理服务
/// </summary>
public class OuApp(SysDbContext dbContext, DtSoftHelper dtSoftHelper, UserCacheHelper userCacheHelper, IDtSoftCache dtSoftCache)
{
    private const string DepartmentTreeCacheKey = "Department:Tree";

    /// <summary>
    /// 获取部门列表（树形结构）
    /// </summary>
    public async Task<JsonObject> GetDepartmentListAsync(Para obj)
    {
        IQueryable<SysOu>? query = dbContext.SysOu;
        
        if (query == null)
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
        
        // 关键词搜索
        if (!string.IsNullOrEmpty(obj.Keyword))
        {
            query = query.Where(b => b.OuName!.Contains(obj.Keyword) || 
                                     b.OuCode!.Contains(obj.Keyword));
        }
        
        var total = await query.CountAsync();
        var result = await query.OrderBy(o => o.SortOrder)
                               .ThenBy(o => o.ItemId)
                               .Skip(obj.PageSize * (obj.PageNum - 1))
                               .Take(obj.PageSize)
                               .ToListAsync();

        // 构建树形结构
        var treeData = BuildDepartmentTree(result);

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["Total"] = total,
            ["data"] = treeData
        };
    }

    /// <summary>
    /// 获取所有部门（树形结构，不分页）
    /// </summary>
    public async Task<JsonObject> GetAllDepartmentsAsync()
    {
        if (dbContext.SysOu == null)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "系统错误",
                ["data"] = new JsonArray()
            };
        }

        var cachedJson = await dtSoftCache.GetAsync<string>(DepartmentTreeCacheKey);
        if (!string.IsNullOrWhiteSpace(cachedJson))
        {
            try
            {
                var cached = JsonNode.Parse(cachedJson) as JsonArray;
                if (cached is not null)
                {
                    return new JsonObject
                    {
                        ["success"] = true,
                        ["StateCode"] = 0,
                        ["data"] = cached
                    };
                }
            }
            catch
            {
                // ignore
            }
        }

        var departments = await dbContext.SysOu
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.ItemId)
            .ToListAsync();

        var treeData = BuildDepartmentTree(departments);
        await dtSoftCache.SetAsync(DepartmentTreeCacheKey, treeData.ToJsonString(), TimeSpan.FromMinutes(5));

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = treeData
        };
    }

    /// <summary>
    /// 获取单个部门信息
    /// </summary>
    public async Task<JsonObject> GetDepartmentAsync(long departmentId)
    {
        if (dbContext.SysOu == null)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "系统错误"
            };
        }
        
        var department = await dbContext.SysOu
            .FirstOrDefaultAsync(b => b.ItemId == departmentId);
        
        if (department != null)
        {
            return new JsonObject
            {
                ["success"] = true,
                ["StateCode"] = 0,
                ["ItemId"] = department.ItemId,
                ["OuName"] = department.OuName,
                ["OuCode"] = department.OuCode,
                ["ParentId"] = department.ParentId,
                ["SortOrder"] = department.SortOrder,
                ["Disable"] = department.Disable,
                ["Remark"] = department.Remark
            };
        }
        else
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "部门不存在"
            };
        }
    }

    /// <summary>
    /// 创建部门
    /// </summary>
    public async Task<JsonObject> CreateDepartment(OuDto ouDto)
    {
        JsonObject rv = new()
        {
            ["StateCode"] = 0,
            ["success"] = true
        };

        if (string.IsNullOrEmpty(ouDto.OuName))
        {
            rv["success"] = false;
            rv["Msg"] = "部门名称不能为空";
            return rv;
        }

        // 检查部门编码是否重复
        if (!string.IsNullOrEmpty(ouDto.OuCode))
        {
            var exists = await dbContext.SysOu!
                .AnyAsync(b => b.OuCode == ouDto.OuCode.Trim());
            if (exists)
            {
                rv["success"] = false;
                rv["Msg"] = $"部门编码：{ouDto.OuCode}已存在";
                return rv;
            }
        }

        dbContext.SysOu!.Add(new SysOu 
        { 
            ItemId = YitterHelper.NewId(), 
            OuName = ouDto.OuName,
            OuCode = ouDto.OuCode,
            ParentId = ouDto.ParentId,
            SortOrder = ouDto.SortOrder,
            Disable = ouDto.Disable,
            Remark = ouDto.Remark
        });
        
        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache(DepartmentTreeCacheKey);
        rv["Msg"] = "部门创建成功";
        
        return rv;
    }

    /// <summary>
    /// 修改部门信息
    /// </summary>
    public async Task<JsonObject> ModifyDepartmentInfo(OuDto ouDto, string loginUserAcc)
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

        if (string.IsNullOrEmpty(ouDto.OuName))
        {
            rv["success"] = false;
            rv["Msg"] = "部门名称不能为空";
            return rv;
        }

        var department = await dbContext.SysOu!
            .FirstOrDefaultAsync(p => p.ItemId == ouDto.ItemId);
            
        if (department == null)
        {
            rv["success"] = false;
            rv["Msg"] = "部门不存在";
            return rv;
        }

        // 检查部门编码是否重复（排除自己）
        if (!string.IsNullOrEmpty(ouDto.OuCode))
        {
            var exists = await dbContext.SysOu!
                .AnyAsync(b => b.OuCode == ouDto.OuCode.Trim() && b.ItemId != ouDto.ItemId);
            if (exists)
            {
                rv["success"] = false;
                rv["Msg"] = "部门编码已存在";
                return rv;
            }
        }

        department.OuName = ouDto.OuName;
        department.OuCode = ouDto.OuCode;
        department.ParentId = ouDto.ParentId;
        department.SortOrder = ouDto.SortOrder;
        department.Disable = ouDto.Disable;
        department.Remark = ouDto.Remark;

        await dbContext.SaveChangesAsync();
        dtSoftCache.RefreshCache(DepartmentTreeCacheKey);
        rv["Msg"] = "修改成功";
        
        return rv;
    }

    /// <summary>
    /// 删除部门
    /// </summary>
    public async Task<JsonObject> DelDepartment(long departmentId, string loginUserAcc)
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

        // 检查是否有子部门
        var hasChildren = await dbContext.SysOu!
            .AnyAsync(p => p.ParentId == departmentId);
        if (hasChildren)
        {
            rv["success"] = false;
            rv["Msg"] = "存在子部门，不能删除";
            return rv;
        }

        // 检查是否有部门成员
        var hasMembers = await dbContext.SysUserMember!
            .AnyAsync(p => p.OuId == departmentId);
        if (hasMembers)
        {
            rv["success"] = false;
            rv["Msg"] = "部门中存在成员，请先移除成员";
            return rv;
        }

        // 删除部门
        var department = await dbContext.SysOu!
            .FirstOrDefaultAsync(p => p.ItemId == departmentId);
        if (department != null)
        {
            dbContext.SysOu!.Remove(department);
            await dbContext.SaveChangesAsync();
        }
        dtSoftCache.RefreshCache(DepartmentTreeCacheKey);
        
        rv["Msg"] = "删除成功";
        
        return rv;
    }

    /// <summary>
    /// 获取部门成员列表
    /// </summary>
    public async Task<JsonObject> GetDepartmentMemberListAsync(long departmentId)
    {
        JsonObject rv = new();
        JsonArray children = new();
        rv["StateCode"] = 0;
        rv["success"] = true;

        // 查询部门成员
        var departmentMembers = await dbContext.SysUserMember!
            .Where(b => b.OuId == departmentId)
            .ToListAsync();

        // 提取所有需要的用户账号
        var userAccounts = departmentMembers.Select(m => m.UserAcc).Distinct().ToList();
        var users = await userCacheHelper.GetUsersByAccountsAsync(userAccounts);
        var userLookup = users.ToDictionary(u => u.Account!, u => u.DisplayName!);

        foreach (var member in departmentMembers)
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
    /// 添加部门成员
    /// </summary>
    public async Task<JsonObject> AddDepartmentMember(OuMember departmentMember, string loginUserAcc)
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

        // 删除现有成员
        await dbContext.SysUserMember!
            .Where(b => b.OuId == departmentMember.OuId)
            .ExecuteDeleteAsync();

        // 添加新成员
        if (departmentMember.UserAccounts?.Count > 0)
        {
            var members = departmentMember.UserAccounts
                .Select(userAcc => new SysUserMember
                {
                    ItemId = YitterHelper.NewId(),
                    OuId = departmentMember.OuId,
                    UserAcc = userAcc
                })
                .ToList();

            dbContext.SysUserMember!.AddRange(members);
        }
        
        await dbContext.SaveChangesAsync();
        rv["Msg"] = "添加成功";
        
        return rv;
    }

    /// <summary>
    /// 构建部门树形结构
    /// </summary>
    private JsonArray BuildDepartmentTree(List<SysOu> departments)
    {
        var treeArray = new JsonArray();
        var departmentLookup = departments.ToDictionary(d => d.ItemId);

        // 找出所有根节点（ParentId 为 null 或 0）
        var rootDepartments = departments.Where(d => d.ParentId == null || d.ParentId == 0).ToList();

        foreach (var root in rootDepartments)
        {
            treeArray.Add(BuildDepartmentNode(root, departmentLookup));
        }

        return treeArray;
    }

    /// <summary>
    /// 构建单个部门节点（递归）
    /// </summary>
    private JsonObject BuildDepartmentNode(SysOu department, Dictionary<long, SysOu> lookup)
    {
        var node = new JsonObject
        {
            ["ItemId"] = department.ItemId,
            ["OuName"] = department.OuName,
            ["OuCode"] = department.OuCode,
            ["ParentId"] = department.ParentId,
            ["SortOrder"] = department.SortOrder,
            ["Disable"] = department.Disable,
            ["Remark"] = department.Remark
        };

        // 查找子部门
        var children = lookup.Values
            .Where(d => d.ParentId == department.ItemId)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.ItemId)
            .ToList();

        if (children.Count > 0)
        {
            var childrenArray = new JsonArray();
            foreach (var child in children)
            {
                childrenArray.Add(BuildDepartmentNode(child, lookup));
            }
            node["Children"] = childrenArray;
        }

        return node;
    }
}
