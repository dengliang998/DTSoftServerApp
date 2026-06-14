using DTSoft.AppService.Log;
using DTSoft.Models.Parameter.Log;

namespace DTSoftServerApp.Controllers.Log;

/// <summary>
/// 日志接口
/// </summary>
[Authorize]
[ApiController]
[Tags("日志管理")]
[Route("api/[controller]/[action]")]
public class LogController : Controller
{
    readonly LogApp _logList;
    /// <summary>
    /// LogController
    /// </summary>
    /// <param name="logList"></param>
    public LogController(LogApp logList) => _logList = logList;

    /// <summary>
    /// 获取接口请求日志列表
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> GetLogActionList([FromForm] LogAction obj)
    {
        return Ok(await _logList.GetLogActionListAsync(obj));
    }
}
