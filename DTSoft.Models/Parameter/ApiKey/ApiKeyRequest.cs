using System.ComponentModel.DataAnnotations;

namespace DTSoft.Models.Parameter.ApiKey;

/// <summary>
/// API密钥登录请求参数
/// </summary>
public class ApiKeyLoginRequest
{
    /// <summary>
    /// 密钥名称
    /// </summary>
    [Required(ErrorMessage = "KeyName不能为空")]
    public string KeyName { get; set; } = string.Empty;
    
    /// <summary>
    /// 密钥
    /// </summary>
    [Required(ErrorMessage = "SecretKey不能为空")]
    public string SecretKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 用户账号
    /// </summary>
    [Required(ErrorMessage = "UserAccount不能为空")]
    public string UserAccount { get; set; } = string.Empty;
}
