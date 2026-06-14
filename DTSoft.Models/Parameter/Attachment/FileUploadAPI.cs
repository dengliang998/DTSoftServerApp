using Microsoft.AspNetCore.Http;

namespace DTSoft.Models.Parameter.Attachment;

public class FileUploadApi
{
    public IFormFile? Files { get; init; }
}

