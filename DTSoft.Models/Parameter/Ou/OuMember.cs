namespace DTSoft.Models.Parameter.Ou;

/// <summary>
/// 部门成员参数
/// </summary>
public class OuMember
{
    /// <summary>
    /// 部门ID
    /// </summary>
    public long OuId { get; set; }
    
    /// <summary>
    /// 用户账号列表
    /// </summary>
    public List<string>? UserAccounts { get; set; }
}
