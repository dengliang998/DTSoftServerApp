using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DTSoft.Core.Common;

public abstract class Encrypt
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DefaultIterations = 120_000;
    private const int MinIterations = 10_000;
    private const string PasswordHashAlgorithm = "PBKDF2";
    private const string PasswordHashPrf = "SHA256";
    private static readonly Regex Md5Regex = new("^[A-Fa-f0-9]{32}$", RegexOptions.Compiled);
    private static int _iterations = DefaultIterations;

    public enum PasswordVerificationResult
    {
        Failed,
        Success,
        SuccessRehashNeeded
    }

    public static int Iterations => Volatile.Read(ref _iterations);

    public static void ConfigurePasswordHashing(int? iterations)
    {
        if (!iterations.HasValue)
        {
            return;
        }

        Volatile.Write(ref _iterations, Math.Max(MinIterations, iterations.Value));
    }

    /// <summary>
    /// 使用 PBKDF2-HMAC-SHA256 生成带盐密码哈希。
    /// </summary>
    public static string HashPassword(string password)
    {
        var iterations = Iterations;
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);

        return string.Join('$',
            PasswordHashAlgorithm,
            PasswordHashPrf,
            iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    /// <summary>
    /// 校验密码。兼容旧 MD5 哈希，校验成功后调用方应重新保存为 HashPassword 的结果。
    /// </summary>
    public static PasswordVerificationResult VerifyPassword(string? passwordHash, string password)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return PasswordVerificationResult.Failed;
        }

        if (IsLegacyMd5Hash(passwordHash))
        {
            return FixedTimeEquals(ComputeMd5(password), passwordHash)
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Failed;
        }

        var parts = passwordHash.Split('$');
        if (parts.Length != 5 ||
            !parts[0].Equals(PasswordHashAlgorithm, StringComparison.OrdinalIgnoreCase) ||
            !parts[1].Equals(PasswordHashPrf, StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parts[2], out var iterations) ||
            iterations <= 0)
        {
            return PasswordVerificationResult.Failed;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[3]);
            byte[] expectedHash = Convert.FromBase64String(parts[4]);
            if (salt.Length != SaltSize || expectedHash.Length != HashSize)
            {
                return PasswordVerificationResult.Failed;
            }

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

            if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            {
                return PasswordVerificationResult.Failed;
            }

            return iterations != Iterations
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Success;
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }
        catch (ArgumentException)
        {
            return PasswordVerificationResult.Failed;
        }
    }

    public static bool IsLegacyMd5Hash(string passwordHash)
    {
        return Md5Regex.IsMatch(passwordHash);
    }

    /// <summary>
    /// MD5加密字符串（32位大写）
    /// </summary>
    /// <param name="source">源字符串</param>
    /// <returns>加密后的字符串</returns>
    [Obsolete("MD5 is not safe for password storage. Use HashPassword and VerifyPassword instead.")]
    public static string Encrypt_MD5(string source)
    {
        return ComputeMd5(source);
    }

    private static string ComputeMd5(string source)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(source);
        string result = BitConverter.ToString(MD5.HashData(bytes));
        return result.Replace("-", "");
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    }
}
