namespace DTSoft.Models.Parameter.MicroApp;

/// <summary>
/// 微应用数据表系统字段名称。
/// </summary>
public static class MicroTableSystemColumns
{
    /// <summary>
    /// 主键字段。
    /// </summary>
    public const string Id = "ItemId";

    /// <summary>
    /// 创建时间字段。
    /// </summary>
    public const string CreatedTime = "created_time";

    /// <summary>
    /// 更新时间字段。
    /// </summary>
    public const string UpdatedTime = "updated_time";

    /// <summary>
    /// 创建人字段。
    /// </summary>
    public const string CreatedBy = "created_by";

    /// <summary>
    /// 更新人字段。
    /// </summary>
    public const string UpdatedBy = "updated_by";

    /// <summary>
    /// 子表关联主表数据 ID 字段。
    /// </summary>
    public const string ParentId = "ParentId";

    /// <summary>
    /// 子表行序号字段。
    /// </summary>
    public const string RowNo = "row_no";
}
