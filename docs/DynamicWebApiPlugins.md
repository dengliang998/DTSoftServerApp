# 动态 WebAPI 插件开发说明

## 放置目录

系统启动时会扫描发布目录下的 `UserDll` 文件夹，也可以通过配置修改：

```json
{
  "DynamicWebApi": {
    "Enabled": true,
    "PluginDirectory": "UserDll"
  }
}
```

`PluginDirectory` 支持相对路径和绝对路径。相对路径基于 `AppContext.BaseDirectory`，也就是程序发布目录。

## 插件 Controller 示例

外部插件项目建议引用：

- `DTSoft.Plugin.Abstractions`
- `DTSoft.AppService`（可选：需要通过 `GetRequiredService<T>()` 调用内置应用服务时引用）
- `DTSoft.Models`（可选：调用 AppService 方法需要参数/DTO 类型时引用）

不要直接引用 `DTSoft.Core` 等宿主内部基础设施程序集。宿主能力优先通过 `IPluginContext` 暴露；插件加载时只允许 `DTSoft.Plugin.Abstractions`、`DTSoft.AppService`、`DTSoft.Models` 这几个 `DTSoft.*` 依赖。

示例：

```csharp
using DTSoft.Plugin.Abstractions;
using DTSoft.AppService.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UserPlugin;

[ApiController]
[Authorize]
[Route("api/plugin/user/[controller]/[action]")]
public sealed class DemoController(IPluginContext pluginContext) : ControllerBase
{
    [HttpGet]
    public IActionResult Me()
    {
        var account = pluginContext.UserAccount;
        if (string.IsNullOrWhiteSpace(account))
        {
            return Unauthorized(new { success = false, Msg = "未登录" });
        }

        var isAdmin = pluginContext.DtSoftHelper.IsAdmin(account);
        var requestPath = pluginContext.Request?.Path.Value;

        return Ok(new { success = true, data = new { account, isAdmin, requestPath } });
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var account = pluginContext.UserAccount;
        if (string.IsNullOrWhiteSpace(account))
        {
            return Unauthorized(new { success = false, Msg = "未登录" });
        }

        var userApp = pluginContext.GetRequiredService<UserApp>();
        return Ok(await userApp.GetUserInfoAsync(account));
    }
}
```

访问路径示例：

```text
GET /api/plugin/user/Demo/Me
```

## 注册插件私有服务

如果插件需要注册自己的服务，可以实现 `IDynamicWebApiPlugin`：

```csharp
using DTSoft.Plugin.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UserPlugin;

public sealed class UserPluginModule : IDynamicWebApiPlugin
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<UserPluginService>();
    }
}
```

## 宿主能力调用

插件 Controller 可以通过构造函数注入 `IPluginContext`，再访问宿主显式开放的能力：

- `Configuration`：读取宿主配置
- `HttpContext` / `Request`：访问当前请求
- `User` / `UserAccount`：访问当前登录用户
- `DbContext`：通过 `Set<TEntity>()`、`Database`、`GetDbConnection()` 访问数据库
- `DtSoftHelper`：管理员判断、角色判断、账号状态等系统能力
- `GetRequiredService<T>()`：按需取宿主已经注册的服务，例如 `pluginContext.GetRequiredService<UserApp>()` 或 `pluginContext.GetRequiredService<ILogger<MyPlugin>>()`

推荐优先使用 `IPluginContext` 上的显式属性；需要复用 `DTSoft.AppService` 里的应用服务时，再通过 `GetRequiredService<T>()` 获取。`DTSoft.Core` 仍属于宿主内部基础设施，不对插件开放直接引用。

## 插件自定义实体

外部 DLL 里定义的实体要先注册到宿主 EF Core 模型，之后才能通过 `pluginContext.DbContext.Set<TEntity>()` 使用。插件可以实现 `IPluginEntityModelConfiguration`，宿主加载插件时会自动注册这些配置类。

实体示例：

```csharp
namespace UserPlugin.Entities;

public sealed class PluginOrder
{
    public long Id { get; set; }

    public string OrderNo { get; set; } = "";

    public string CreatedBy { get; set; } = "";

    public DateTime CreatedTime { get; set; }
}
```

模型配置示例：

```csharp
using DTSoft.Plugin.Abstractions;
using Microsoft.EntityFrameworkCore;
using UserPlugin.Entities;

namespace UserPlugin;

public sealed class PluginOrderModelConfiguration : IPluginEntityModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PluginOrder>(entity =>
        {
            entity.ToTable("plugin_order");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OrderNo).HasMaxLength(50);
            entity.Property(x => x.CreatedBy).HasMaxLength(50);
        });
    }
}
```

`ToTable` 是 `Microsoft.EntityFrameworkCore.Relational` 包提供的扩展方法，命名空间仍然是 `Microsoft.EntityFrameworkCore`。如果插件项目是直接引用 `DTSoft.Plugin.Abstractions.dll` 文件，而不是通过项目引用或 NuGet 包引用，请在插件项目里显式添加：

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="9.0.14" />
```

查询示例：

```csharp
using Microsoft.EntityFrameworkCore;
using UserPlugin.Entities;

[HttpGet]
public async Task<IActionResult> Orders()
{
    var account = pluginContext.UserAccount ?? "";

    var orders = await pluginContext.DbContext
        .Set<PluginOrder>()
        .AsNoTracking()
        .Where(x => x.CreatedBy == account)
        .ToListAsync();

    return Ok(new { success = true, data = orders });
}
```

实体注册只负责让 EF Core 认识模型，不负责自动升级已有数据库表结构。插件表需要通过插件自己的初始化 SQL、后台初始化逻辑，或运维脚本提前创建；也可以用 `pluginContext.DbContext.Database` 或 `GetDbConnection()` 执行建表 SQL。

## 发布注意事项

插件 DLL 放入 `UserDll` 后需要重启主程序才能生效。第一版不做热插拔。

如果插件只引用宿主已有程序集，通常只需要放插件 DLL。若插件引用第三方私有依赖，请把插件发布输出中的依赖文件一起放到 `UserDll`，并保留 `.deps.json`。
