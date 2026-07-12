using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DTSoft.Plugin.Abstractions;

/// <summary>
/// 动态 WebApi 插件的可选注册入口。
/// 插件程序集如果需要注册自己的服务，可以实现此接口。
/// </summary>
public interface IDynamicWebApiPlugin
{
    /// <summary>
    /// 注册插件自身需要的服务。
    /// </summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
