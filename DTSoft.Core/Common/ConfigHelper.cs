using Microsoft.Extensions.Configuration;

namespace DTSoft.Core.Common;

/// <summary>
/// 获取appsettings.json参数
/// </summary>
public class ConfigHelper(IConfiguration configuration)
{
    //获取值
    public string? GetSectionValue(string key) => configuration[key];

    /// <summary>
    /// 文件存储根路径
    /// </summary>
    public string RootPath => configuration[AppConfigurationKeys.Storage.RootPath]
        ?? configuration[AppConfigurationKeys.Storage.LegacyRootPath]
        ?? "Attachment";

    /// <summary>
    /// 附件保存路径
    /// </summary>
    public string AttachmentPath => Path.Combine(
        RootPath,
        configuration[AppConfigurationKeys.Storage.Attachments.Directory]
            ?? configuration[AppConfigurationKeys.Storage.Attachments.LegacyDirectory]
            ?? "File");

    /// <summary>
    /// 用户数据路径
    /// </summary>
    public string UserDataPath => Path.Combine(
        RootPath,
        configuration[AppConfigurationKeys.Storage.Users.Directory]
            ?? configuration[AppConfigurationKeys.Storage.Users.LegacyDirectory]
            ?? "User",
        configuration[AppConfigurationKeys.Storage.Users.AvatarDirectory] ?? "Avatar");

    /// <summary>
    /// 用户数据路径-上传
    /// </summary>
    public string UserDataPathUpload => Path.Combine(AppContext.BaseDirectory, UserDataPath);
}
