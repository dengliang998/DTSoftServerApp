using Microsoft.EntityFrameworkCore;

namespace DTSoft.Plugin.Abstractions;

/// <summary>
/// 插件实体模型配置入口。
/// </summary>
public interface IPluginEntityModelConfiguration
{
    void Configure(ModelBuilder modelBuilder);
}
