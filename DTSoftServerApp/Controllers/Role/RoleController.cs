using DTSoft.AppService.Role;
using DTSoft.Core.Common;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Base;
using DTSoft.Models.Parameter.Role;

namespace DTSoftServerApp.Controllers.Role;

/// <summary>
/// 角色管理
/// </summary>
[Authorize]
[ApiController]
[Tags("角色管理")]
[Route("api/[controller]/[action]")]
public class RoleController : Controller
{
    private readonly RoleApp _role;
    /// <summary>
    /// RoleController
    /// </summary>
    /// <param name="role"></param>
    public RoleController(RoleApp role) => _role = role;

    /// <summary>
    /// 获取角色列表
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public IActionResult GetRoleList([FromForm] Para obj)
    {
        return Ok(_role.GetRoleList(obj));
    }

    /// <summary>
    /// 获取角色
    /// </summary>
    /// <param name="RoleId"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetRole(long RoleId)
    {
        return Ok(await _role.GetRoleAsync(RoleId));
    }

    /// <summary>
    /// 创建角色
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> CreateRole([FromForm] RoleBase obj)
    {
        return Ok(await _role.CreateRole(obj));
    }

    /// <summary>
    /// 修改角色信息
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> ModifyRoleInfo([FromForm] ModifyRole obj)
    {
        return Ok(await _role.ModifyRoleInfo(obj, DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 删除角色
    /// </summary>
    /// <param name="roleId"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> DelRole([FromForm] long roleId)
    {
        return Ok(await _role.DelRole(roleId, DtSoftHelper.GetLoginUserAccount(User)));
    }
    /// <summary>
    /// 获取角色成员列表
    /// </summary>
    /// <param name="roleId"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> GetRoleMemberList([FromForm] long roleId)
    {
        return Ok(await _role.GetRoleMemberListAsync(roleId));
    }

    /// <summary>
    /// 角色添加成员
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> AddRoleMember([FromBody] RoleMenber obj)
    {
        return Ok(await _role.AddRoleMember(obj, DtSoftHelper.GetLoginUserAccount(User)));
    }
}
