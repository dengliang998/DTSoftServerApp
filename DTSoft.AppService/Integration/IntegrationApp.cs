using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Core.Interfaces;
using DTSoft.AppService.Localization;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Integration;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace DTSoft.AppService.Integration;

/// <summary>
/// 集成API管理服务
/// </summary>
public class IntegrationApp(SysDbContext dbContext, IDtSoftCache dtSoftCache, IAppLocalizer localizer)
{
    private static string ApiKeyCacheKey(string keyName) => $"ApiKey:{keyName.Trim().ToLowerInvariant()}";
    private string L(string key, params object[] args) => args.Length == 0 ? localizer[key] : localizer.Format(key, args);

    /// <summary>
    /// 生成安全的随机密钥
    /// </summary>
    /// <returns>64位随机密钥</returns>
    public string GenerateSecureKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = new byte[64];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
            var result = new StringBuilder(64);
            foreach (var b in bytes)
            {
                result.Append(chars[b % chars.Length]);
            }
            return result.ToString();
        }
    }
    
    /// <summary>
    /// 对密钥进行SHA256哈希
    /// </summary>
    /// <param name="secretKey">明文密钥</param>
    /// <returns>哈希值</returns>
    public string HashSecretKey(string secretKey)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(secretKey);
        var hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "");
    }
    
    /// <summary>
    /// 创建API密钥
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <param name="createdBy">创建人</param>
    /// <returns>API密钥响应（包含明文密钥）</returns>
    public async Task<(bool success, string message, ApiKeyResponse? data)> CreateApiKey(ApiKeyCreateRequest request, string createdBy)
    {
        try
        {
            // 检查KeyName是否已存在
            var exists = await dbContext.SysApiKey!.AnyAsync(x => x.KeyName == request.KeyName);
            if (exists)
            {
                return (false, $"{L("integration.keyName")} {L("common.exists")}", null);
            }
            
            // 生成密钥
            var secretKey = GenerateSecureKey();
            var hashedKey = HashSecretKey(secretKey);
            
            var apiKey = new SysApiKey
            {
                ItemId = YitterHelper.NewId(),
                KeyName = request.KeyName,
                SecretKey = hashedKey,
                Description = request.Description,
                Enabled = true,
                CreateTime = DateTime.Now,
                CreatedBy = createdBy,
                ExpireTime = request.ExpireTime
            };
            
            dbContext.SysApiKey!.Add(apiKey);
            await dbContext.SaveChangesAsync();

            dtSoftCache.RefreshCache(ApiKeyCacheKey(apiKey.KeyName));
            
            // 返回响应（包含明文密钥，仅在创建时返回一次）
            var response = new ApiKeyResponse
            {
                ItemId = apiKey.ItemId,
                KeyName = apiKey.KeyName,
                SecretKey = secretKey,  // 明文密钥
                Description = apiKey.Description,
                Enabled = apiKey.Enabled,
                CreateTime = apiKey.CreateTime,
                CreatedBy = apiKey.CreatedBy,
                ExpireTime = apiKey.ExpireTime
            };
            
            return (true, L("common.createSuccess"), response);
        }
        catch (Exception ex)
        {
            return (false, $"{L("common.createFailed")}: {ex.Message}", null);
        }
    }
    
    /// <summary>
    /// 更新API密钥
    /// </summary>
    /// <param name="request">更新请求</param>
    /// <returns>结果</returns>
    public async Task<(bool success, string message)> UpdateApiKey(ApiKeyUpdateRequest request)
    {
        try
        {
            var apiKey = await dbContext.SysApiKey!.FirstOrDefaultAsync(x => x.ItemId == request.ItemId);
            if (apiKey == null)
            {
                return (false, L("integration.apiKeyNotFound"));
            }
            
            apiKey.Description = request.Description;
            apiKey.Enabled = request.Enabled;
            apiKey.ExpireTime = request.ExpireTime;
            
            await dbContext.SaveChangesAsync();

            dtSoftCache.RefreshCache(ApiKeyCacheKey(apiKey.KeyName));
            
            return (true, L("common.updateSuccess"));
        }
        catch (Exception ex)
        {
            return (false, $"{L("common.updateFailed")}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 删除API密钥
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <returns>结果</returns>
    public async Task<(bool success, string message)> DeleteApiKey(ApiKeyDeleteRequest request)
    {
        try
        {
            var apiKey = await dbContext.SysApiKey!.FirstOrDefaultAsync(x => x.ItemId == request.ItemId);
            if (apiKey == null)
            {
                return (false, L("integration.apiKeyNotFound"));
            }
            
            var keyName = apiKey.KeyName;
            dbContext.SysApiKey!.Remove(apiKey);
            await dbContext.SaveChangesAsync();

            dtSoftCache.RefreshCache(ApiKeyCacheKey(keyName));
            
            return (true, L("common.deleteSuccess"));
        }
        catch (Exception ex)
        {
            return (false, $"{L("common.deleteFailed")}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 查询API密钥列表
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <returns>API密钥列表（不包含明文密钥）</returns>
    public async Task<List<ApiKeyResponse>> GetApiKeyList(ApiKeyQueryRequest request)
    {
        var query = dbContext.SysApiKey!.AsQueryable();
        
        if (!string.IsNullOrWhiteSpace(request.KeyName))
        {
            query = query.Where(x => x.KeyName.Contains(request.KeyName));
        }
        
        if (request.Enabled.HasValue)
        {
            query = query.Where(x => x.Enabled == request.Enabled.Value);
        }
        
        var list = await query
            .OrderByDescending(x => x.CreateTime)
            .Select(x => new ApiKeyResponse
            {
                ItemId = x.ItemId,
                KeyName = x.KeyName,
                SecretKey = null,  // 不返回密钥
                Description = x.Description,
                Enabled = x.Enabled,
                CreateTime = x.CreateTime,
                CreatedBy = x.CreatedBy,
                ExpireTime = x.ExpireTime
            })
            .ToListAsync();
        
        return list;
    }
    
    /// <summary>
    /// 验证API密钥
    /// </summary>
    /// <param name="keyName">密钥名称</param>
    /// <param name="secretKey">密钥</param>
    /// <returns>验证结果</returns>
    public async Task<(bool success, string message, SysApiKey? apiKey)> ValidateApiKey(string keyName, string secretKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyName))
                return (false, $"{L("integration.keyName")} {L("common.required")}", null);

            // 查询密钥（缓存优先）
            SysApiKey? apiKey = null;
            var cacheKey = ApiKeyCacheKey(keyName);
            var cachedJson = await dtSoftCache.GetAsync<string>(cacheKey);
            if (!string.IsNullOrWhiteSpace(cachedJson))
            {
                try
                {
                    apiKey = JsonSerializer.Deserialize<SysApiKey>(cachedJson);
                }
                catch
                {
                    apiKey = null;
                }
            }

            if (apiKey == null)
            {
                apiKey = await dbContext.SysApiKey!
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.KeyName == keyName);
                if (apiKey != null)
                {
                    await dtSoftCache.SetAsync(cacheKey, JsonSerializer.Serialize(apiKey), TimeSpan.FromMinutes(1));
                }
            }

            if (apiKey == null)
            {
                return (false, L("integration.keyNameNotFound"), null);
            }
            
            // 检查是否启用
            if (!apiKey.Enabled)
            {
                return (false, L("integration.apiKeyDisabled"), null);
            }
            
            // 检查是否过期
            if (apiKey.ExpireTime.HasValue && apiKey.ExpireTime.Value < DateTime.Now)
            {
                return (false, L("integration.apiKeyExpired"), null);
            }
            
            // 验证密钥
            var hashedKey = HashSecretKey(secretKey);
            if (apiKey.SecretKey != hashedKey)
            {
                return (false, L("integration.secretKeyInvalid"), null);
            }
            
            return (true, L("integration.validateSuccess"), apiKey);
        }
        catch (Exception ex)
        {
            return (false, $"{L("common.operationFailed")}: {ex.Message}", null);
        }
    }
}
