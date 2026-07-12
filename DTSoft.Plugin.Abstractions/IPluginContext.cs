using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace DTSoft.Plugin.Abstractions;

/// <summary>
/// 插件在运行时可访问的宿主上下文。
/// </summary>
public interface IPluginContext
{
    IConfiguration Configuration { get; }

    HttpContext? HttpContext { get; }

    HttpRequest? Request { get; }

    ClaimsPrincipal? User { get; }

    string? UserAccount { get; }

    IPluginDbContext DbContext { get; }

    IDtSoftHelper DtSoftHelper { get; }

    T GetRequiredService<T>() where T : notnull;
}
