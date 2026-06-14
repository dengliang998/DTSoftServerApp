using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Base;

namespace DTSoft.Models.Parameter.Menu;

public class AddMenu
{
    public long Pid { get; set; }
    public string? MenuName { get; set; }
    public string? MenuPath { get; set; }
    public int Order { get; set; }
    public string? Icon { get; set; }
    public bool IsHidden { get; set; }
    public int MType { get; set; }
    public MenuType Type { get; set; } = MenuType.Internal;
    public SystemUrlBase SystemUrlBase { get; set; } = new();
}
