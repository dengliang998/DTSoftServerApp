using DTSoft.Models.Parameter.Http;
using System.Net.Http.Headers;
using System.Text;

namespace DTSoft.Core.HttpRequest
{
    public class HttpBase
    {
        protected virtual async Task<string> Post(PostParameter parameter)
        {
            // 检查URL是否为有效的绝对URI
            if (string.IsNullOrEmpty(parameter.Url) || !Uri.IsWellFormedUriString(parameter.Url, UriKind.Absolute))
            {
                throw new InvalidOperationException($"Invalid URL: {parameter.Url}. The URL must be an absolute URI.");
            }

            HttpClient httpClient = new();

            if (!string.IsNullOrEmpty(parameter.Token))
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parameter.Token);

            //参数处理
            HttpContent? hContent = null;
            switch (parameter.PostType)
            {
                case "multipart/form-data":
                    var content = new MultipartFormDataContent();
                    string boundary = $"--{DateTime.Now.Ticks:x}";
                    content.Headers.Add("ContentType", $"{parameter.PostType}, boundary={boundary}");
                    content.Headers.ContentType!.CharSet = "UTF-8";
                    foreach (var item in parameter.Parameter)
                    {
                        if (item.Key.Split(':')[0].Equals("file", StringComparison.Ordinal))
                        {
                            content.Add((item.Value as StreamContent)!, item.Key.Split(':')[0], item.Key.Split(':')[1]);
                        }
                        else
                        {
                            content.Add((item.Value as StringContent)!, item.Key);
                        }
                    }
                    hContent = content;
                    break;
                case "x-www-from-urlencoded":
                    var data = new List<KeyValuePair<string, string?>>();
                    foreach (var item in parameter.Parameter)
                    {
                        data.Add(new KeyValuePair<string, string?>(item.Key, item.Value as string));
                    }
                    hContent = new FormUrlEncodedContent(data);
                    break;
                case "application/json":
                    hContent = new StringContent(Convert.ToString(parameter.Parameter["data"])!, Encoding.UTF8, "application/json");
                    break;
            }
            HttpResponseMessage response = await httpClient.PostAsync(parameter.Url, hContent);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            return "";
        }

        public virtual async Task<string> Get(ParameterBase parameter)
        {
            // 检查URL是否为有效的绝对URI
            if (string.IsNullOrEmpty(parameter.Url) || !Uri.IsWellFormedUriString(parameter.Url, UriKind.Absolute))
            {
                throw new InvalidOperationException($"Invalid URL: {parameter.Url}. The URL must be an absolute URI.");
            }

            HttpClient httpClient = new();

            if (!string.IsNullOrEmpty(parameter.Token))
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parameter.Token);

            StringBuilder builder = new();

            #region 参数处理
            foreach (var item in parameter.Parameter)
            {
                if (builder.Length == 0)
                    builder.Append($"?{item.Key}={item.Value}");
                else
                    builder.Append($"&{item.Key}={item.Value}");
            }
            parameter.Url += Convert.ToString(builder);
            #endregion

            HttpResponseMessage response = await httpClient.GetAsync(parameter.Url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            return "";
        }
    }
}
