using DTSoft.AppService.ApiKey;
using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.ApiKey;
using DTSoftServerApp.Services;
using System.Security.Claims;

namespace DTSoftServerApp.Controllers.Auth
{
    /// <summary>
    /// API密钥认证接口
    /// </summary>
    [ApiController]
    [Tags("API密钥认证")]
    [Route("api/[controller]")]
    public class ApiKeyAuthController(
        JwtService jwtService,
        SysDbContext dbContext,
        UserCacheHelper userCacheHelper,
        ApiKeyApp apiKeyApp) : ControllerBase
    {
        /// <summary>
        /// API密钥获取Token接口
        /// </summary>
        /// <param name="request">API密钥登录请求</param>
        /// <returns>Token信息</returns>
        [HttpPost("login")]
        public async Task<IActionResult> ApiKeyLogin([FromBody] ApiKeyLoginRequest request)
        {
            // 记录日志
            var log = new SysActionLog
            {
                ItemId = YitterHelper.NewId(),
                LogDate = DateTime.Now.ToCstTime(),
                UserAcc = request.UserAccount,
                ActionName = "ApiKeyLogin",
                ClientIP = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Param = $"KeyName: {request.KeyName}",
                RequestType = "API-ApiKeyLogin"
            };

            try
            {
                // 验证API密钥
                var (valid, message, apiKey) = await apiKeyApp.ValidateApiKey(request.KeyName, request.SecretKey);
                if (!valid)
                {
                    log.Result = message;
                    dbContext.SysActionLog!.Add(log);
                    await dbContext.SaveChangesAsync();

                    return Unauthorized(new
                    {
                        Code = 401,
                        Message = message
                    });
                }

                // 验证用户账号
                var user = await userCacheHelper.GetUserByAccountAsync(request.UserAccount);
                if (user == null || user.Disable)
                {
                    log.Result = "用户账号不存在或已禁用";
                    dbContext.SysActionLog!.Add(log);
                    await dbContext.SaveChangesAsync();

                    return Unauthorized(new
                    {
                        Code = 401,
                        Message = "用户账号不存在或已禁用"
                    });
                }

                // 生成Token
                var (token, expires) = jwtService.GenerateToken(request.UserAccount, user.Account!);

                log.Result = "获取Token成功";
                dbContext.SysActionLog!.Add(log);
                await dbContext.SaveChangesAsync();

                return Ok(new
                {
                    Code = 200,
                    Message = "获取Token成功",
                    Data = new
                    {
                        Token = token,
                        Expires = expires,
                        jwtService.ExpiresInHours,
                        jwtService.ExpiresInSeconds
                    }
                });
            }
            catch (Exception ex)
            {
                log.Result = $"获取Token失败: {ex.Message}";
                dbContext.SysActionLog!.Add(log);
                await dbContext.SaveChangesAsync();

                return StatusCode(500, new
                {
                    Code = 500,
                    Message = $"获取Token失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 创建API密钥（需要管理员权限）
        /// </summary>
        /// <param name="request">创建请求</param>
        /// <returns>API密钥信息（包含明文密钥）</returns>
        [HttpPost("create")]
        [Authorize]
        public async Task<IActionResult> CreateApiKey([FromBody] ApiKeyCreateRequest request)
        {
            // 从JWT中获取当前登录用户
            var currentUser = DtSoftHelper.GetLoginUserAccount(User);

            var (success, message, data) = await apiKeyApp.CreateApiKey(request, currentUser);

            if (!success)
            {
                return BadRequest(new
                {
                    Code = 400,
                    Message = message
                });
            }

            return Ok(new
            {
                Code = 200,
                Message = message,
                Data = data
            });
        }

        /// <summary>
        /// 更新API密钥（需要管理员权限）
        /// </summary>
        /// <param name="request">更新请求</param>
        /// <returns>结果</returns>
        [HttpPut("update")]
        [Authorize]
        public async Task<IActionResult> UpdateApiKey([FromBody] ApiKeyUpdateRequest request)
        {
            var (success, message) = await apiKeyApp.UpdateApiKey(request);

            if (!success)
            {
                return BadRequest(new
                {
                    Code = 400,
                    Message = message
                });
            }

            return Ok(new
            {
                Code = 200,
                Message = message
            });
        }

        /// <summary>
        /// 删除API密钥（需要管理员权限）
        /// </summary>
        /// <param name="request">删除请求</param>
        /// <returns>结果</returns>
        [HttpDelete("delete")]
        [Authorize]
        public async Task<IActionResult> DeleteApiKey([FromBody] ApiKeyDeleteRequest request)
        {
            var (success, message) = await apiKeyApp.DeleteApiKey(request);

            if (!success)
            {
                return BadRequest(new
                {
                    Code = 400,
                    Message = message
                });
            }

            return Ok(new
            {
                Code = 200,
                Message = message
            });
        }

        /// <summary>
        /// 查询API密钥列表（需要管理员权限）
        /// </summary>
        /// <param name="request">查询请求</param>
        /// <returns>API密钥列表</returns>
        [HttpPost("list")]
        [Authorize]
        public async Task<IActionResult> GetApiKeyList([FromBody] ApiKeyQueryRequest request)
        {
            var list = await apiKeyApp.GetApiKeyList(request);

            return Ok(new
            {
                Code = 200,
                Message = "查询成功",
                Data = list
            });
        }
    }
}
