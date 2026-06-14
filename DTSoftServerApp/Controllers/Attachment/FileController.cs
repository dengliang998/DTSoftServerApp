using DTSoft.AppService.Attachment;
using DTSoft.Core.Common;
using DTSoft.Models.Parameter.Attachment;

namespace DTSoftServerApp.Controllers.Attachment;

/// <summary>
/// 文件管理
/// </summary>
[Authorize]
[ApiController]
[Tags("文件管理")]
[Route("api/[controller]/[action]")]
public class FileController : Controller
{
    private readonly AttachmentApp _fileAction;
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="fileAction"></param>
    public FileController(AttachmentApp fileAction) => _fileAction = fileAction;

    /// <summary>
    /// 上传文件
    /// </summary>
    /// <param name="objFile">文件对象</param>
    /// <returns>附件信息-FileID可用于下载</returns>
    [HttpPost]
    public async Task<IActionResult> Save([FromForm] FileUploadApi objFile)
    {
        return Ok(await _fileAction.Save(objFile, DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 上传文件-分片上传
    /// </summary>
    /// <param name="objFile">文件对象</param>
    /// <returns>附件信息-FileID可用于下载</returns>
    [HttpPost]
    public async Task<IActionResult> Saves([FromForm] FileUploadInfo objFile)
    {
        return Ok(await _fileAction.Saves(objFile, DtSoftHelper.GetLoginUserAccount(User)));
    }

    /// <summary>
    /// 文件下载
    /// </summary>
    /// <param name="fileId">文件编号</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet]
    public Task<IActionResult> Download(string fileId)
    {
        JsonObject rv = _fileAction.Download(fileId);
        string filePath = Convert.ToString(rv["FilePath"])!;
        if (filePath is "")
        {
            return Task.FromResult<IActionResult>(Ok(rv));
        }

        // 使用普通的FileStream替代FileStreamSpeedLimit
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var result = new FileStreamResult(fileStream, "application/octet-stream")
        {
            FileDownloadName = Convert.ToString(rv["FileName"])
        };

        return Task.FromResult<IActionResult>(result);
    }

    /// <summary>
    /// 获取文件列表
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> GetFileList([FromForm] FileParameter obj)
    {
        return Ok(await _fileAction.GetFileListAsync(obj));
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    /// <param name="fileId"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> RemoveFile([FromForm] string fileId) => Ok(await _fileAction.RemoveFileAsync(fileId, DtSoftHelper.GetLoginUserAccount(User)));
}
