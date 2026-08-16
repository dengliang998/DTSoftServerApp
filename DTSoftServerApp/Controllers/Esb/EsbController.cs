using DTSoft.AppService.Esb;
using DTSoft.AppService.Localization;
using DTSoft.Core.Common;
using DTSoft.Models.Parameter.Esb;
using DTSoftServerApp.Extensions;

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

            return Success(localizer["common.fetchSuccess"], result.Data, result.Total);
        }
        catch (Exception ex)
        {
            return Failure(localizer["common.fetchFailed"], ex);
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
            return Success(localizer["common.fetchSuccess"], result);
        }
        catch (Exception ex)
        {
            return Failure(localizer["common.fetchFailed"], ex);
        }
    }

    /// <summary>
    /// 获取支持的数据库类型。
    /// </summary>
    [HttpGet]
    public IActionResult GetSupportedDatabaseTypes()
    {
        return Success(localizer["common.fetchSuccess"], EsbServiceConnectionApp.GetSupportedDatabaseTypes());
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
                return InvalidArguments();
            }

            var result = await esbServiceConnectionApp.AddConnection(parameter);
            return Success(localizer["common.addSuccess"], result);
        }
        catch (Exception ex)
        {
            return Failure(localizer["common.addFailed"], ex);
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
                return InvalidArguments();
            }

            var result = await esbServiceConnectionApp.UpdateConnection(parameter);
            return Success(localizer["common.updateSuccess"], result);
        }
        catch (Exception ex)
        {
            return Failure(localizer["common.updateFailed"], ex);
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
            if (parameter == null) return InvalidArguments();

            await esbServiceConnectionApp.DeleteConnection(parameter.ItemId);
            return Success(localizer["common.deleteSuccess"]);
        }
        catch (Exception ex)
        {
            return Failure(localizer["common.deleteFailed"], ex);
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
            if (parameter == null) return InvalidArguments();

            await esbServiceConnectionApp.TestConnection(parameter);
            return Success(localizer["esb.connectionTestSuccess"]);
        }
        catch (Exception ex)
        {
            return Failure(localizer["esb.connectionTestFailed"], ex);
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

            return Success(localizer["common.fetchSuccess"], result.Data, result.Total);
        }
        catch (Exception ex)
        {
            return Failure(localizer["common.fetchFailed"], ex);
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
            return Success(localizer["common.fetchSuccess"], result);
        }
        catch (Exception ex)
        {
            return Failure(localizer["common.fetchFailed"], ex);
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
                return InvalidArguments();
            }

            var result = await esbDataSourceApp.AddDataSource(parameter);
            return Success(localizer["common.addSuccess"], result);
        }
        catch (Exception ex)
        {
            return Failure(localizer["common.addFailed"], ex);
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
                return InvalidArguments();
            }

            var result = await esbDataSourceApp.UpdateDataSource(parameter);
            return Success(localizer["common.updateSuccess"], result);
        }
        catch (Exception ex)
        {
            return Failure(localizer["common.updateFailed"], ex);
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
            if (parameter == null) return InvalidArguments();

            await esbDataSourceApp.DeleteDataSource(parameter.ItemId);
            return Success(localizer["common.deleteSuccess"]);
        }
        catch (Exception ex)
        {
            return Failure(localizer["common.deleteFailed"], ex);
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
            if (request == null) return InvalidArguments();

            var result = await esbDataSourceApp.Execute(request, DtSoftHelper.GetLoginUserAccount(User));
            return Success(localizer["common.executeSuccess"], result);
        }
        catch (Exception ex)
        {
            return Failure(localizer["common.executeFailed"], ex);
        }
    }

    private IActionResult Success(string message)
    {
        return Ok(new { success = true, msg = message });
    }

    private IActionResult Success(string message, object? data)
    {
        return Ok(new { success = true, msg = message, data });
    }

    private IActionResult Success(string message, object? data, int total)
    {
        return Ok(new { success = true, msg = message, data, total });
    }

    private IActionResult Failure(string message, Exception exception)
    {
        return Ok(new { success = false, msg = $"{message}: {exception.Message}" });
    }

    private IActionResult InvalidArguments()
    {
        var errors = ModelState.GetErrorMessages();
        return Ok(new { success = false, msg = string.Join(";", errors.DefaultIfEmpty(localizer["common.argumentMissing"])) });
    }
}
