namespace DTSoft.Models.Parameter.Http;

public class PostParameter : ParameterBase
{
    /// <summary>
    /// 请求类型
    /// </summary>
    public string PostType { get; init; } = "multipart/form-data";
}
