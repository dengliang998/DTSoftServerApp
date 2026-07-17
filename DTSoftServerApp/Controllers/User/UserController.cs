using DTSoft.AppService.User;
using DTSoft.Core.Common;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Attachment;
using DTSoft.Models.Parameter.Base;
using DTSoft.Models.Parameter.User;

namespace DTSoftServerApp.Controllers.User;

/// <summary>
/// 用户管理
/// </summary>
/// <param name="user"></param>
[Authorize]
[ApiController]
[Tags("用户管理")]
[Route("api/[controller]/[action]")]
public class UserController(UserApp user) : Controller
{
    /// <summary>
    /// 获取用户信息
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> GetUserInfo() => Ok(await user.GetUserInfoAsync(DtSoftHelper.GetLoginUserAccount(User)));

    /// <summary>
    /// 获取用户列表
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> GetUserList([FromForm] Para obj)
    {
        return Ok(await user.GetUserListAsync(obj));
    }

    /// <summary>
    /// 根据账号获取用户详细信息
    /// </summary>
    /// <param name="account">用户账号</param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> GetUserDetailByAccount([FromForm] string account)
    {
        return Ok(await user.GetUserDetailByAccountAsync(account));
    }

    /// <summary>
    /// 获取在线用户列表
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> GetOnlineUsers()
    {
        return Ok(await user.GetOnlineUsersAsync());
    }

    /// <summary>
    /// 创建用户
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromForm] UserDto obj)
    {
        return Ok(await user.CreateUser(obj));
    }

    /// <summary>
    /// 修改用户信息
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> ModifyUserInfo([FromForm] UserDto obj)
    {
        var updateSupervisor = Request.HasFormContentType && Request.Form.ContainsKey(nameof(UserDto.SupervisorAcc));
        return Ok(await user.ModifyUserInfo(obj, DtSoftHelper.GetLoginUserAccount(User), updateSupervisor));
    }

    /// <summary>
    /// 删除用户
    /// </summary>
    /// <param name="account"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> DelUser([FromForm] string account)
    {
        return Ok(await user.DelUser(account, DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 上传用户头像
    /// </summary>
    /// <param name="objFile"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost]
    public IActionResult UploadAvatar([FromForm] FileUploadApi objFile)
    {
        return Ok(user.UploadAvatar(objFile));
    }

    /// <summary>
    /// 获取用户头像
    /// </summary>
    /// <param name="account"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetUserAvatar(string account)
    {
        JsonObject rv =await user.GetUserAvatar(account);
        string filePath = Convert.ToString(rv["FilePath"])!;
        if (filePath is "")
        {
            return Ok(rv);
        }
        return PhysicalFile(filePath, "application/octet-stream");
    }

    /// <summary>
    /// 重置密码
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> ResetPassword([FromForm] ResetPassword obj)
    {
        return Ok(await user.ResetPassword(obj, DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> ModifyPassword([FromForm] ModifyPassword obj)
    {
        return Ok(await user.ModifyPassword(obj));
    }
}
