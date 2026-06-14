namespace DTSoft.Models.Parameter.Department;

/// <summary>
/// 部门成员参数
/// </summary>
public class DepartmentMember
{
    /// <summary>
    /// 部门ID
    /// </summary>
    public long DepartmentId { get; set; }
    
    /// <summary>
    /// 用户账号列表
    /// </summary>
    public List<string>? UserAccounts { get; set; }
}
