using DTSoft.AppService.DynamicApp;
using DTSoft.Models.Parameter.DynamicApp;

namespace DTSoftServerApp.Controllers.DynamicApp
{
    /// <summary>
    /// 动态应用接口
    /// </summary>
    /// <param name="dynamicConfigApp"></param>
    [Authorize]
    [ApiController]
    [Tags("动态应用")]
    [Route("api/[controller]")]
    public class DynamicAppController(DynamicConfigApp dynamicConfigApp) : ControllerBase
    {

        /// <summary>
        /// 获取App配置列表
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="modelName">模块名称</param>
        /// <param name="pageNum">页码</param>
        /// <param name="pageSize">每页条数</param>
        /// <returns>App配置列表</returns>
        [HttpGet("GetDynamicAppConfigs")]
        public async Task<IActionResult> GetDynamicAppConfigs([FromQuery] string? keyword, [FromQuery] string? modelName, [FromQuery] int? pageNum = 1,
            [FromQuery] int? pageSize = 10)
        {
            try
            {
                var parameter = new CrudConfigQueryParameter
                {
                    Keyword = keyword,
                    ModelName = modelName,
                    PageNum = pageNum,
                    PageSize = pageSize
                };

                var result = await dynamicConfigApp.GetDynamicAppConfigs(parameter);

                return Ok(new { success = true, msg = "获取成功", data = result.Data, total = result.Total });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, msg = $"获取失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 添加App配置
        /// </summary>
        /// <param name="parameter">添加参数</param>
        /// <returns>添加的App配置</returns>
        [HttpPost("AddDynamicAppConfig")]
        public async Task<IActionResult> AddDynamicAppConfig([FromBody] CrudConfigAddParameter parameter)
        {
            try
            {
                // 验证参数
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Ok(new
                    {
                        success = false,
                        msg = string.Join(";", errors)
                    });
                }

                var result =await dynamicConfigApp.AddDynamicAppConfig(parameter);

                return Ok(new
                {
                    success = true,
                    msg = "添加成功",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    msg = $"添加失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 更新App配置
        /// </summary>
        /// <param name="parameter">更新参数</param>
        /// <returns>更新结果</returns>
        [HttpPost("UpdateDynamicAppConfig")]
        public async Task<IActionResult> UpdateDynamicAppConfig([FromBody] CrudConfigUpdateParameter parameter)
        {
            try
            {
                // 验证参数
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Ok(new
                    {
                        success = false,
                        msg = string.Join(";", errors)
                    });
                }

                var result =await dynamicConfigApp.UpdateDynamicAppConfig(parameter);

                return Ok(new
                {
                    success = true,
                    msg = "更新成功",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    msg = $"更新失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 删除App配置
        /// </summary>
        /// <param name="parameter">删除参数</param>
        /// <returns>删除结果</returns>
        [HttpPost("DeleteDynamicAppConfig")]
        public async Task<IActionResult> DeleteDynamicAppConfig([FromBody] CrudConfigDeleteParameter parameter)
        {
            try
            {
                // 验证参数
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Ok(new
                    {
                        success = false,
                        msg = string.Join(";", errors)
                    });
                }

                await dynamicConfigApp.DeleteDynamicAppConfig(parameter);

                return Ok(new
                {
                    success = true,
                    msg = "删除成功"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    msg = $"删除失败: {ex.Message}"
                });
            }
        }
    }
}
