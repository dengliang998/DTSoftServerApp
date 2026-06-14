using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTSoft.Models.Entities;

[Table("sys_attachments")]
public class SysAttachments
{
    [Key]
    public long ItemId { get; init; }
    public string? FileName { get; init; }
    public string? FileId { get; init; }
    public long Size { get; init; }
    public string? FilePath { get; init; }
    public string? CreateUser { get; init; }
    public DateTime CreateDate { get; init; }
    public string? Ext { get; init; }
}
