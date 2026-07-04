using DTSoft.Core.Common;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DTSoftServerApp.Services
{
    /// <summary>
    /// Jwt 服务
    /// </summary>
    public class JwtService(IConfiguration configuration)
    {
        private readonly int _expiresInHours = 8;

        /// <summary>
        /// 生成 Token
        /// </summary>
        /// <param name="username"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public (string token, DateTime expires) GenerateToken(string username, string userId)
        {
            var expires = DateTime.Now.AddHours(_expiresInHours);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var signingKey = configuration[AppConfigurationKeys.Authentication.Jwt.SigningKey]
                ?? configuration[AppConfigurationKeys.Authentication.Jwt.LegacySigningKey]
                ?? throw new InvalidOperationException("JWT 签名密钥未配置，请配置 Authentication:Jwt:SigningKey。");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: configuration[AppConfigurationKeys.Authentication.Jwt.Issuer]
                    ?? configuration[AppConfigurationKeys.Authentication.Jwt.LegacyIssuer],
                audience: configuration[AppConfigurationKeys.Authentication.Jwt.Audience]
                    ?? configuration[AppConfigurationKeys.Authentication.Jwt.LegacyAudience],
                claims: claims,
                expires: expires,
                signingCredentials: creds);

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }

        /// <summary>
        /// 获取 Token 过期时间（小时）
        /// </summary>
        public int ExpiresInHours => _expiresInHours;

        /// <summary>
        /// 获取 Token 过期时间（秒）
        /// </summary>
        public int ExpiresInSeconds => _expiresInHours * 3600;
    }
}
