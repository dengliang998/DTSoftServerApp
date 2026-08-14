using DTSoft.AppService.Localization;
using DTSoft.Core.Common;
using DTSoftServerApp.Plugins;
using System.Text.Json;

namespace DTSoftServerApp.Controllers.Plugin;

/// <summary>
/// 动态插件管理
/// </summary>
[Authorize]
[ApiController]
[Tags("Plugin Management")]
[Route("api/[controller]/[action]")]
public class PluginController(
    DynamicWebApiPluginCatalog pluginCatalog,
    IAppLocalizer localizer) : Controller
{
    /// <summary>
    /// 获取已上传插件列表
    /// </summary>
    [HttpGet]
    public IActionResult GetPlugins()
    {
        return Ok(new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = JsonSerializer.SerializeToNode(pluginCatalog.List())
        });
    }

    /// <summary>
    /// 上传插件 DLL 或 ZIP 包
    /// </summary>
    [HttpPost]
    public IActionResult Upload([FromForm] IFormFile? file)
    {
        try
        {
            var item = pluginCatalog.AddUploadedPlugin(file, DtSoftHelper.GetLoginUserAccount(User));
            return Ok(new JsonObject
            {
                ["success"] = true,
                ["StateCode"] = 0,
                ["Msg"] = localizer["plugin.uploadSuccessRestartRequired"],
                ["data"] = JsonSerializer.SerializeToNode(item)
            });
        }
        catch (Exception ex)
        {
            return Ok(new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 400,
                ["Msg"] = ex.Message
            });
        }
    }

    /// <summary>
    /// 启用插件
    /// </summary>
    [HttpPost]
    public IActionResult Enable([FromForm] string id)
    {
        return SetEnabled(id, true);
    }

    /// <summary>
    /// 停用插件
    /// </summary>
    [HttpPost]
    public IActionResult Disable([FromForm] string id)
    {
        return SetEnabled(id, false);
    }

    /// <summary>
    /// 删除插件
    /// </summary>
    [HttpPost]
    public IActionResult Remove([FromForm] string id)
    {
        try
        {
            pluginCatalog.Remove(id);
            return Ok(new JsonObject
            {
                ["success"] = true,
                ["StateCode"] = 0,
                ["Msg"] = localizer["plugin.deleteSuccessRestartRequired"]
            });
        }
        catch (Exception ex)
        {
            return Ok(new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 400,
                ["Msg"] = ex.Message
            });
        }
    }

    private IActionResult SetEnabled(string id, bool enabled)
    {
        try
        {
            var item = pluginCatalog.SetEnabled(id, enabled);
            return Ok(new JsonObject
            {
                ["success"] = true,
                ["StateCode"] = 0,
                ["Msg"] = localizer["plugin.statusUpdatedRestartRequired"],
                ["data"] = JsonSerializer.SerializeToNode(item)
            });
        }
        catch (Exception ex)
        {
            return Ok(new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 400,
                ["Msg"] = ex.Message
            });
        }
    }
}
