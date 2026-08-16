using DTSoft.AppService.Localization;
using DTSoftServerApp.Extensions;

namespace DTSoftServerApp.Controllers;

public abstract class DtSoftControllerBase(IAppLocalizer localizer) : ControllerBase
{
    protected IAppLocalizer Localizer { get; } = localizer;

    protected IActionResult Success(string message)
    {
        return ApiResponse(true, message);
    }

    protected IActionResult Success(string message, object? data)
    {
        return Ok(new { success = true, msg = message, data });
    }

    protected IActionResult Success(string message, object? data, int total)
    {
        return Ok(new { success = true, msg = message, data, total });
    }

    protected IActionResult Failure(string message, Exception exception)
    {
        return Ok(new { success = false, msg = Localizer.Format("common.failedWithReason", message, exception.Message) });
    }

    protected IActionResult Failure(string message)
    {
        return ApiResponse(false, message);
    }

    protected IActionResult InvalidArguments()
    {
        var errors = ModelState.GetErrorMessages();
        return Failure(string.Join(";", errors.DefaultIfEmpty(Localizer["common.argumentMissing"])));
    }

    protected IActionResult ApiResponse(bool success, string message, object? data = null)
    {
        if (data == null)
        {
            return Ok(new { success, msg = message });
        }

        return Ok(new { success, msg = message, data });
    }
}
