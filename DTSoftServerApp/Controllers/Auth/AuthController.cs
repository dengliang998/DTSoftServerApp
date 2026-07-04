using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.Models.Entities;
using DTSoftServerApp.Services;
using Microsoft.EntityFrameworkCore;

namespace DTSoftServerApp.Controllers.Auth
{
    /// <summary>
    ///授权认证接口
    /// </summary>
    [ApiController]
    [Tags("授权认证")]
    [Route("api/[controller]")]
    public class AuthController(
        JwtService jwtService,
        SysDbContext dbContext,
        UserCacheHelper userCacheHelper,
        CaptchaService captchaService) : ControllerBase
    {
        /// <summary>
        /// 获取登录验证码
        /// </summary>
        /// <returns></returns>
        [HttpGet("captcha")]
        public async Task<IActionResult> GetCaptcha()
        {
            return Ok(new
            {
                Code = 200,
                Message = "获取验证码成功",
                Data = await captchaService.CreateAsync()
            });
        }

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

            var (captchaValid, captchaMessage) = await captchaService.ValidateAsync(request.CaptchaId, request.CaptchaCode);
            if (!captchaValid)
            {
                log.Result = captchaMessage;
                dbContext.SysActionLog!.Add(log);
                await dbContext.SaveChangesAsync();

                return BadRequest(new {
                    Code = 400,
                    Message = captchaMessage
                });
            }

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

            if (user == null || user.Disable)
            {
                return null;
            }

            var verificationResult = Encrypt.VerifyPassword(user.PassWord, password);
            if (verificationResult == Encrypt.PasswordVerificationResult.Failed)
            {
                return null;
            }

            if (verificationResult == Encrypt.PasswordVerificationResult.SuccessRehashNeeded)
            {
                var dbUser = await dbContext.SysUser.FirstOrDefaultAsync(p => p.Account == user.Account);
                if (dbUser != null)
                {
                    dbUser.PassWord = Encrypt.HashPassword(password);
                    await dbContext.SaveChangesAsync();
                    await userCacheHelper.RefreshUserCacheAsync(user.Account);
                    user.PassWord = dbUser.PassWord;
                }
            }

            return user;
        }
    }

    /// <summary>
    /// 登录参数
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 验证码 ID，通过 /api/Auth/captcha 获取
        /// </summary>
        public string CaptchaId { get; set; } = string.Empty;

        /// <summary>
        /// 用户输入的验证码
        /// </summary>
        public string CaptchaCode { get; set; } = string.Empty;
    }
}
