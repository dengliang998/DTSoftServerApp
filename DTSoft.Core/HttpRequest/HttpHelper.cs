using DTSoft.Models.Parameter.Http;

namespace DTSoft.Core.HttpRequest;

public class HttpHelper : HttpBase
{
    /// <summary>
    /// Post请求
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    protected override Task<string> Post(PostParameter parameter)
    {
        if (!parameter.Token.IndexOf("Bearer", StringComparison.Ordinal).Equals(-1))
        {
            parameter.Token = parameter.Token[6..];
        }
        return base.Post(parameter);
    }
}
