using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using DTSoftServerApp.Services;

namespace DTSoftServerApp.Controllers.Auth
{
    /// <summary>
    ///授权认证接口
    /// </summary>
    [ApiController]
    [Tags("授权认证")]
    [Route("api/[controller]")]
    public class AuthController(JwtService jwtService, SysDbContext dbContext, UserCacheHelper userCacheHelper) : ControllerBase
    {
        /// <summary>
        ///登录接口
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 记录登录日志
            var log = new SysActionLog
            {
                ItemId = YitterHelper.NewId(),  // 生成唯一 ID
                LogDate = DateTime.Now.ToCstTime(),
                UserAcc = request.Username,
                ActionName = "Login",
                ClientIP = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Param = "",
                RequestType = "API-Login"
            };

            var user = await ValidateUser(request.Username, request.Password);
            if (user != null)
            {
                var (token, expires) = jwtService.GenerateToken(request.Username, user.Account!); // 用户ID来自数据库

                log.Result = "登录成功";
                dbContext.SysActionLog!.Add(log);
                await dbContext.SaveChangesAsync();

                return Ok(new {
                    Code = 200,
                    Message = "登录成功",
                    Data = new {
                        Token = token,
                        Expires = expires,
                        jwtService.ExpiresInHours,
                        jwtService.ExpiresInSeconds
                    }
                });
            }
            else
            {
                log.Result = "用户名或密码错误";
                dbContext.SysActionLog!.Add(log);
                await dbContext.SaveChangesAsync();

                return Unauthorized(new {
                    Code = 401,
                    Message = "用户名或密码错误"
                });
            }
        }

        private async Task<SysUser?> ValidateUser(string username, string password)
        {
            // 从缓存获取用户数据
            var user = await userCacheHelper.GetUserByAccountAsync(username);

            // 检查用户是否存在且密码正确
            if (user != null && user.PassWord == Encrypt.Encrypt_MD5(password) && !user.Disable)
            {
                return user;
            }

            return null;
        }
    }

    /// <summary>
    /// 登录参数
    /// </summary>
    public class LoginRequest(string username, string password)
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; } = username;

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; } = password;
    }
}
