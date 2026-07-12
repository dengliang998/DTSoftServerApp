using DTSoft.Plugin.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DTSoftServerApp.Plugins;

public static class DynamicWebApiServiceCollectionExtensions
{
    public static DynamicWebApiPluginLoadResult AddDynamicWebApiPlugins(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IPluginContext, DynamicWebApiPluginContext>();
        return DynamicWebApiPluginLoader.Load(services, configuration);
    }
}
