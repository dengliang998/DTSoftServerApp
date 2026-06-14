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
    /// 附件保存路径
    /// </summary>
    public string AttachmentPath => Path.Combine(configuration["RootPath"]!, configuration["AttachmentPath"]!);

    /// <summary>
    /// 用户数据路径
    /// </summary>
    public string UserDataPath => Path.Combine(configuration["RootPath"]!, configuration["UserData"]!, "Avatar");

    /// <summary>
    /// 用户数据路径-上传
    /// </summary>
    public string UserDataPathUpload => Path.Combine(AppContext.BaseDirectory, UserDataPath);
}
