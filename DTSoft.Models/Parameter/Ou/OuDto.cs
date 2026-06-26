namespace DTSoft.Models.Parameter.Ou;

/// <summary>
/// 部门基础参数
/// </summary>
public class OuDto
{
    /// <summary>
    /// 部门ID
    /// </summary>
    public long? ItemId { get; set; }
    
    /// <summary>
    /// 部门名称
    /// </summary>
    public string? OuName { get; set; }
    
    /// <summary>
    /// 部门编码
    /// </summary>
    public string? OuCode { get; set; }
    
    /// <summary>
    /// 上级部门ID
    /// </summary>
    public long? ParentId { get; set; }
    
    /// <summary>
    /// 排序号
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// 是否禁用
    /// </summary>
    public bool Disable { get; set; }
    
    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
