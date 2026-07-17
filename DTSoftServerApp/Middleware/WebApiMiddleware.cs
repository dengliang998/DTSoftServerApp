using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Models.Entities;
using System.Text;
using System.Text.Json;
using DTSoftServerApp.Services;
using System.Text.RegularExpressions;

namespace DTSoftServerApp.Middleware
{
    public class WebApiMiddleware(
        RequestDelegate next,
        ILogger<WebApiMiddleware> logger,
        ILogQueueService logQueueService)
    {
        private const int MaxLoggedBodyBytes = 64 * 1024;
        private const int MaxLoggedFormValueLength = 4 * 1024;
        private static readonly Regex SensitiveJsonStringFieldRegex = new(
            "(\"(?:(?:Username)|(?:UserName)|(?:PassWord)|(?:Password)|(?:SecretKey)|(?:AccessToken)|(?:RefreshToken)|(?:Token))\"\\s*:\\s*\")([^\"]*)(\")",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex JwtLikeRegex = new(
            "(eyJ[a-zA-Z0-9_\\-]{10,}\\.[a-zA-Z0-9_\\-]{10,}\\.[a-zA-Z0-9_\\-]{10,})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public async Task InvokeAsync(HttpContext context, DtSoftHelper dtSoftHelper, OnlineUserService onlineUserService)
        {
            // 检查是否为API请求，如果不是则跳过中间件处理
            var requestPath = context.Request.Path.HasValue ? context.Request.Path.Value : string.Empty;

            // 只处理API请求，跳过静态资源等其他请求
            if (!IsApiRequest(requestPath))
            {
                await next(context);
                return;
            }

            // 记录请求开始时间
            var startTime = DateTime.Now;
            var requestMethod = context.Request.Method;
            var clientIp = GetClientIp(context);

            // 读取请求体内容（仅对POST、PUT等方法）
            string requestBody = "";
            if (requestMethod.ToUpper() == "POST" || requestMethod.ToUpper() == "PUT" || requestMethod.ToUpper() == "PATCH")
            {
                requestBody = await TryReadRequestBodyForLoggingAsync(context.Request, requestPath);
            }

            // 在调用下一个中间件之前执行的逻辑（相当于 OnActionExecuting ）
            #region 账号状态检测
            string? token = context.Request.Headers["Authorization"];
            string? userAccount = null;

            if (!string.IsNullOrEmpty(token))
            {
                // 移除Bearer前缀
                if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = token["Bearer ".Length..].Trim();
                }

                // 使用新的JWT解析方法获取用户账号
                userAccount = dtSoftHelper.GetLoginUserAccountFromJwt(token);
                JsonObject rv = await dtSoftHelper.CheckAccStatus(userAccount);

                // 修复CS8602/CS8604警告：安全访问JSON属性
                var successValue = rv["success"];
                if (successValue != null && successValue.GetValueKind() != JsonValueKind.Undefined && !(bool)successValue)
                {
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(Convert.ToString(rv)!);
                    return; // 如果账号状态检查失败，直接返回，不执行后续中间件
                }

                await onlineUserService.MarkActiveAsync(userAccount);
            }
            #endregion

            // 捕获响应内容用于日志（不要缓存/重写整个响应，避免影响下载/大响应吞吐）
            var originalResponseBody = context.Response.Body;
            var captureResponse = !ShouldSkipResponseLoggingByPath(requestPath);
            using var responseCapture = captureResponse ? new MemoryStream() : null;
            var captureStream = captureResponse ? responseCapture! : Stream.Null;
            await using var teeStream = new TeeStream(originalResponseBody, captureStream, captureResponse ? MaxLoggedBodyBytes : 0);
            context.Response.Body = teeStream;

            // 调用下一个中间件，捕获可能的异常
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                // 记录业务异常日志，但不处理响应（由 ExceptionHandlingMiddleware 统一处理）
                logger.LogError(ex, "请求处理过程中发生未处理异常：{RequestPath}, 用户：{UserAccount}, IP: {ClientIP}", 
                    requestPath, userAccount, clientIp);
                throw; // 重新抛出异常，让 ExceptionHandlingMiddleware 统一处理
            }
            finally
            {
                context.Response.Body = originalResponseBody;
            }

            // 读取响应内容（用于日志）
            string responseContent = string.Empty;

            // 检查是否为文件下载响应（二进制数据），如果是则跳过文本处理
            var isFileDownload = context.Response.ContentType?.Contains("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") == true ||
                                 context.Response.ContentType?.Contains("application/octet-stream") == true ||
                                 context.Response.ContentType?.Contains("application/pdf") == true ||
                                 context.Response.Headers.ContainsKey("Content-Disposition");

            if (!isFileDownload && captureResponse && responseCapture!.Length > 0)
            {
                responseContent = Encoding.UTF8.GetString(responseCapture.GetBuffer(), 0, (int)responseCapture.Length);
                if (teeStream.IsTruncated)
                {
                    responseContent += " ...(truncated)";
                }
            }

            // 出于性能考虑：不再修改响应体内容，仅做日志记录

            // 记录完整请求日志
            requestBody = RedactSensitiveContent(requestBody);
            responseContent = RedactSensitiveContent(responseContent);
            LogRequestAsync(userAccount, requestPath, requestMethod, clientIp, requestBody, responseContent, startTime, context);
        }

        private static bool IsApiRequest(string path)
        {
            // 定义API请求的特征
            // 通常API请求路径包含 /api 或其他特定标识
            if (string.IsNullOrEmpty(path))
                return false;

            // 忽略静态资源文件请求
            var extensions = new[]
            {
                ".css", ".js", ".jpg", ".jpeg", ".png", ".gif", ".ico", ".svg", ".woff", ".woff2", ".ttf", ".eot",
                ".map", ".html", ".htm"
            };
            return !extensions.Any(ext => path.ToLower().EndsWith(ext)) &&
                   // 只处理API请求（路径中包含 /api 或以 /api 开头）
                   path.StartsWith("/api", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetClientIp(HttpContext context)
        {
            // 尝试从 X-Forwarded-For 头部获取真实IP
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            // 尝试从 X-Real-IP 头部获取真实IP
            var realIp = context.Request.Headers["X-Real-IP"].ToString();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp.Trim();
            }

            // 默认返回远程IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private static async Task<string> TryReadRequestBodyForLoggingAsync(HttpRequest request, string requestPath)
        {
            if (request.Body == Stream.Null)
                return string.Empty;

            if (ShouldSkipBodyLoggingByPath(requestPath))
                return "[skipped: endpoint]";

            var contentType = request.ContentType ?? string.Empty;
            if (request.HasFormContentType)
                return await ReadFormForLoggingAsync(request, contentType);

            var contentLength = request.ContentLength;
            if (contentLength.HasValue && contentLength.Value > MaxLoggedBodyBytes)
                return $"[skipped: body too large ({contentLength.Value} bytes)]";

            request.EnableBuffering();

            try
            {
                request.Body.Position = 0;

                var length = request.Body.CanSeek ? request.Body.Length : (contentLength ?? 0);
                var truncated = length > MaxLoggedBodyBytes;
                var toRead = (int)Math.Min(length, MaxLoggedBodyBytes);

                if (toRead <= 0)
                    return string.Empty;

                var buffer = new byte[toRead];
                var readTotal = 0;
                while (readTotal < toRead)
                {
                    var read = await request.Body.ReadAsync(buffer.AsMemory(readTotal, toRead - readTotal));
                    if (read == 0)
                        break;
                    readTotal += read;
                }

                var bodyText = Encoding.UTF8.GetString(buffer, 0, readTotal);
                if (truncated)
                    bodyText += " ...(truncated)";
                return bodyText;
            }
            finally
            {
                request.Body.Position = 0;
            }
        }

        private static async Task<string> ReadFormForLoggingAsync(HttpRequest request, string contentType)
        {
            if (!request.Body.CanSeek)
                request.EnableBuffering();

            try
            {
                request.Body.Position = 0;

                var form = await request.ReadFormAsync();
                var log = new JsonObject
                {
                    ["contentType"] = contentType,
                    ["fields"] = CreateFormFieldsJson(form)
                };

                if (form.Files.Count > 0)
                {
                    log["files"] = CreateFormFilesJson(form);
                }

                return log.ToJsonString();
            }
            catch (InvalidDataException ex)
            {
                return $"[skipped: invalid form data ({ex.Message})]";
            }
            catch (IOException ex)
            {
                return $"[skipped: failed to read form data ({ex.Message})]";
            }
            finally
            {
                request.Body.Position = 0;
            }
        }

        private static JsonObject CreateFormFieldsJson(IFormCollection form)
        {
            var fields = new JsonObject();
            foreach (var field in form)
            {
                if (field.Value.Count == 1)
                {
                    fields[field.Key] = TruncateFormValue(field.Value[0]);
                    continue;
                }

                var values = new JsonArray();
                foreach (var value in field.Value)
                {
                    values.Add(TruncateFormValue(value));
                }
                fields[field.Key] = values;
            }

            return fields;
        }

        private static JsonArray CreateFormFilesJson(IFormCollection form)
        {
            var files = new JsonArray();
            foreach (var file in form.Files)
            {
                files.Add(new JsonObject
                {
                    ["name"] = file.Name,
                    ["fileName"] = file.FileName,
                    ["contentType"] = file.ContentType,
                    ["length"] = file.Length
                });
            }

            return files;
        }

        private static string? TruncateFormValue(string? value)
        {
            if (value == null || value.Length <= MaxLoggedFormValueLength)
                return value;

            return value[..MaxLoggedFormValueLength] + " ...(truncated)";
        }

        private static bool ShouldSkipBodyLoggingByPath(string requestPath)
        {
            if (string.IsNullOrEmpty(requestPath))
                return false;

            // 文件上传/导入：不要读取请求体（可能很大/多部分表单）
            if (requestPath.Contains("/api/File/Save", StringComparison.OrdinalIgnoreCase) ||
                requestPath.Contains("/api/File/Saves", StringComparison.OrdinalIgnoreCase) ||
                requestPath.Contains("/api/", StringComparison.OrdinalIgnoreCase) && requestPath.EndsWith("/import", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool ShouldSkipResponseLoggingByPath(string requestPath)
        {
            if (string.IsNullOrEmpty(requestPath))
                return false;

            // 文件下载/导出：通常是二进制大响应，不做响应体日志捕获
            if (requestPath.Contains("/api/File/Download", StringComparison.OrdinalIgnoreCase) ||
                requestPath.EndsWith("/export", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static string RedactSensitiveContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            // 只在看起来像 JSON 时做字段脱敏
            var trimmed = content.AsSpan().TrimStart();
            if (trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '['))
            {
                content = SensitiveJsonStringFieldRegex.Replace(content, "$1***$3");
            }

            // 兜底：把明显的 JWT 形态也打码（避免 token 泄漏到日志库）
            content = JwtLikeRegex.Replace(content, "***.***.***");
            return content;
        }

        private void LogRequestAsync(string? userAccount, string requestPath, string requestMethod,
            string clientIp, string requestBody, string responseContent, DateTime startTime, HttpContext context)
        {
            // 使用日志队列异步写入，不阻塞请求流程
            var logEntry = new SysActionLog
            {
                ItemId = YitterHelper.NewId(),  // 生成唯一 ID
                LogDate = DateTime.Now,
                UserAcc = userAccount,
                ActionName = requestPath, // 接口名称就是请求路径
                ClientIP = clientIp,
                Param = requestBody, // 请求参数
                RequestType = requestMethod, // 请求方法类型
                Result = responseContent // 响应结果
            };

            // 将日志添加到队列，由后台服务批量写入
            logQueueService.Enqueue(logEntry);
        }

        private sealed class TeeStream : Stream
        {
            private readonly Stream _inner;
            private readonly Stream _capture;
            private readonly int _maxCaptureBytes;
            private long _capturedBytes;

            public TeeStream(Stream inner, Stream capture, int maxCaptureBytes)
            {
                _inner = inner;
                _capture = capture;
                _maxCaptureBytes = maxCaptureBytes;
            }

            public bool IsTruncated { get; private set; }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => _inner.Flush();
            public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => _inner.SetLength(value);

            public override void Write(byte[] buffer, int offset, int count)
            {
                _inner.Write(buffer, offset, count);
                Capture(buffer.AsSpan(offset, count));
            }

            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                await _inner.WriteAsync(buffer, cancellationToken);
                Capture(buffer.Span);
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
                Capture(buffer.AsSpan(offset, count));
            }

            private void Capture(ReadOnlySpan<byte> bytes)
            {
                if (IsTruncated || _maxCaptureBytes <= 0)
                    return;

                var remaining = _maxCaptureBytes - (int)_capturedBytes;
                if (remaining <= 0)
                {
                    IsTruncated = true;
                    return;
                }

                var toWrite = Math.Min(remaining, bytes.Length);
                _capture.Write(bytes[..toWrite]);
                _capturedBytes += toWrite;
                if (toWrite < bytes.Length)
                    IsTruncated = true;
            }
        }
    }

    // 扩展方法用于注册中间件
    public static class WebApiMiddlewareExtensions
    {
        public static void UseWebApiMiddleware(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<WebApiMiddleware>();
        }
    }
}
