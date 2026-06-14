namespace DTSoft.Models.Parameter.Http
{
    public class ParameterBase
    {
        /// <summary>
        /// 请求地址
        /// </summary>
        public required string Url { get; set; }

        /// <summary>
        /// 身份令牌
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// 请求参数
        /// </summary>
        public Dictionary<string, object> Parameter { get; init; } = new();
    }
}

