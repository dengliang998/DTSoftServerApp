namespace DTSoft.Models.Parameter.Menu;

public class UpdateMenuParameter
{
    public long ItemId { get; set; }
    public long Pid { get; set; }
    public string? MenuName { get; set; }
    public string? MenuPath { get; set; }
    public int Order { get; set; }
    public string? Icon { get; set; }
    public bool IsHidden { get; set; }
    public int MType { get; set; }
}
