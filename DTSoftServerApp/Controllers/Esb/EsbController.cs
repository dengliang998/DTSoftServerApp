using DTSoft.AppService.Esb;
using DTSoft.Core.Common;
using DTSoft.Models.Parameter.Esb;

namespace DTSoftServerApp.Controllers.Esb;

/// <summary>
/// ESB 数据源接口。
/// </summary>
[Authorize]
[Tags("ESB")]
[Route("api/[controller]/[action]")]
public class EsbController(EsbDataSourceApp esbDataSourceApp, EsbServiceConnectionApp esbServiceConnectionApp) : ControllerBase
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

            return Ok(new { success = true, msg = "获取成功", data = result.Data, total = result.Total });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"获取失败: {ex.Message}" });
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
            return Ok(new { success = true, msg = "获取成功", data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"获取失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取支持的数据库类型。
    /// </summary>
    [HttpGet]
    public IActionResult GetSupportedDatabaseTypes()
    {
        return Ok(new { success = true, msg = "获取成功", data = EsbServiceConnectionApp.GetSupportedDatabaseTypes() });
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
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Ok(new { success = false, msg = string.Join(";", errors.DefaultIfEmpty("请求参数不能为空")) });
            }

            var result = await esbServiceConnectionApp.AddConnection(parameter);
            return Ok(new { success = true, msg = "添加成功", data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"添加失败: {ex.Message}" });
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
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Ok(new { success = false, msg = string.Join(";", errors.DefaultIfEmpty("请求参数不能为空")) });
            }

            var result = await esbServiceConnectionApp.UpdateConnection(parameter);
            return Ok(new { success = true, msg = "更新成功", data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"更新失败: {ex.Message}" });
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
            if (parameter == null) return Ok(new { success = false, msg = "请求参数不能为空" });

            await esbServiceConnectionApp.DeleteConnection(parameter.ItemId);
            return Ok(new { success = true, msg = "删除成功" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"删除失败: {ex.Message}" });
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
            if (parameter == null) return Ok(new { success = false, msg = "请求参数不能为空" });

            await esbServiceConnectionApp.TestConnection(parameter);
            return Ok(new { success = true, msg = "连接成功" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"连接失败: {ex.Message}" });
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

            return Ok(new { success = true, msg = "获取成功", data = result.Data, total = result.Total });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"获取失败: {ex.Message}" });
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
            return Ok(new { success = true, msg = "获取成功", data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"获取失败: {ex.Message}" });
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
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Ok(new { success = false, msg = string.Join(";", errors.DefaultIfEmpty("请求参数不能为空")) });
            }

            var result = await esbDataSourceApp.AddDataSource(parameter);
            return Ok(new { success = true, msg = "添加成功", data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"添加失败: {ex.Message}" });
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
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Ok(new { success = false, msg = string.Join(";", errors.DefaultIfEmpty("请求参数不能为空")) });
            }

            var result = await esbDataSourceApp.UpdateDataSource(parameter);
            return Ok(new { success = true, msg = "更新成功", data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"更新失败: {ex.Message}" });
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
            if (parameter == null) return Ok(new { success = false, msg = "请求参数不能为空" });

            await esbDataSourceApp.DeleteDataSource(parameter.ItemId);
            return Ok(new { success = true, msg = "删除成功" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"删除失败: {ex.Message}" });
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
            if (request == null) return Ok(new { success = false, msg = "请求参数不能为空" });

            var result = await esbDataSourceApp.Execute(request, DtSoftHelper.GetLoginUserAccount(User));
            return Ok(new { success = true, msg = "执行成功", data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = $"执行失败: {ex.Message}" });
        }
    }
}
