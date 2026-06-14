using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_user")]
public class SysUser
{
    [Key]
    public string? Account { get; init; }
    //密码
    public string? PassWord { get; set; }
    //用户名
    public string? DisplayName { get; set; }
    //性别
    public string? Sex { get; set; }
    //是否禁用
    public bool Disable { get; set; }
    //头像
    public string? Avatar { get; set; }
    //职位
    public string? Position { get; set; }
    //邮箱
    public string? Email { get; set; }
    public List<SysRoleMember>? SysRoleMember { get; init; }
    public List<SysUserMember>? SysUserMember { get; init; }
}
