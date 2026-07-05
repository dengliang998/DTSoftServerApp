using DTSoft.Models.Parameter.Base;

namespace DTSoft.Models.Parameter.Log;

public class LogAction : Para
{
    /// <summary>
    /// 日志开始时间
    /// </summary>
    public DateTime? LogDateStart { get; set; }

    /// <summary>
    /// 日志结束时间
    /// </summary>
    public DateTime? LogDateEnd { get; set; }

    /// <summary>
    /// 操作用户账号或显示名，模糊搜索
    /// </summary>
    public string? UserAcc { get; set; }

    /// <summary>
    /// IP 地址，模糊搜索
    /// </summary>
    public string? ClientIP { get; set; }

    /// <summary>
    /// 接口名称，模糊搜索
    /// </summary>
    public string? ActionName { get; set; }

    /// <summary>
    /// 请求参数，模糊搜索
    /// </summary>
    public string? Param { get; set; }

    /// <summary>
    /// 返回结果，模糊搜索
    /// </summary>
    public string? Result { get; set; }
}
