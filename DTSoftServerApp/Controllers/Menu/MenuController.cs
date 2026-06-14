using DTSoft.AppService.Menu;
using DTSoft.Core.Common;
using DTSoft.Models.Parameter.Menu;

namespace DTSoftServerApp.Controllers.Menu;

/// <summary>
/// 菜单管理
/// </summary>
[Authorize]
[ApiController]
[Tags("菜单管理")]
[Route("api/[controller]/[action]")]
public class MenuController : Controller
{
    private readonly MenuApp _menu;
    /// <summary>
    /// MenuController
    /// </summary>
    /// <param name="menu"></param>
    public MenuController(MenuApp menu) =>
        //获取token
        //_token = Convert.ToString(httpContextAccessor.HttpContext.Request.Headers["Authorization"]);
        _menu = menu;

    /// <summary>
    /// 获取菜单
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetMenu()
    {
        return Ok(await _menu.GetMenuAsync(DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 获取一级菜单
    /// </summary>
    /// <param name="obj">MenuList</param>
    /// <returns></returns>
    [HttpPost]
    public IActionResult GetFatherMenu([FromBody] MenuList obj)
    {
        return Ok(_menu.GetFatherMenu(obj));
    }

    /// <summary>
    /// 获取子菜单
    /// </summary>
    /// <param name="obj">父对象</param>
    /// <returns></returns>
    [HttpPost]
    public IActionResult GetChildrenMenu([FromBody] MenuList obj)
    {
        return Ok(_menu.GetChildrenMenu(obj));
    }

    /// <summary>
    /// 获取角色和菜单对应关系
    /// </summary>
    /// <param name="RoleId">角色ID</param>
    /// <returns></returns>
    [HttpPost]
    public IActionResult GetRoleMenuMap(long RoleId)
    {
        return Ok(_menu.GetRoleMenuMap(RoleId));
    }

    /// <summary>
    /// 更新角色对应的菜单权限
    /// </summary>
    /// <param name="menulist"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> UpdateMenuAuthority([FromBody] MenuList menulist)
    {
        return Ok(await _menu.UpdateMenuAuthority(menulist, DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 获取自定义菜单url
    /// </summary>
    /// <param name="pageCode"></param>
    /// <returns></returns>
    [HttpPost]
    public IActionResult GetMenuUrl(string pageCode)
    {
        return Ok(_menu.GetMenuUrl(pageCode));
    }

    /// <summary>
    /// 检查是否有菜单访问权限
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> CheckMenuPermissions([FromForm] CheckMenuPermissions obj)
    {
        return Ok(await _menu.CheckMenuPermissionsAsync(obj));
    }

    /// <summary>
    /// 添加菜单
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> AddMenu([FromForm] AddMenu obj)
    {
        return Ok(await _menu.AddMenu(obj, DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 修改菜单
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> UpdateMenu([FromForm] UpdateMenuParameter obj)
    {
        return Ok(await _menu.UpdateMenu(obj, DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 删除菜单
    /// </summary>
    /// <param name="menuId"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> DelMenu([FromForm] long menuId)
    {
        return Ok(await _menu.DelMenu(menuId, DtSoftHelper.GetLoginUserAccount(User)));
    }
}
