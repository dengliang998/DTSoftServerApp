using DTSoft.Core.Interfaces;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DTSoftServerApp.Services;

/// <summary>
/// 登录图形验证码服务
/// </summary>
public class CaptchaService(IDtSoftCache cache)
{
    private const string CacheKeyPrefix = "auth:captcha:";
    private const string CodeChars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 创建验证码
    /// </summary>
    public async Task<CaptchaResult> CreateAsync()
    {
        var captchaId = Guid.NewGuid().ToString("N");
        var code = GenerateCode(4);
        var svg = GenerateSvg(code);
        var imageBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));

        await cache.SetAsync(CacheKeyPrefix + captchaId, code, Expiration);

        return new CaptchaResult
        {
            CaptchaId = captchaId,
            ImageBase64 = imageBase64,
            ImageDataUrl = $"data:image/svg+xml;base64,{imageBase64}",
            ExpiresInSeconds = (int)Expiration.TotalSeconds
        };
    }

    /// <summary>
    /// 校验并消费验证码，校验后无论是否成功都会删除缓存
    /// </summary>
    public async Task<(bool Success, string Message)> ValidateAsync(string? captchaId, string? captchaCode)
    {
        if (string.IsNullOrWhiteSpace(captchaId) || string.IsNullOrWhiteSpace(captchaCode))
            return (false, "验证码不能为空");

        var cacheKey = CacheKeyPrefix + captchaId.Trim();
        var code = await cache.GetAsync<string>(cacheKey);
        if (code is not null)
            await cache.RemoveAsync(cacheKey);

        if (string.IsNullOrWhiteSpace(code))
            return (false, "验证码已过期，请刷新后重试");

        if (!string.Equals(code, captchaCode.Trim(), StringComparison.OrdinalIgnoreCase))
            return (false, "验证码错误");

        return (true, "验证码校验通过");
    }

    private static string GenerateCode(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)];
        }

        return new string(chars);
    }

    private static string GenerateSvg(string code)
    {
        var escapedCode = WebUtility.HtmlEncode(code);
        var noiseLines = new StringBuilder();
        var noiseDots = new StringBuilder();

        for (var i = 0; i < 6; i++)
        {
            var x1 = RandomNumberGenerator.GetInt32(0, 120);
            var y1 = RandomNumberGenerator.GetInt32(0, 40);
            var x2 = RandomNumberGenerator.GetInt32(0, 120);
            var y2 = RandomNumberGenerator.GetInt32(0, 40);
            noiseLines.Append($"""<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="#b8c2cc" stroke-width="1" opacity="0.6"/>""");
        }

        for (var i = 0; i < 24; i++)
        {
            var cx = RandomNumberGenerator.GetInt32(0, 120);
            var cy = RandomNumberGenerator.GetInt32(0, 40);
            noiseDots.Append($"""<circle cx="{cx}" cy="{cy}" r="1" fill="#8b98a5" opacity="0.5"/>""");
        }

        return $"""
                <svg xmlns="http://www.w3.org/2000/svg" width="120" height="40" viewBox="0 0 120 40">
                    <rect width="120" height="40" rx="4" fill="#f5f7fa"/>
                    {noiseLines}
                    {noiseDots}
                    <text x="60" y="27" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="24" font-weight="700" fill="#1f2933" letter-spacing="5">{escapedCode}</text>
                </svg>
                """;
    }
}

/// <summary>
/// 验证码响应
/// </summary>
public class CaptchaResult
{
    /// <summary>
    /// 验证码 ID，登录时原样传回
    /// </summary>
    public string CaptchaId { get; set; } = string.Empty;

    /// <summary>
    /// SVG 图片 Base64，不含 data URL 前缀
    /// </summary>
    public string ImageBase64 { get; set; } = string.Empty;

    /// <summary>
    /// 可直接用于 img src 的 data URL
    /// </summary>
    public string ImageDataUrl { get; set; } = string.Empty;

    /// <summary>
    /// 验证码有效期，单位：秒
    /// </summary>
    public int ExpiresInSeconds { get; set; }
}
