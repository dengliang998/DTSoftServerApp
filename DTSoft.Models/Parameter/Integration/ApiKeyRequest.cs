using System.ComponentModel.DataAnnotations;

namespace DTSoft.Models.Parameter.Integration;

/// <summary>
/// API密钥登录请求参数
/// </summary>
public class ApiKeyLoginRequest
{
    /// <summary>
    /// 密钥名称
    /// </summary>
    [Required(ErrorMessage = "integration.keyNameRequired")]
    public string KeyName { get; set; } = string.Empty;
    
    /// <summary>
    /// 密钥
    /// </summary>
    [Required(ErrorMessage = "integration.secretKeyRequired")]
    public string SecretKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 用户账号
    /// </summary>
    [Required(ErrorMessage = "integration.userAccountRequired")]
    public string UserAccount { get; set; } = string.Empty;
}
