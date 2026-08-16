namespace DTSoft.AppService.Esb;

internal sealed class EsbWebApiConnectionConfig
{
    public string? BaseUrl { get; set; }

    public string? AuthType { get; set; }

    public string? Token { get; set; }

    public string? TokenUrl { get; set; }

    public string? TokenMethod { get; set; }

    public Dictionary<string, string>? TokenHeaders { get; set; }

    public string? TokenBody { get; set; }

    public string? TokenPath { get; set; }

    public string? TokenExpiresInPath { get; set; }

    public string? TokenExpiresAtPath { get; set; }

    public int? TokenRefreshSkewSeconds { get; set; }

    public string? ApiKeyName { get; set; }

    public string? ApiKeyValue { get; set; }

    public string? ApiKeyIn { get; set; }

    public Dictionary<string, string>? Headers { get; set; }
}

internal sealed class EsbWebApiRequestConfig
{
    public string? Method { get; set; }

    public string? Path { get; set; }

    public Dictionary<string, string>? Query { get; set; }

    public Dictionary<string, string>? Headers { get; set; }

    public string? Body { get; set; }

    public string? ContentType { get; set; }

    public string? ResultPath { get; set; }

    public string? TotalPath { get; set; }
}

internal sealed record EsbCachedToken(string Token, DateTimeOffset ExpiresAt);
