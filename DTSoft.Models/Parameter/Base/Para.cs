namespace DTSoft.Models.Parameter.Base;

public class Para
{
    /// <summary>
    /// 搜索关键字
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 第几页
    /// </summary>
    public int PageNum { get; set; }

    /// <summary>
    /// 分页数量
    /// </summary>
    public int PageSize { get; set; }
    
    /// <summary>
    /// 部门ID（用于过滤用户）
    /// </summary>
    public long? DepartmentId { get; set; }
}
