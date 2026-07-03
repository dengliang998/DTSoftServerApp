using DTSoft.AppService.Ou;
using DTSoft.Core.Common;
using DTSoft.Models.Parameter.Base;
using DTSoft.Models.Parameter.Ou;

namespace DTSoftServerApp.Controllers.Ou;

/// <summary>
/// 部门管理
/// </summary>
[Authorize]
[ApiController]
[Tags("部门管理")]
[Route("api/[controller]/[action]")]
public class OuController : Controller
{
    private readonly OuApp _ou;
    
    /// <summary>
    /// OuController
    /// </summary>
    /// <param name="ou"></param>
    public OuController(OuApp ou) => _ou = ou;

    /// <summary>
    /// 获取部门列表（分页）
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> GetOuList([FromForm] Para obj)
    {
        return Ok(await _ou.GetOuListAsync(obj));
    }

    /// <summary>
    /// 获取所有部门（树形结构）
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetAllOus()
    {
        return Ok(await _ou.GetAllOusAsync());
    }

    /// <summary>
    /// 获取部门详情
    /// </summary>
    /// <param name="OuId"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetOu(long OuId)
    {
        return Ok(await _ou.GetOuAsync(OuId));
    }

    /// <summary>
    /// 创建部门
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> CreateOu([FromForm] OuDto obj)
    {
        return Ok(await _ou.CreateOu(obj));
    }

    /// <summary>
    /// 修改部门信息
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> ModifyOuInfo([FromForm] OuDto obj)
    {
        return Ok(await _ou.ModifyOuInfo(obj, DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 删除部门
    /// </summary>
    /// <param name="OuId"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> DelOu([FromForm] long OuId)
    {
        return Ok(await _ou.DelOu(OuId, DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 获取部门成员列表
    /// </summary>
    /// <param name="OuId"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> GetOuMemberList([FromForm] long OuId)
    {
        return Ok(await _ou.GetOuMemberListAsync(OuId));
    }

    /// <summary>
    /// 部门添加成员
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> AddOuMember([FromBody] OuMember obj)
    {
        return Ok(await _ou.AddOuMember(obj, DtSoftHelper.GetLoginUserAccount(User)));
    }
}
