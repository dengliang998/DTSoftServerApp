using System.Net;
using System.Net.Mime;
using DTSoft.AppService.Localization;

namespace DTSoftServerApp.Middleware
{
    /// <summary>
    /// 全局异常处理中间件
    /// 捕获所有未处理的异常并返回统一的 JSON 格式响应
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var localizer = context.RequestServices.GetRequiredService<IAppLocalizer>();

                // 记录异常日志
                _logger.LogError(ex, localizer["exception.unhandledLog"],
                    context.Request.Path, 
                    context.User?.Identity?.Name ?? "Anonymous");

                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
            
            // 根据异常类型返回对应的 HTTP 状态码和错误信息
            var localizer = context.RequestServices.GetRequiredService<IAppLocalizer>();
            var (httpStatusCode, messageKey) = MapExceptionToResponse(exception);
            var message = localizer[messageKey];
            
            var response = new
            {
                success = false,
                statusCode = httpStatusCode,  // 保留 statusCode 字段（与 HTTP 状态码一致）
                message = environment.IsDevelopment() ? exception.Message : message,
                data = (object?)null,
                
                // 开发环境下返回详细错误信息（包括堆栈跟踪）
                error = environment.IsDevelopment() 
                    ? new { exception.Message, exception.StackTrace, InnerException = exception.InnerException?.Message }
                    : null
            };

            context.Response.StatusCode = httpStatusCode;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            var jsonResponse = System.Text.Json.JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(jsonResponse);
        }

        /// <summary>
        /// 将异常类型映射到 HTTP 状态码和消息
        /// </summary>
        private static (int statusCode, string messageKey) MapExceptionToResponse(Exception exception)
        {
            return exception switch
            {
                // 400 Bad Request - 客户端请求错误
                ArgumentNullException => (StatusCodes.Status400BadRequest, "validation.argumentMissing"),
                ArgumentException => (StatusCodes.Status400BadRequest, "validation.argumentInvalid"),
                
                // 401 Unauthorized - 认证失败
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "auth.unauthorized"),
                
                // 403 Forbidden - 权限不足
                System.Security.SecurityException => (StatusCodes.Status403Forbidden, "auth.forbidden"),
                
                // 404 Not Found - 资源不存在
                KeyNotFoundException => (StatusCodes.Status404NotFound, "resource.notFound"),
                
                // 409 Conflict - 资源冲突
                InvalidOperationException when IsConflictException(exception) =>
                    (StatusCodes.Status409Conflict, "resource.conflict"),
                
                // 408 Request Timeout - 请求超时
                TimeoutException => (StatusCodes.Status408RequestTimeout, "request.timeout"),
                
                // 423 Locked - 资源被锁定
                InvalidOperationException when IsLockedException(exception) =>
                    (StatusCodes.Status423Locked, "resource.locked"),
                
                // 500 Internal Server Error - 服务器内部错误
                _ => (StatusCodes.Status500InternalServerError, "system.error")
            };
        }

        private static bool IsConflictException(Exception exception)
        {
            return exception.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLockedException(Exception exception)
        {
            return exception.Message.Contains("locked", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 扩展方法用于注册中间件
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static void UseExceptionHandling(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
