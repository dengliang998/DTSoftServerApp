namespace DTSoft.Models.Parameter.User;

public class UserDto : UserBase
{
    /// <summary>
    /// 密码
    /// </summary>
    public string? PassWord { get; set; }
    /// <summary>
    /// 用户名
    /// </summary>
    public string? DisplayName { get; set; }
    /// <summary>
    /// 性别
    /// </summary>
    public string? Sex { get; set; }
    /// <summary>
    /// 是否禁用
    /// </summary>
    public bool Disable { get; set; }
    /// <summary>
    /// 头像
    /// </summary>
    public string? Avatar { get; set; }
    /// <summary>
    /// 部门ID
    /// </summary>
    public long? DepartmentId { get; set; }
    /// <summary>
    /// 职位
    /// </summary>
    public string? Position { get; set; }
    /// <summary>
    /// 邮箱
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 直属主管账号（上级账号）
    /// </summary>
    public string? SupervisorAcc { get; set; }
}
