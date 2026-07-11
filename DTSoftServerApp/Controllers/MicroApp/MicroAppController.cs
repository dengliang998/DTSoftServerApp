using DTSoft.AppService.MicroApp;
using DTSoft.Models.Parameter.MicroApp;

namespace DTSoftServerApp.Controllers.MicroApp
{
    /// <summary>
    /// 微应用接口
    /// </summary>
    /// <param name="microConfigApp"></param>
    [Authorize]
    [ApiController]
    [Tags("微应用")]
    [Route("api/[controller]")]
    public class MicroAppController(MicroConfigApp microConfigApp) : ControllerBase
    {

        /// <summary>
        /// 获取微应用配置列表
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="modelName">模型名称</param>
        /// <param name="microAppPath">微应用路径</param>
        /// <param name="pageNum">页码</param>
        /// <param name="pageSize">每页条数</param>
        /// <returns>微应用配置列表</returns>
        [HttpGet("GetMicroAppConfigs")]
        public async Task<IActionResult> GetMicroAppConfigs([FromQuery] string? keyword, [FromQuery] string? modelName,
            [FromQuery] string? microAppPath, [FromQuery] int? pageNum = 1, [FromQuery] int? pageSize = 10)
        {
            try
            {
                var parameter = new MicroConfigQueryParameter
                {
                    Keyword = keyword,
                    ModelName = modelName,
                    MicroAppPath = microAppPath,
                    PageNum = pageNum,
                    PageSize = pageSize
                };

                var result = await microConfigApp.GetMicroAppConfigs(parameter);

                return Ok(new { success = true, msg = "获取成功", data = result.Data, total = result.Total });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, msg = $"获取失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 添加微应用配置
        /// </summary>
        /// <param name="parameter">添加参数</param>
        /// <returns>添加的微应用配置</returns>
        [HttpPost("AddMicroAppConfig")]
        public async Task<IActionResult> AddMicroAppConfig([FromBody] MicroConfigAddParameter parameter)
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

                var result = await microConfigApp.AddMicroAppConfig(parameter);

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
        /// 更新微应用配置
        /// </summary>
        /// <param name="parameter">更新参数</param>
        /// <returns>更新结果</returns>
        [HttpPost("UpdateMicroAppConfig")]
        public async Task<IActionResult> UpdateMicroAppConfig([FromBody] MicroConfigUpdateParameter parameter)
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

                var result = await microConfigApp.UpdateMicroAppConfig(parameter);

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
        /// 删除微应用配置
        /// </summary>
        /// <param name="parameter">删除参数</param>
        /// <returns>删除结果</returns>
        [HttpPost("DeleteMicroAppConfig")]
        public async Task<IActionResult> DeleteMicroAppConfig([FromBody] MicroConfigDeleteParameter parameter)
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

                await microConfigApp.DeleteMicroAppConfig(parameter);

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
