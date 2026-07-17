using DTSoft.AppService.Dictionary;
using DTSoft.Models.Parameter.Dictionary;

namespace DTSoftServerApp.Controllers.Dictionary;

[Authorize]
[ApiController]
[Tags("数据字典")]
[Route("api/[controller]/[action]")]
public class DictionaryController(DictionaryApp dictionaryApp) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> GetTypes([FromBody] DictionaryTypeQuery? query)
    {
        return Ok(await dictionaryApp.GetTypesAsync(query ?? new DictionaryTypeQuery()));
    }

    [HttpPost]
    public async Task<IActionResult> SaveType([FromBody] DictionaryTypeDto? dto)
    {
        if (dto == null)
            return Ok(Error("参数不能为空"));

        return Ok(await dictionaryApp.SaveTypeAsync(dto));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteType([FromBody] DeleteRequest? request)
    {
        if (request == null)
            return Ok(Error("参数不能为空"));

        return Ok(await dictionaryApp.DeleteTypeAsync(request.ItemId));
    }

    [HttpPost]
    public async Task<IActionResult> GetItems([FromBody] DictionaryItemQuery? query)
    {
        return Ok(await dictionaryApp.GetItemsAsync(query ?? new DictionaryItemQuery()));
    }

    [HttpGet]
    public async Task<IActionResult> GetItemsByCode([FromQuery] string dictCode)
    {
        return Ok(await dictionaryApp.GetEnabledItemsByCodeAsync(dictCode));
    }

    [HttpPost]
    public async Task<IActionResult> SaveItem([FromBody] DictionaryItemDto? dto)
    {
        if (dto == null)
            return Ok(Error("参数不能为空"));

        return Ok(await dictionaryApp.SaveItemAsync(dto));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteItem([FromBody] DeleteRequest? request)
    {
        if (request == null)
            return Ok(Error("参数不能为空"));

        return Ok(await dictionaryApp.DeleteItemAsync(request.ItemId));
    }

    private static JsonObject Error(string message) => new()
    {
        ["success"] = false,
        ["StateCode"] = 0,
        ["Msg"] = message
    };
}

public class DeleteRequest
{
    public long ItemId { get; set; }
}
