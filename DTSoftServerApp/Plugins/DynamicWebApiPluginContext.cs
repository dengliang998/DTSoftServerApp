using DTSoft.Plugin.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace DTSoftServerApp.Plugins;

internal sealed class DynamicWebApiPluginContext(
    IServiceProvider services,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    IPluginDbContext dbContext,
    IDtSoftHelper dtSoftHelper) : IPluginContext
{
    private static readonly string[] UserClaimTypes =
    [
        ClaimTypes.NameIdentifier,
        "sub",
        ClaimTypes.Name,
        "name",
        "unique_name"
    ];

    public IConfiguration Configuration { get; } = configuration;

    public HttpContext? HttpContext => httpContextAccessor.HttpContext;

    public HttpRequest? Request => HttpContext?.Request;

    public ClaimsPrincipal? User => HttpContext?.User;

    public string? UserAccount => ResolveUserAccount(User);

    public IPluginDbContext DbContext { get; } = dbContext;

    public IDtSoftHelper DtSoftHelper { get; } = dtSoftHelper;

    public T GetRequiredService<T>() where T : notnull => services.GetRequiredService<T>();

    private static string? ResolveUserAccount(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        foreach (var claimType in UserClaimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
