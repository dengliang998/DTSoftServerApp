namespace DTSoft.Models.Parameter.User;

/// <summary>
/// 重置密码参数
/// </summary>
public class ResetPassword : UserBase
{
    /// <summary>
    /// 密码
    /// </summary>
    public required string PassWord { get; set; }
}
