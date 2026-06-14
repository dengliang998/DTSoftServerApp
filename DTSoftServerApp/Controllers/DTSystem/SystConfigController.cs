using DTSoft.AppService.SysConfig;

namespace DTSoftServerApp.Controllers.DTSystem;

/// <summary>
/// 系统信息接口
/// </summary>
[Authorize]
[ApiController]
[Tags("系统管理")]
[Route("api/[controller]/[action]")]
public class SysConfigController : Controller
{
    private readonly SysConfigApp _sysconfig;
    
    /// <summary>
    /// SysConfigController
    /// </summary>
    /// <param name="sysconfig"></param>
    public SysConfigController(SysConfigApp sysconfig) => _sysconfig = sysconfig;

    /// <summary>
    /// 获取系统信息
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetSystemInfo()
    {
        return Ok(_sysconfig.GetSysConfig());
    }

    /// <summary>
    /// 设置系统信息
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> SetSystemInfo([FromForm] DTSoft.Models.Parameter.SysConfig.Config systemInfo)
    {
        return Ok(await _sysconfig.SetSysConfig(systemInfo));
    }

    /// <summary>
    /// 初始化系统
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult InitializationSystem()
    {
        return Ok(_sysconfig.InitializationSystem());
    }
}
