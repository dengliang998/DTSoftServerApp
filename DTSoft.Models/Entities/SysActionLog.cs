using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_action_log")]
public class SysActionLog
{
    [Key]
    public long ItemId { get; init; }
    /// <summary>
    /// 日志记录时间
    /// </summary>
    public DateTime LogDate { get; set; }
    /// <summary>
    /// 操作账号
    /// </summary>
    public string? UserAcc { get; set; }
    /// <summary>
    /// 执行的接口名称
    /// </summary>
    public string? ActionName { get; set; }
    /// <summary>
    /// 客户端IP地址
    /// </summary>
    public string? ClientIP { get; set; }
    /// <summary>
    /// 请求参数
    /// </summary>
    public string? Param { get; set; }
    /// <summary>
    ///
    /// </summary>
    public string? RequestType { get; set; }
    public string? Result { get; set; }
}
