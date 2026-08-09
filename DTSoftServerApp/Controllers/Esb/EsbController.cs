using DTSoft.AppService.Esb;
using DTSoft.AppService.Localization;
using DTSoft.Core.Common;
using DTSoft.Models.Parameter.Esb;
using DTSoftServerApp.Helpers;

namespace DTSoftServerApp.Controllers.Esb;

/// <summary>
/// ESB 数据源接口。
/// </summary>
[Authorize]
[Tags("ESB")]
[Route("api/[controller]/[action]")]
public class EsbController(EsbDataSourceApp esbDataSourceApp, EsbServiceConnectionApp esbServiceConnectionApp, IAppLocalizer localizer) : ControllerBase
{
    /// <summary>
    /// 获取 ESB 服务连接列表。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetServiceConnections(
        [FromQuery] string? keyword,
        [FromQuery] string? serviceType,
        [FromQuery] int? status,
        [FromQuery] int? pageNum = 1,
        [FromQuery] int? pageSize = 10)
    {
        try
        {
            var result = await esbServiceConnectionApp.GetConnections(new EsbServiceConnectionQueryParameter
            {
                Keyword = keyword,
                ServiceType = serviceType,
                Status = status,
                PageNum = pageNum,
                PageSize = pageSize
            });

            return Ok(new { success = true, msg = localizer["common.fetchSuccess"], data = result.Data, total = result.Total });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"{localizer["common.fetchFailed"]}: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取 ESB 服务连接选项。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetServiceConnectionOptions()
    {
        try
        {
            var result = await esbServiceConnectionApp.GetConnectionOptions();
            return Ok(new { success = true, msg = localizer["common.fetchSuccess"], data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"{localizer["common.fetchFailed"]}: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取支持的数据库类型。
    /// </summary>
    [HttpGet]
    public IActionResult GetSupportedDatabaseTypes()
    {
        return Ok(new { success = true, msg = localizer["common.fetchSuccess"], data = EsbServiceConnectionApp.GetSupportedDatabaseTypes() });
    }

    /// <summary>
    /// 新增 ESB 服务连接。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddServiceConnection([FromBody] EsbServiceConnectionAddParameter? parameter)
    {
        try
        {
            if (!ModelState.IsValid || parameter == null)
            {
                var errors = ModelStateLocalizationHelper.GetLocalizedErrors(ModelState, localizer);
                return Ok(new { success = false, msg = string.Join(";", errors.DefaultIfEmpty(localizer["common.argumentMissing"])) });
            }

            var result = await esbServiceConnectionApp.AddConnection(parameter);
            return Ok(new { success = true, msg = localizer["common.addSuccess"], data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"{localizer["common.addFailed"]}: {ex.Message}" });
        }
    }

    /// <summary>
    /// 更新 ESB 服务连接。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UpdateServiceConnection([FromBody] EsbServiceConnectionUpdateParameter? parameter)
    {
        try
        {
            if (!ModelState.IsValid || parameter == null)
            {
                var errors = ModelStateLocalizationHelper.GetLocalizedErrors(ModelState, localizer);
                return Ok(new { success = false, msg = string.Join(";", errors.DefaultIfEmpty(localizer["common.argumentMissing"])) });
            }

            var result = await esbServiceConnectionApp.UpdateConnection(parameter);
            return Ok(new { success = true, msg = localizer["common.updateSuccess"], data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"{localizer["common.updateFailed"]}: {ex.Message}" });
        }
    }

    /// <summary>
    /// 删除 ESB 服务连接。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteServiceConnection([FromBody] EsbServiceConnectionDeleteParameter? parameter)
    {
        try
        {
            if (parameter == null) return Ok(new { success = false, msg = localizer["common.argumentMissing"] });

            await esbServiceConnectionApp.DeleteConnection(parameter.ItemId);
            return Ok(new { success = true, msg = localizer["common.deleteSuccess"] });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"{localizer["common.deleteFailed"]}: {ex.Message}" });
        }
    }

    /// <summary>
    /// 测试 ESB 服务连接。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> TestServiceConnection([FromBody] EsbServiceConnectionTestParameter? parameter)
    {
        try
        {
            if (parameter == null) return Ok(new { success = false, msg = localizer["common.argumentMissing"] });

            await esbServiceConnectionApp.TestConnection(parameter);
            return Ok(new { success = true, msg = localizer["esb.connectionTestSuccess"] });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"{localizer["esb.connectionTestFailed"]}: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取 ESB 数据源列表。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDataSources(
        [FromQuery] string? keyword,
        [FromQuery] string? sourceType,
        [FromQuery] long? connectionId,
        [FromQuery] int? status,
        [FromQuery] int? pageNum = 1,
        [FromQuery] int? pageSize = 10)
    {
        try
        {
            var result = await esbDataSourceApp.GetDataSources(new EsbDataSourceQueryParameter
            {
                Keyword = keyword,
                SourceType = sourceType,
                ConnectionId = connectionId,
                Status = status,
                PageNum = pageNum,
                PageSize = pageSize
            });

            return Ok(new { success = true, msg = localizer["common.fetchSuccess"], data = result.Data, total = result.Total });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"{localizer["common.fetchFailed"]}: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取 ESB 数据源详情。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDataSourceById([FromQuery] long id)
    {
        try
        {
            var result = await esbDataSourceApp.GetDataSourceById(id);
            return Ok(new { success = true, msg = localizer["common.fetchSuccess"], data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"{localizer["common.fetchFailed"]}: {ex.Message}" });
        }
    }

    /// <summary>
    /// 新增 ESB 数据源。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddDataSource([FromBody] EsbDataSourceAddParameter? parameter)
    {
        try
        {
            if (!ModelState.IsValid || parameter == null)
            {
                var errors = ModelStateLocalizationHelper.GetLocalizedErrors(ModelState, localizer);
                return Ok(new { success = false, msg = string.Join(";", errors.DefaultIfEmpty(localizer["common.argumentMissing"])) });
            }

            var result = await esbDataSourceApp.AddDataSource(parameter);
            return Ok(new { success = true, msg = localizer["common.addSuccess"], data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"{localizer["common.addFailed"]}: {ex.Message}" });
        }
    }

    /// <summary>
    /// 更新 ESB 数据源。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UpdateDataSource([FromBody] EsbDataSourceUpdateParameter? parameter)
    {
        try
        {
            if (!ModelState.IsValid || parameter == null)
            {
                var errors = ModelStateLocalizationHelper.GetLocalizedErrors(ModelState, localizer);
                return Ok(new { success = false, msg = string.Join(";", errors.DefaultIfEmpty(localizer["common.argumentMissing"])) });
            }

            var result = await esbDataSourceApp.UpdateDataSource(parameter);
            return Ok(new { success = true, msg = localizer["common.updateSuccess"], data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"{localizer["common.updateFailed"]}: {ex.Message}" });
        }
    }

    /// <summary>
    /// 删除 ESB 数据源。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteDataSource([FromBody] EsbDataSourceDeleteParameter? parameter)
    {
        try
        {
            if (parameter == null) return Ok(new { success = false, msg = localizer["common.argumentMissing"] });

            await esbDataSourceApp.DeleteDataSource(parameter.ItemId);
            return Ok(new { success = true, msg = localizer["common.deleteSuccess"] });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"{localizer["common.deleteFailed"]}: {ex.Message}" });
        }
    }

    /// <summary>
    /// 执行 ESB 数据源。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Execute([FromBody] EsbExecuteRequest? request)
    {
        try
        {
            if (request == null) return Ok(new { success = false, msg = localizer["common.argumentMissing"] });

            var result = await esbDataSourceApp.Execute(request, DtSoftHelper.GetLoginUserAccount(User));
            return Ok(new { success = true, msg = localizer["common.executeSuccess"], data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"{localizer["common.executeFailed"]}: {ex.Message}" });
        }
    }
}
