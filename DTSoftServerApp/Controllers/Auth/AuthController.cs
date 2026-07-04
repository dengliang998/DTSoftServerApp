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
        CaptchaService captchaService,
        AuthEncryptionService authEncryptionService,
        ILogQueueService logQueueService) : ControllerBase
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
        /// 获取登录加密公钥
        /// </summary>
        /// <returns></returns>
        [HttpGet("login-encryption-key")]
        public IActionResult GetLoginEncryptionKey()
        {
            return Ok(new
            {
                Code = 200,
                Message = "获取登录加密公钥成功",
                Data = authEncryptionService.GetPublicKey()
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
                ActionName = "Login",
                ClientIP = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Param = "",
                RequestType = "API-Login"
            };

            var (captchaValid, captchaMessage) = await captchaService.ValidateAsync(request.CaptchaId, request.CaptchaCode);
            if (!captchaValid)
            {
                log.Result = captchaMessage;
                logQueueService.Enqueue(log);

                return BadRequest(new {
                    Code = 400,
                    Message = captchaMessage
                });
            }

            var (credentialsValid, username, password, credentialsMessage) = TryDecryptCredentials(request);
            if (!credentialsValid)
            {
                log.Result = credentialsMessage;
                logQueueService.Enqueue(log);

                return BadRequest(new
                {
                    Code = 400,
                    Message = credentialsMessage
                });
            }

            log.UserAcc = username;

            var user = await ValidateUser(username, password);
            if (user != null)
            {
                var (token, expires) = jwtService.GenerateToken(username, user.Account!); // 用户ID来自数据库

                log.Result = "登录成功";
                logQueueService.Enqueue(log);

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
                logQueueService.Enqueue(log);

                return Unauthorized(new {
                    Code = 401,
                    Message = "用户名或密码错误"
                });
            }
        }

        private (bool Success, string Username, string Password, string Message) TryDecryptCredentials(LoginRequest request)
        {
            try
            {
                var (decryptedUsername, password) = authEncryptionService.DecryptCredentialsRequired(
                    request.EncryptionKeyId,
                    request.Username,
                    request.Password);
                var username = decryptedUsername.Trim();

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    return (false, string.Empty, string.Empty, "用户名和密码不能为空");
                }

                return (true, username, password, string.Empty);
            }
            catch (AuthEncryptionException)
            {
                return (false, string.Empty, string.Empty, "登录参数解密失败，请刷新页面后重试");
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
        /// 用户名密文，使用 /api/Auth/login-encryption-key 返回的公钥加密
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 密码密文，使用 /api/Auth/login-encryption-key 返回的公钥加密
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 登录加密公钥 ID，通过 /api/Auth/login-encryption-key 获取
        /// </summary>
        public string EncryptionKeyId { get; set; } = string.Empty;

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
