using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_result_log")]
public class SysResultLog
{
    [Key]
    public long ItemId { get; init; }
    /// <summary>
    /// 日志记录时间
    /// </summary>
    public DateTime LogDate { get; init; }
    /// <summary>
    /// 操作账号
    /// </summary>
    public string? UserAcc { get; init; }
    /// <summary>
    /// 执行的接口名称
    /// </summary>
    public string? ActionName { get; init; }
    /// <summary>
    /// 客户端IP地址
    /// </summary>
    public string? ClientIP { get; init; }
    /// <summary>
    /// 返回值
    /// </summary>
    public string? Result { get; init; }
    public bool Success { get; init; }
}
