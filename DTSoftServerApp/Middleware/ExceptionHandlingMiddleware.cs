using System.Net;
using System.Net.Mime;

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
                // 记录异常日志
                _logger.LogError(ex, "未处理的异常：{RequestPath}, 用户：{UserAccount}", 
                    context.Request.Path, 
                    context.User?.Identity?.Name ?? "Anonymous");

                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
            
            // 根据异常类型返回对应的 HTTP 状态码和错误信息
            var (httpStatusCode, message) = MapExceptionToResponse(exception);
            
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
        private static (int statusCode, string message) MapExceptionToResponse(Exception exception)
        {
            return exception switch
            {
                // 400 Bad Request - 客户端请求错误
                ArgumentNullException => (StatusCodes.Status400BadRequest, "必需参数缺失"),
                ArgumentException => (StatusCodes.Status400BadRequest, "参数格式不正确"),
                
                // 401 Unauthorized - 认证失败
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "未授权访问，请登录"),
                
                // 403 Forbidden - 权限不足
                System.Security.SecurityException => (StatusCodes.Status403Forbidden, "权限不足，无法执行此操作"),
                
                // 404 Not Found - 资源不存在
                KeyNotFoundException => (StatusCodes.Status404NotFound, "请求的资源不存在"),
                
                // 409 Conflict - 资源冲突
                InvalidOperationException when exception.Message.Contains("已存在") => 
                    (StatusCodes.Status409Conflict, "资源已存在，无法重复创建"),
                
                // 408 Request Timeout - 请求超时
                TimeoutException => (StatusCodes.Status408RequestTimeout, "请求超时，请稍后重试"),
                
                // 423 Locked - 资源被锁定
                InvalidOperationException when exception.Message.Contains("锁定") => 
                    (StatusCodes.Status423Locked, "资源已被锁定"),
                
                // 500 Internal Server Error - 服务器内部错误
                _ => (StatusCodes.Status500InternalServerError, "系统内部错误，请稍后重试")
            };
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
