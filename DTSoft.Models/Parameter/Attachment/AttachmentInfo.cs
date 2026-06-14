namespace DTSoft.Models.Parameter.Attachment;

public class AttachmentInfo
{
    /// <summary>
    /// 文件编号
    /// </summary>
    public string? FileId { get; set; }

    /// <summary>
    /// 文件名称
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// 文件名称，带扩展名
    /// </summary>
    public string? FileFullName { get; set; }

    /// <summary>
    /// 扩展名
    /// </summary>
    public string? Ext { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; } = true;
}
