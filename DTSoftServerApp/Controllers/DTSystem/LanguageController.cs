using DTSoft.AppService.Language;
using DTSoft.Core.Common;
using DTSoft.Models.Parameter.Language;

namespace DTSoftServerApp.Controllers.DTSystem;

[Authorize]
[ApiController]
[Tags("系统管理")]
[Route("api/[controller]/[action]")]
public class LanguageController(LanguageApp languageApp) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLanguages()
    {
        return Ok(await languageApp.GetLanguagesAsync());
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetEnabledLanguages()
    {
        return Ok(await languageApp.GetEnabledLanguagesAsync());
    }

    [HttpPost]
    public async Task<IActionResult> SaveLanguage([FromBody] LanguageSaveParameter parameter)
    {
        return Ok(await languageApp.SaveLanguageAsync(parameter, DtSoftHelper.GetLoginUserAccount(User)));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteLanguage([FromQuery] long itemId)
    {
        return Ok(await languageApp.DeleteLanguageAsync(itemId, DtSoftHelper.GetLoginUserAccount(User)));
    }

    [HttpGet]
    public async Task<IActionResult> GetLanguageResources()
    {
        return Ok(await languageApp.GetLanguageResourcesAsync());
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetLanguageResourceValues([FromQuery] string? languageCode)
    {
        return Ok(await languageApp.GetLanguageResourceValuesAsync(languageCode));
    }

    [HttpPost]
    public async Task<IActionResult> SaveLanguageResource([FromBody] LanguageResourceSaveParameter parameter)
    {
        return Ok(await languageApp.SaveLanguageResourceAsync(parameter, DtSoftHelper.GetLoginUserAccount(User)));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteLanguageResource([FromQuery] long itemId)
    {
        return Ok(await languageApp.DeleteLanguageResourceAsync(itemId, DtSoftHelper.GetLoginUserAccount(User)));
    }
}
