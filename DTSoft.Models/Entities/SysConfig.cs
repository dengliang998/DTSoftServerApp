using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_config")]
public class SysConfig
{
    /// <summary>
    /// 主键
    /// </summary>
    [Key]
    public long ItemId { get; init; }

    /// <summary>
    /// 登录页背景图
    /// </summary>
    public string? LoginImg { get; set; }

    /// <summary>
    /// 登录是否启用验证码
    /// </summary>
    public bool? LoginCaptchaEnabled { get; set; } = true;

    /// <summary>
    /// 系统名称
    /// </summary>
    public string? SystemName { get; set; }

    /// <summary>
    /// 浏览器标签页小logo
    /// </summary>
    public string? BrowserLogo { get; set; }

    /// <summary>
    /// 系统主题配置JSON
    /// </summary>
    public string? ThemeConfig { get; set; }
}
