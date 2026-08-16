using DTSoft.AppService.Localization;
using DTSoft.AppService.SysConfig;
using DTSoft.Core.Common;
using DTSoft.Core.Licensing;
using DTSoftServerApp.Extensions;
using DTSoftServerApp.Middleware;
using DTSoftServerApp.Plugins;
using DTSoftServerApp.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.OpenApi;
using Serilog;
using Scalar.AspNetCore;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
var entryAssembly = Assembly.GetEntryAssembly();
var applicationVersion = entryAssembly?.GetName().Version?.ToString() ?? "-";
var startupLocalizer = LocalizationConfigurationExtensions.CreateAppLocalizer(builder.Configuration);

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
    Log.Information(
        """
        =====================================================
        [STARTUP] DTSoft Server starting
        -----------------------------------------------------
          Version      : {Version}
          Environment  : {Environment}
          Content Root : {ContentRoot}
        =====================================================
        """,
        applicationVersion == "-" ? "-" : $"v{applicationVersion}",
        builder.Environment.EnvironmentName,
        builder.Environment.ContentRootPath);

    // 使用 Serilog 作为日志提供者
    builder.Host.UseSerilog();

    // 初始化 Yitter IdGenerator
    YitterHelper.Initialize(1);
    Log.Information("[STARTUP] Yitter IdGenerator initialized. WorkerId: {WorkerId}", 1);
    Encrypt.ConfigurePasswordHashing(
        builder.Configuration.GetValue<int?>(AppConfigurationKeys.Security.PasswordHashing.Iterations)
        ?? builder.Configuration.GetValue<int?>(AppConfigurationKeys.Security.PasswordHashing.LegacyIterations));

    // 基础服务
    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
    builder.Services.AddDtSoftRequestLocalization(builder.Configuration);
    var appResourceAssemblyName = typeof(AppLocalizer).Assembly.GetName().Name!;
    var mvcBuilder = builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // 保持 PascalCase 命名，不使用默认的 camelCase
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
        })
        .AddDataAnnotationsLocalization(options =>
        {
            options.DataAnnotationLocalizerProvider = (_, factory) =>
                factory.Create("DTResource", appResourceAssemblyName);
        });

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var localizer = context.HttpContext.RequestServices.GetRequiredService<IAppLocalizer>();
            var errors = context.ModelState.GetErrorMessages()
                .ToArray();

            var message = errors.Length == 0
                ? localizer["common.argumentMissing"]
                : string.Join(";", errors);

            return new BadRequestObjectResult(new
            {
                Code = 400,
                Message = message
            });
        };
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
                Description = startupLocalizer["openApi.description"]
            };
            return Task.CompletedTask;
        });
    });

    // 使用扩展方法组织配置
    builder.Services.AddHelpers();              // Helper 工具类
    builder.Services.AddAppServices(builder.Configuration);          // App 服务
    builder.Services.AddInfrastructure(builder.Configuration); // 基础设施服务
    var pluginLoadResult = builder.Services.AddDynamicWebApiPlugins(builder.Configuration);
    DynamicWebApiPluginLoader.RegisterApplicationParts(mvcBuilder, pluginLoadResult);

    // 添加日志队列服务（单例模式，后台运行）
    builder.Services.AddSingleton<LogQueueService>();
    builder.Services.AddSingleton<ILogQueueService>(sp => sp.GetRequiredService<LogQueueService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<LogQueueService>());

    var app = builder.Build();

    // 许可证固定从程序根目录读取，替换许可证后重启生效。
    var licenseService = app.Services.GetRequiredService<LicenseService>();
    licenseService.ValidateOnStartup();
    if (licenseService.IsValid)
    {
        var license = licenseService.Current;
        var licenseTypes = license.HasType(LicenseType.Temporary)
            ? startupLocalizer["license.temporaryTypeName"]
            : startupLocalizer["license.officialTypeName"];
        var expireAt = license.ExpireAt?.ToString("yyyy-MM-dd") ?? startupLocalizer["license.unlimitedTime"];
        var maxConcurrentUsers = license.HasType(LicenseType.Temporary)
            ? startupLocalizer["license.notControlled"]
            : license.MaxConcurrentUsers == -1 ? startupLocalizer["license.unlimited"] : license.MaxConcurrentUsers?.ToString() ?? "-";

        Log.Information(
            """
            [LICENSE]
              Status      : Valid
              Customer    : {Customer}
              Type        : {LicenseTypes}
              Expire At   : {ExpireAt}
              Max Users   : {MaxConcurrentUsers}
            """,
            license.Customer,
            licenseTypes,
            expireAt,
            maxConcurrentUsers);
    }
    else
    {
        Log.Warning(
            """
            [LICENSE]
              Status  : Invalid
              Message : {Message}
              Effect  : {Effect}
            """,
            licenseService.ErrorMessage,
            startupLocalizer["startup.licenseUnauthorizedEffect"]);
    }

    if (pluginLoadResult.Plugins.Count > 0)
    {
        var pluginLines = string.Join(
            Environment.NewLine,
            pluginLoadResult.Plugins.Select(plugin =>
                $"  - {plugin.PluginName ?? plugin.AssemblyName} | Assembly: {plugin.AssemblyName} | Controllers: {plugin.ControllerTypes.Count} | Modules: {plugin.ModuleTypes.Count}"));

        Log.Information(
            """
            [PLUGINS]
              Loaded : {Count}
            {PluginLines}
            """,
            pluginLoadResult.Plugins.Count,
            pluginLines);
    }
    else
    {
        Log.Information(
            """
            [PLUGINS]
              Loaded : 0
            """);
    }

    foreach (var failure in pluginLoadResult.Failures)
    {
        Log.Warning("[PLUGINS] Load failed: {FilePath} | {Message} | {ExceptionType}",
            failure.FilePath,
            failure.Message,
            failure.ExceptionType);
    }

    // =========================================
    // 中间件管道配置
    // =========================================

    // 系统初始化检查
    var initializeOnStartup = builder.Configuration.GetValue<bool?>(
        AppConfigurationKeys.Application.InitializeOnStartup)
        ?? builder.Configuration.GetValue<bool>(AppConfigurationKeys.Application.LegacyInitializeOnStartup);
    if (initializeOnStartup)
    {
        using var scope = app.Services.CreateScope();
        var sysConfigApp = scope.ServiceProvider.GetRequiredService<SysConfigApp>();
        var result = sysConfigApp.InitializationSystem();
        if (!(bool)result["success"]!)
        {
            Log.Error(
                """
                [SYSTEM INIT]
                  Status  : Failed
                  Message : {Message}
                """,
                result["Msg"]);
        }
        else
        {
            Log.Information(
                """
                [SYSTEM INIT]
                  Status  : Completed
                  Message : {Message}
                """,
                result["Msg"] ?? startupLocalizer["sysConfig.initializationSuccess"]);
        }
    }

    // API 文档配置
    var scalarEnabled = builder.Configuration.GetValue<bool?>(
        AppConfigurationKeys.ApiDocumentation.Enabled)
        ?? builder.Configuration.GetValue<bool>(AppConfigurationKeys.ApiDocumentation.LegacyEnabled);
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
    var staticFileContentTypes = new FileExtensionContentTypeProvider();
    staticFileContentTypes.Mappings[".vue"] = "text/plain";

    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = staticFileContentTypes
    });

    // 转发头配置（Nginx 代理）- 必须在认证和授权之前
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    // 全局异常处理（必须在最前面）
    app.UseDtSoftRequestLocalization();
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

    // 注册应用程序启动完成回调，输出服务启动摘要
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var listeningUrls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : startupLocalizer["startup.urlsUnbound"];
        var apiDocUrls = scalarEnabled
            ? string.Join(", ", app.Urls.Select(url => $"{url.TrimEnd('/')}/apidoc"))
            : startupLocalizer["startup.apiDocsDisabled"];
        var versionText = applicationVersion == "-" ? "-" : $"v{applicationVersion}";
        var startedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        Log.Information(
            """
            
            =====================================================
            [READY] DTSoft Server is ready
            -----------------------------------------------------
              Version     : {Version}
              Environment : {Environment}
              URLs        : {Urls}
              API Docs    : {ApiDocs}
              Started At  : {StartedAt}
            =====================================================
            """,
            versionText,
            app.Environment.EnvironmentName,
            listeningUrls,
            apiDocUrls,
            startedAt);
    });

    // 启动服务
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "[STARTUP] {Message}", startupLocalizer["startup.applicationStartFailed"]);
}
finally
{
    await Log.CloseAndFlushAsync();
}
