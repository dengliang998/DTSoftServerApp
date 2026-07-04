using DTSoft.AppService.Attachment;
using DTSoft.AppService.ApiKey;
using DTSoft.AppService.Ou;
using DTSoft.AppService.DynamicApp;
using DTSoft.AppService.Log;
using DTSoft.AppService.Menu;
using DTSoft.AppService.Role;
using DTSoft.AppService.SysConfig;
using DTSoft.AppService.User;
using DTSoft.Core.Cache;
using DTSoft.Core.Common;
using DTSoft.Core.DbProviders;
using DTSoft.Core.Interfaces;
using DTSoftServerApp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DTSoftServerApp.Extensions
{
    /// <summary>
    /// 服务集合扩展方法
    /// 用于组织和管理依赖注入配置
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加 Helper 工具类（Scoped 生命周期）
        /// </summary>
        public static IServiceCollection AddHelpers(this IServiceCollection services)
        {
            services.AddScoped<DtSoftHelper>();
            services.AddScoped<ConfigHelper>();
            services.AddScoped<UserCacheHelper>();

            return services;
        }

        /// <summary>
        /// 添加应用服务（Scoped 生命周期）
        /// </summary>
        public static IServiceCollection AddAppServices(this IServiceCollection services)
        {
            // App 服务
            services.AddScoped<MenuApp>();
            services.AddScoped<AttachmentApp>();
            services.AddScoped<LogApp>();
            services.AddScoped<RoleApp>();
            services.AddScoped<UserApp>();
            services.AddScoped<SysConfigApp>();
            services.AddScoped<DynamicConfigApp>();
            services.AddScoped<DynamicTableService>();
            services.AddScoped<OuApp>();
            services.AddScoped<ApiKeyApp>();

            // 其他服务
            services.AddScoped<JwtService>();
            services.AddScoped<CaptchaService>();

            return services;
        }

        /// <summary>
        /// 添加基础设施服务
        /// </summary>
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            #region Kestrel 服务器配置

            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 100000000; // 100MB
                options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(20);
                options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(20);
            });

            #endregion

            #region 跨域配置

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .SetPreflightMaxAge(TimeSpan.FromHours(1));
                });
            });

            #endregion

            #region JWT 认证配置

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // SignalR 获取登录用户
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/chatHub")))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        // 接口认证失败返回处理 - 返回 401 状态码
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Clear();
                        context.Response.ContentType = "application/json";
                        var rv = new JsonObject
                        {
                            ["success"] = false,
                            ["statusCode"] = 401,  // 保留 statusCode 字段
                            ["message"] = "身份验证失败，请登录"
                        };
                        return context.Response.WriteAsync(rv.ToString());
                    }
                };
            });

            #endregion

            #region 数据库配置

            DatabaseConfigurationService.ConfigureDatabase(services, configuration);

            #endregion

            #region 缓存配置

            var cacheModel = configuration["CacheModel"] ?? "MemoryCache";
            switch (cacheModel)
            {
                case "RedisCache":
                    services.AddScoped<IDtSoftCache, RedisCache>();
                    break;
                default:
                    services.AddScoped<IDtSoftCache, MemoryCache>();
                    break;
            }

            #endregion

            #region 基础服务

            services.AddHttpContextAccessor();
            services.AddSignalR();
            services.AddMemoryCache();

            #endregion

            #region 响应压缩配置

            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<GzipCompressionProvider>();
                options.Providers.Add<BrotliCompressionProvider>();
            });

            services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = System.IO.Compression.CompressionLevel.Optimal;
            });

            services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = System.IO.Compression.CompressionLevel.Optimal;
            });

            #endregion

            // TODO: Rate Limiting 在.NET 10 中的 API 有变化，暂时不启用
            // #region 速率限制配置
            //
            // services.AddRateLimiter(options =>
            // {
            //     options.GlobalLimiter = Microsoft.AspNetCore.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            //         partitionKey: "global",
            //         factory: partition => new Microsoft.AspNetCore.RateLimiting.FixedWindowRateLimiterOptions
            //         {
            //             PermitLimit = 100,
            //             Window = TimeSpan.FromMinutes(1),
            //             QueueProcessingOrder = Microsoft.AspNetCore.RateLimiting.QueueProcessingOrder.OldestFirst,
            //             QueueLimit = 10
            //         });
            //
            //     options.OnRejected = async (context, token) =>
            //     {
            //         context.HttpContext.Response.StatusCode = 429;
            //         context.HttpContext.Response.ContentType = "application/json";
            //
            //         var response = new
            //         {
            //             success = false,
            //             StateCode = (int)DTSoft.Models.Enums.ErrorCode.GeneralError,
            //             Msg = "请求过于频繁，请稍后再试",
            //             Data = (object?)null
            //         };
            //
            //         await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: token);
            //     };
            // });
            //
            // #endregion

            return services;
        }
    }
}
