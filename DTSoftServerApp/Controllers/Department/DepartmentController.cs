using DTSoft.AppService.Department;
using DTSoft.Core.Common;
using DTSoft.Models.Parameter.Base;
using DTSoft.Models.Parameter.Department;

namespace DTSoftServerApp.Controllers.Department;

/// <summary>
/// 部门管理
/// </summary>
[Authorize]
[ApiController]
[Tags("部门管理")]
[Route("api/[controller]/[action]")]
public class DepartmentController : Controller
{
    private readonly DepartmentApp _department;
    
    /// <summary>
    /// DepartmentController
    /// </summary>
    /// <param name="department"></param>
    public DepartmentController(DepartmentApp department) => _department = department;

    /// <summary>
    /// 获取部门列表（分页）
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> GetDepartmentList([FromForm] Para obj)
    {
        return Ok(await _department.GetDepartmentListAsync(obj));
    }

    /// <summary>
    /// 获取所有部门（树形结构）
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetAllDepartments()
    {
        return Ok(await _department.GetAllDepartmentsAsync());
    }

    /// <summary>
    /// 获取部门详情
    /// </summary>
    /// <param name="departmentId"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetDepartment(long departmentId)
    {
        return Ok(await _department.GetDepartmentAsync(departmentId));
    }

    /// <summary>
    /// 创建部门
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> CreateDepartment([FromForm] DepartmentDto obj)
    {
        return Ok(await _department.CreateDepartment(obj));
    }

    /// <summary>
    /// 修改部门信息
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> ModifyDepartmentInfo([FromForm] DepartmentDto obj)
    {
        return Ok(await _department.ModifyDepartmentInfo(obj, DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 删除部门
    /// </summary>
    /// <param name="departmentId"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> DelDepartment([FromForm] long departmentId)
    {
        return Ok(await _department.DelDepartment(departmentId, DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 获取部门成员列表
    /// </summary>
    /// <param name="departmentId"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> GetDepartmentMemberList([FromForm] long departmentId)
    {
        return Ok(await _department.GetDepartmentMemberListAsync(departmentId));
    }

    /// <summary>
    /// 部门添加成员
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> AddDepartmentMember([FromBody] DepartmentMember obj)
    {
        return Ok(await _department.AddDepartmentMember(obj, DtSoftHelper.GetLoginUserAccount(User)));
    }
}
