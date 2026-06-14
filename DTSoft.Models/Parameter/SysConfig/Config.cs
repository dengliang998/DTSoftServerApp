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
}
