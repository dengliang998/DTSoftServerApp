using System.Text.Json.Nodes;

namespace DTSoft.Plugin.Abstractions;

/// <summary>
/// DTSoft 系统能力的插件可见入口。
/// </summary>
public interface IDtSoftHelper
{
    bool IsAdmin(string userAcc);

    string? GetRoleName(long roleId);

    bool IsContainRole(long roleId, string userAcc);

    string GetLoginUserAccountFromJwt(string token);

    Task<JsonObject> CheckAccStatus(string userAcc);

    Task<JsonArray> GetDictionaryItemsAsync(string dictCode);
}
