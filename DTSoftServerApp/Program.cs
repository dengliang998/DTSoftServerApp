using DTSoft.AppService.SysConfig;
using DTSoft.Core.Common;
using DTSoftServerApp.Extensions;
using DTSoftServerApp.Middleware;
using DTSoftServerApp.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using Serilog;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// =========================================
// 服务配置区域
// =========================================

// Serilog 日志配置（必须在最前面）
builder.Configuration.AddJsonFile("serilog.json", optional: true, reloadOnChange: true);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateBootstrapLogger();

try
{
    Log.Information("🚀 DTSoft Server 正在启动...");
    
    // 使用 Serilog 作为日志提供者
    builder.Host.UseSerilog();
    
    // 初始化 Yitter IdGenerator
    YitterHelper.Initialize(1);
    Encrypt.ConfigurePasswordHashing(builder.Configuration.GetValue<int?>("PasswordHashing:Iterations"));

    // 基础服务
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // 保持 PascalCase 命名，不使用默认的 camelCase
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
        });

    // Swagger/OpenAPI 配置
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "DTSoft Server API",
                Version = "V1",
                Description = "引擎服务接口"
            };
            return Task.CompletedTask;
        });
    });

    // 使用扩展方法组织配置
    builder.Services.AddHelpers();              // Helper 工具类
    builder.Services.AddAppServices();          // App 服务
    builder.Services.AddInfrastructure(builder.Configuration); // 基础设施服务
    
    // 添加日志队列服务（单例模式，后台运行）
    builder.Services.AddSingleton<LogQueueService>();
    builder.Services.AddSingleton<ILogQueueService>(sp => sp.GetRequiredService<LogQueueService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<LogQueueService>());

    var app = builder.Build();

    // =========================================
    // 中间件管道配置
    // =========================================

    // 系统初始化检查
    var initializeOnStartup = builder.Configuration.GetValue<bool>("InitializeOnStartup");
    if (initializeOnStartup)
    {
        using var scope = app.Services.CreateScope();
        var sysConfigApp = scope.ServiceProvider.GetRequiredService<SysConfigApp>();
        var result = sysConfigApp.InitializationSystem();
        if (!(bool)result["success"]!)
        {
            Log.Error($"❌ 系统初始化失败：{result["Msg"]}");
        }
        else
        {
            Log.Information($"✅ 系统初始化完成：{result["Msg"] ?? "系统初始化成功！"}");
        }
    }

    // API 文档配置
    var scalarEnabled = builder.Configuration.GetValue<bool>("ScalarEnabled");
    if (app.Environment.IsDevelopment() || scalarEnabled)
    {
        app.MapOpenApi();
        app.MapScalarApiReference("apidoc", options =>
        {
            options.WithTitle("DTSoft Server API");
        });
    }

    // 中间件顺序（重要！）
    app.UseHttpsRedirection();
    app.UseResponseCompression(); // 添加响应压缩
                                  // app.UseRateLimiter(); // 速率限制暂时不启用
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // 转发头配置（Nginx 代理）- 必须在认证和授权之前
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    // 全局异常处理（必须在最前面）
    app.UseExceptionHandling();

    // 业务中间件
    app.UseWebApiMiddleware();

    // 跨域
    app.UseCors("CorsPolicy");

    // 认证授权（必须在 UseForwardedHeaders 之后）
    app.UseAuthentication();
    app.UseAuthorization();

    // 控制器路由
    app.MapControllers();

    // 注册应用程序启动完成回调，输出炫酷的服务启动成功信息
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var asciiArt = @"
=====================================================
             DTSoft Server Starting Up
                    v10.10.001
=====================================================
";

        Log.Information(asciiArt);
        Log.Information("");
        Log.Information("╔═══════════════════════════════════════════════════╗");
        Log.Information($"║           DTSoft Server 启动成功!                 ║");
        Log.Information("╠═══════════════════════════════════════════════════╣");
        Log.Information($"║ API 监听地址：{string.Join(", ", app.Urls),-35} ║");
        Log.Information($"║ 运行环境：{app.Environment.EnvironmentName,-40}║");
        Log.Information($"║ 启动时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}                     ║");
        Log.Information("╚═══════════════════════════════════════════════════╝");

        if (scalarEnabled)
        {
            Log.Information($"║ API 文档地址：{string.Join(", ", app.Urls)}/apidoc              ║");
        }
        Log.Information("");
        Log.Information("✓ 服务已就绪，等待请求...");
    });

    // 启动服务
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ 应用程序启动失败");
}
finally
{
    await Log.CloseAndFlushAsync();
}
