using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DTSoft.AppService.Localization;
using DTSoft.Core.Exceptions;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Esb;

namespace DTSoft.AppService.Esb;

public class EsbWebApiExecutor(IAppLocalizer localizer)
{
    private const string SourceTypeRestful = "restful";
    private static readonly ConcurrentDictionary<string, EsbCachedToken> TokenCache = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> TokenLocks = new();
    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private string L(string key, params object[] args) => args.Length == 0 ? localizer[key] : localizer.Format(key, args);
    private DtSoftException BadRequest(string key, params object[] args) => DtSoftException.BadRequest(L(key, args), key);
    private DtSoftException BadGateway(string key, params object[] args) => DtSoftException.BadGateway(L(key, args), key);

    public void ValidateRequestConfig(string? httpConfig)
    {
        var requestConfig = DeserializeWebApiRequestConfig(httpConfig);
        ValidateWebApiRequestConfig(requestConfig);
    }

    public async Task<object> Execute(
        SysEsbDataSource entity,
        SysEsbServiceConnection serviceConnection,
        List<EsbParameterConfig> declaredParameters,
        Dictionary<string, JsonNode?> inputParameters,
        Dictionary<string, string> variableContext,
        int? pageNum,
        int? pageSize)
    {
        if (!string.Equals(serviceConnection.ServiceType, SourceTypeRestful, StringComparison.OrdinalIgnoreCase))
        {
            throw BadRequest("esb.webApiRequiresWebApiConnection");
        }

        var connectionConfig = DeserializeWebApiConnectionConfig(serviceConnection.WebApiConfig);
        var requestConfig = DeserializeWebApiRequestConfig(entity.HttpConfig);
        ValidateWebApiConfig(connectionConfig, requestConfig);

        var templateContext = EsbTemplateRenderer.BuildTemplateContext(declaredParameters, inputParameters, variableContext);
        var requestUri = BuildWebApiRequestUri(connectionConfig, requestConfig, templateContext);

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(NormalizeTimeoutSeconds(entity.TimeoutSeconds));

        using var request = new HttpRequestMessage(CreateHttpMethod(requestConfig.Method), requestUri);
        EsbTemplateRenderer.ApplyHeaders(request, connectionConfig.Headers, templateContext);
        EsbTemplateRenderer.ApplyHeaders(request, requestConfig.Headers, templateContext);
        await ApplyAuthentication(request, connectionConfig, templateContext, requestUri, client);

        var body = EsbTemplateRenderer.RenderTemplate(requestConfig.Body, templateContext);
        if (!string.IsNullOrWhiteSpace(body) && request.Method != HttpMethod.Get)
        {
            request.Content = new StringContent(body, Encoding.UTF8, string.IsNullOrWhiteSpace(requestConfig.ContentType) ? "application/json" : requestConfig.ContentType.Trim());
        }

        using var response = await client.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw BadGateway("esb.webApiRequestFailed", (int)response.StatusCode, response.ReasonPhrase ?? string.Empty);
        }

        var root = ParseJsonResponse(responseText);
        var dataNode = EsbJsonHelper.SelectJsonPath(root, requestConfig.ResultPath) ?? root;
        var rows = EsbJsonHelper.ConvertJsonNodeToRows(dataNode, NormalizeMaxRows(entity.MaxRows));

        if (pageNum is > 0 && pageSize is > 0)
        {
            var normalizedPageNum = pageNum.Value;
            var normalizedPageSize = Math.Clamp(pageSize.Value, 1, 200);
            var total = EsbJsonHelper.ReadJsonPathAsInt(root, requestConfig.TotalPath) ?? rows.Count;
            return new EsbPagedExecuteResponse
            {
                List = rows.Skip((normalizedPageNum - 1) * normalizedPageSize).Take(normalizedPageSize).ToList(),
                Total = total,
                PageNum = normalizedPageNum,
                PageSize = normalizedPageSize
            };
        }

        return rows;
    }

    private EsbWebApiConnectionConfig DeserializeWebApiConnectionConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new EsbWebApiConnectionConfig();
        try
        {
            return JsonSerializer.Deserialize<EsbWebApiConnectionConfig>(json, CaseInsensitiveJsonOptions) ?? new EsbWebApiConnectionConfig();
        }
        catch (JsonException)
        {
            throw BadRequest("esb.webApiConfigInvalid");
        }
    }

    private EsbWebApiRequestConfig DeserializeWebApiRequestConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new EsbWebApiRequestConfig();
        try
        {
            return JsonSerializer.Deserialize<EsbWebApiRequestConfig>(json, CaseInsensitiveJsonOptions) ?? new EsbWebApiRequestConfig();
        }
        catch (JsonException)
        {
            throw BadRequest("esb.webApiConfigInvalid");
        }
    }

    private void ValidateWebApiConfig(EsbWebApiConnectionConfig connectionConfig, EsbWebApiRequestConfig requestConfig)
    {
        if (string.IsNullOrWhiteSpace(connectionConfig.BaseUrl)) throw BadRequest("esb.webApiBaseUrlRequired");
        if (!Uri.TryCreate(connectionConfig.BaseUrl, UriKind.Absolute, out _)) throw BadRequest("esb.webApiBaseUrlRequired");
        ValidateWebApiRequestConfig(requestConfig);
    }

    private void ValidateWebApiRequestConfig(EsbWebApiRequestConfig requestConfig)
    {
        if (string.IsNullOrWhiteSpace(requestConfig.Path)) throw BadRequest("esb.webApiPathRequired");
        _ = CreateHttpMethod(requestConfig.Method);
    }

    private HttpMethod CreateHttpMethod(string? method)
    {
        var normalized = string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();
        return normalized switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            _ => throw BadRequest("esb.webApiMethodUnsupported")
        };
    }

    private static Uri BuildWebApiRequestUri(
        EsbWebApiConnectionConfig connectionConfig,
        EsbWebApiRequestConfig requestConfig,
        Dictionary<string, string> templateContext)
    {
        var baseUri = new Uri(connectionConfig.BaseUrl!.Trim().TrimEnd('/') + "/");
        var path = EsbTemplateRenderer.RenderTemplate(requestConfig.Path, templateContext).TrimStart('/');
        var builder = new UriBuilder(new Uri(baseUri, path));
        var queryItems = new List<string>();
        if (!string.IsNullOrWhiteSpace(builder.Query))
        {
            queryItems.Add(builder.Query.TrimStart('?'));
        }

        foreach (var pair in requestConfig.Query ?? new Dictionary<string, string>())
        {
            var value = EsbTemplateRenderer.RenderTemplate(pair.Value, templateContext);
            if (string.IsNullOrEmpty(value)) continue;
            queryItems.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(value)}");
        }

        builder.Query = string.Join("&", queryItems.Where(item => !string.IsNullOrWhiteSpace(item)));
        return builder.Uri;
    }

    private async Task ApplyAuthentication(
        HttpRequestMessage request,
        EsbWebApiConnectionConfig connectionConfig,
        Dictionary<string, string> templateContext,
        Uri requestUri,
        HttpClient client)
    {
        var authType = (connectionConfig.AuthType ?? "none").Trim().ToLowerInvariant();
        if (authType == "bearer")
        {
            var token = await ResolveBearerToken(connectionConfig, templateContext, client);
            if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return;
        }

        if (authType != "apikey") return;

        var apiKeyName = EsbTemplateRenderer.RenderTemplate(connectionConfig.ApiKeyName, templateContext);
        var apiKeyValue = EsbTemplateRenderer.RenderTemplate(connectionConfig.ApiKeyValue, templateContext);
        if (string.IsNullOrWhiteSpace(apiKeyName) || string.IsNullOrWhiteSpace(apiKeyValue)) return;

        if (string.Equals(connectionConfig.ApiKeyIn, "query", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(requestUri);
            var prefix = string.IsNullOrWhiteSpace(builder.Query) ? string.Empty : builder.Query.TrimStart('?') + "&";
            builder.Query = prefix + $"{Uri.EscapeDataString(apiKeyName)}={Uri.EscapeDataString(apiKeyValue)}";
            request.RequestUri = builder.Uri;
            return;
        }

        request.Headers.TryAddWithoutValidation(apiKeyName, apiKeyValue);
    }

    private async Task<string> ResolveBearerToken(
        EsbWebApiConnectionConfig connectionConfig,
        Dictionary<string, string> templateContext,
        HttpClient client)
    {
        var tokenUrlText = EsbTemplateRenderer.RenderTemplate(connectionConfig.TokenUrl, templateContext);
        if (string.IsNullOrWhiteSpace(tokenUrlText)) throw BadRequest("esb.webApiTokenUrlRequired");

        var tokenUri = Uri.TryCreate(tokenUrlText, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(new Uri(connectionConfig.BaseUrl!.Trim().TrimEnd('/') + "/"), tokenUrlText.TrimStart('/'));
        var body = EsbTemplateRenderer.RenderTemplate(connectionConfig.TokenBody, templateContext);
        var cacheKey = BuildTokenCacheKey(connectionConfig, tokenUri, body);
        var refreshSkewSeconds = Math.Clamp(connectionConfig.TokenRefreshSkewSeconds ?? 60, 0, 3600);
        if (TokenCache.TryGetValue(cacheKey, out var cachedToken) &&
            cachedToken.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(refreshSkewSeconds))
        {
            return cachedToken.Token;
        }

        var tokenLock = TokenLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await tokenLock.WaitAsync();
        try
        {
            if (TokenCache.TryGetValue(cacheKey, out cachedToken) &&
                cachedToken.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(refreshSkewSeconds))
            {
                return cachedToken.Token;
            }

            using var tokenRequest = new HttpRequestMessage(CreateHttpMethod(connectionConfig.TokenMethod), tokenUri);
            EsbTemplateRenderer.ApplyHeaders(tokenRequest, connectionConfig.TokenHeaders, templateContext);

            if (!string.IsNullOrWhiteSpace(body) && tokenRequest.Method != HttpMethod.Get)
            {
                tokenRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(tokenRequest);
            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw BadGateway("esb.webApiTokenRequestFailed", (int)response.StatusCode, response.ReasonPhrase ?? string.Empty);
            }

            var root = ParseJsonResponse(responseText);
            var tokenNode = EsbJsonHelper.SelectJsonPath(root, connectionConfig.TokenPath) ?? EsbJsonHelper.SelectJsonPath(root, "$.access_token");
            var token = tokenNode == null ? null : EsbJsonHelper.ReadJsonNodeAsString(tokenNode);
            if (string.IsNullOrWhiteSpace(token)) throw BadGateway("esb.webApiTokenNotFound");

            TokenCache[cacheKey] = new EsbCachedToken(token, ResolveTokenExpiresAt(root, connectionConfig));
            return token;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private static string BuildTokenCacheKey(EsbWebApiConnectionConfig connectionConfig, Uri tokenUri, string body)
    {
        var rawKey = JsonSerializer.Serialize(new
        {
            connectionConfig.BaseUrl,
            connectionConfig.AuthType,
            TokenUrl = tokenUri.ToString(),
            connectionConfig.TokenMethod,
            connectionConfig.TokenHeaders,
            Body = body,
            connectionConfig.TokenPath,
            connectionConfig.TokenExpiresInPath,
            connectionConfig.TokenExpiresAtPath
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
    }

    private static DateTimeOffset ResolveTokenExpiresAt(JsonNode root, EsbWebApiConnectionConfig connectionConfig)
    {
        var expiresAtNode = EsbJsonHelper.SelectJsonPath(root, connectionConfig.TokenExpiresAtPath);
        if (expiresAtNode != null)
        {
            var expiresAtText = EsbJsonHelper.ReadJsonNodeAsString(expiresAtNode);
            if (DateTimeOffset.TryParse(expiresAtText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiresAt))
            {
                return expiresAt.ToUniversalTime();
            }
        }

        var expiresInNode = EsbJsonHelper.SelectJsonPath(root, connectionConfig.TokenExpiresInPath) ?? EsbJsonHelper.SelectJsonPath(root, "$.expires_in");
        if (expiresInNode != null)
        {
            var expiresInText = EsbJsonHelper.ReadJsonNodeAsString(expiresInNode);
            if (int.TryParse(expiresInText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresInSeconds) &&
                expiresInSeconds > 0)
            {
                return DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
            }
        }

        return DateTimeOffset.UtcNow.AddMinutes(5);
    }

    private JsonNode ParseJsonResponse(string responseText)
    {
        try
        {
            return JsonNode.Parse(responseText) ?? new JsonObject();
        }
        catch (JsonException)
        {
            throw BadGateway("esb.webApiInvalidJson");
        }
    }

    private static int NormalizeMaxRows(int? value) => Math.Clamp(value ?? 500, 1, 1000);

    private static int NormalizeTimeoutSeconds(int? value) => Math.Clamp(value ?? 30, 1, 120);
}
