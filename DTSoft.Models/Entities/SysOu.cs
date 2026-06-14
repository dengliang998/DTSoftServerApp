using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

/// <summary>
/// 部门实体（Organization Unit）
/// </summary>
[Table("sys_ou")]
public class SysOu
{
    /// <summary>
    /// 部门ID（雪花ID）
    /// </summary>
    [Key]
    public long ItemId { get; set; }
    
    /// <summary>
    /// 部门名称
    /// </summary>
    public string? DepartmentName { get; set; }
    
    /// <summary>
    /// 部门编码
    /// </summary>
    public string? DepartmentCode { get; set; }
    
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
    
    /// <summary>
    /// 部门成员列表
    /// </summary>
    public List<SysUserMember>? SysUserMember { get; init; }
    
    /// <summary>
    /// 子部门列表
    /// </summary>
    public List<SysOu>? Children { get; set; }
}
