namespace DTSoft.Models.Parameter.User;

/// <summary>
/// 修改密码参数
/// </summary>
public class ModifyPassword: UserBase
{
    /// <summary>
    /// 老密码
    /// </summary>
    public string? OldPassWord { get; set; }

    /// <summary>
    /// 新密码
    /// </summary>
    public string? NewPassWord { get; set; }
}
