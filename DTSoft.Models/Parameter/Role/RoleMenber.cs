
namespace DTSoft.Models.Parameter.Role;

/// <summary>
/// 角色成员
/// </summary>
public class RoleMenber
{
    /// <summary>
    /// 角色 Id
    /// </summary>
    public long ItemId { get; set; }
    public required List<RoleMember> RoleMember { get; set; }
}

public class RoleMember()
{
    public string? UserAcc { get; init; }
}
