using Microsoft.AspNetCore.Http;

namespace DTSoft.Models.Parameter.SysConfig;

public class Config
{
    /// <summary>
    /// 系统名称
    /// </summary>
    public string? SystemName { get; set; } = "DT Program";
    /// <summary>
    /// 背景图片
    /// </summary>
    public IFormFile? LoginImg { get; set; }

    /// <summary>
    /// 浏览器标签页小logo
    /// </summary>
    public IFormFile? BrowserLogo { get; set; }

    /// <summary>
    /// 系统主题配置JSON
    /// </summary>
    public string? ThemeConfig { get; set; }
}
