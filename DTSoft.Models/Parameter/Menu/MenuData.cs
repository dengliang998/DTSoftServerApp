using DTSoft.Models.Entities;

namespace DTSoft.Models.Parameter.Menu;

public class MenuData: SysMenu
{
    public required string UserAcc { get; init; }
    public long RoleId { get; init; }
}
