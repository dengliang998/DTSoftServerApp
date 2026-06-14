namespace DTSoft.Models.Parameter.Attachment;

public class FileUploadInfo : FormFile
{
    public required string FileName { get; set; }
    public int Total { get; set; }
    public int Index { get; set; }
}

