using DTSoft.AppService.Localization;
using DTSoft.AppService.MicroApp;
using DTSoft.Core.Common;
using DTSoft.Models.Parameter.MicroApp;

namespace DTSoftServerApp.Controllers.MicroApp
{
    /// <summary>
    /// 微应用数据接口控制器
    /// </summary>
    [Authorize]
    [ApiController]
    [Tags("Micro App Data")]
    public class MicroApiController(MicroRuntimeApp microRuntimeApp, IAppLocalizer localizer) : DtSoftControllerBase(localizer)
    {
        /// <summary>
        /// 查询微应用数据列表
        /// </summary>
        [HttpGet("/api/{modelName}")]
        public async Task<IActionResult> GetList(
            string modelName,
            [FromQuery] int pageNum,
            [FromQuery] int pageSize,
            [FromQuery] string keyword = "",
            [FromQuery] string filters = "",
            [FromQuery] string sortField = "",
            [FromQuery] string sortOrder = "")
        {
            var result = await microRuntimeApp.GetList(
                modelName,
                pageNum,
                pageSize,
                keyword,
                filters,
                sortField,
                sortOrder,
                GetLoginUserAccount());

            return ToJsonResult(result);
        }

        /// <summary>
        /// 查询微应用数据详情
        /// </summary>
        [HttpGet("/api/{modelName}/{id:long}")]
        public async Task<IActionResult> GetDetail(string modelName, long id)
        {
            var result = await microRuntimeApp.GetDetail(modelName, id, GetLoginUserAccount());
            return ToJsonResult(result);
        }

        /// <summary>
        /// 新增微应用数据
        /// </summary>
        [HttpPost("/api/{modelName}")]
        public async Task<IActionResult> Create(string modelName, [FromBody] object? data)
        {
            if (data == null)
            {
                return Failure(Localizer["common.argumentMissing"]);
            }

            var result = await microRuntimeApp.Create(modelName, data, GetLoginUserAccount());
            return ToJsonResult(result);
        }

        /// <summary>
        /// 更新微应用数据
        /// </summary>
        [HttpPut("/api/{modelName}/{id:long}")]
        public async Task<IActionResult> Update(string modelName, long id, [FromBody] object? data)
        {
            if (data == null)
            {
                return Failure(Localizer["common.argumentMissing"]);
            }

            var result = await microRuntimeApp.Update(modelName, id, data, GetLoginUserAccount());
            return ToJsonResult(result);
        }

        /// <summary>
        /// 删除微应用数据
        /// </summary>
        [HttpDelete("/api/{modelName}/{id:long}")]
        public async Task<IActionResult> Delete(string modelName, long id)
        {
            var result = await microRuntimeApp.Delete(modelName, id, GetLoginUserAccount());
            return ToJsonResult(result);
        }

        /// <summary>
        /// 批量删除微应用数据
        /// </summary>
        [HttpPost("/api/{modelName}/batch-delete")]
        public async Task<IActionResult> BatchDelete(string modelName, [FromBody] MicroBatchDeleteParameter? parameter)
        {
            if (!ModelState.IsValid || parameter == null)
            {
                return InvalidArguments();
            }

            var result = await microRuntimeApp.BatchDelete(modelName, parameter, GetLoginUserAccount());
            return ToJsonResult(result);
        }

        /// <summary>
        /// 导出微应用数据 Excel
        /// </summary>
        [HttpGet("/api/{modelName}/export")]
        public async Task<IActionResult> ExportExcel(
            string modelName,
            [FromQuery] string keyword = "",
            [FromQuery] string filters = "",
            [FromQuery] string sortField = "",
            [FromQuery] string sortOrder = "")
        {
            var result = await microRuntimeApp.ExportExcel(
                modelName,
                keyword,
                filters,
                sortField,
                sortOrder,
                GetLoginUserAccount());

            if (!result.Success)
            {
                return Failure(result.Msg);
            }

            return File(
                result.FileContent!,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                result.FileName);
        }

        /// <summary>
        /// 导入微应用 Excel 数据
        /// </summary>
        [HttpPost("/api/{modelName}/import")]
        [RequestSizeLimit(100 * 1024 * 1024)]
        public async Task<IActionResult> ImportExcel(string modelName, IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return Failure(Localizer["micro.uploadExcel"]);
            }

            await using var fileStream = file.OpenReadStream();
            var result = await microRuntimeApp.ImportExcel(
                modelName,
                file.FileName,
                fileStream,
                GetLoginUserAccount());

            return ToJsonResult(result);
        }

        private string GetLoginUserAccount() => DtSoftHelper.GetLoginUserAccount(User);

        private IActionResult ToJsonResult(MicroRuntimeResult result)
        {
            if (result.Data == null)
            {
                return result.Success ? Success(result.Msg) : Failure(result.Msg);
            }

            return ApiResponse(result.Success, result.Msg, result.Data);
        }
    }
}
