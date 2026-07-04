using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DTSoftServerApp.Services;

public sealed class AuthEncryptionService : IDisposable
{
    private const int KeySize = 2048;
    private const string Algorithm = "RSA-OAEP-256";
    private const int MaxPooledRsaCount = 32;
    private readonly byte[] _privateKey;
    private readonly byte[] _publicKey;
    private readonly string _keyId;
    private readonly ConcurrentBag<RSA> _rsaPool = [];
    private int _pooledRsaCount;

    public AuthEncryptionService()
    {
        using var rsa = RSA.Create(KeySize);
        _privateKey = rsa.ExportPkcs8PrivateKey();
        _publicKey = rsa.ExportSubjectPublicKeyInfo();
        _keyId = CreateKeyId(_publicKey);
    }

    public AuthEncryptionPublicKey GetPublicKey()
    {
        return new AuthEncryptionPublicKey
        {
            KeyId = _keyId,
            Algorithm = Algorithm,
            PublicKey = Convert.ToBase64String(_publicKey),
            PublicKeyPem = ToPem("PUBLIC KEY", _publicKey)
        };
    }

    public (string Username, string Password) DecryptCredentialsRequired(
        string keyId,
        string encryptedUsername,
        string encryptedPassword)
    {
        if (string.IsNullOrWhiteSpace(keyId) ||
            string.IsNullOrWhiteSpace(encryptedUsername) ||
            string.IsNullOrWhiteSpace(encryptedPassword))
        {
            throw new AuthEncryptionException("登录加密参数不能为空");
        }

        ValidateCurrentKey(keyId);
        var usernameBytes = DecodeCipherText(encryptedUsername, "Username");
        var passwordBytes = DecodeCipherText(encryptedPassword, "Password");

        var rsa = RentRsa();
        try
        {
            return (
                DecryptText(rsa, usernameBytes, "Username"),
                DecryptText(rsa, passwordBytes, "Password"));
        }
        finally
        {
            ReturnRsa(rsa);
        }
    }

    public void Dispose()
    {
        while (_rsaPool.TryTake(out var rsa))
        {
            rsa.Dispose();
        }
    }

    private RSA RentRsa()
    {
        if (_rsaPool.TryTake(out var rsa))
        {
            Interlocked.Decrement(ref _pooledRsaCount);
            return rsa;
        }

        rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(_privateKey, out _);
        return rsa;
    }

    private void ReturnRsa(RSA rsa)
    {
        var newCount = Interlocked.Increment(ref _pooledRsaCount);
        if (newCount <= MaxPooledRsaCount)
        {
            _rsaPool.Add(rsa);
            return;
        }

        Interlocked.Decrement(ref _pooledRsaCount);
        rsa.Dispose();
    }

    private void ValidateCurrentKey(string keyId)
    {
        if (!IsCurrentKey(keyId))
        {
            throw new AuthEncryptionException("登录加密公钥已失效，请刷新页面后重试");
        }
    }

    private static byte[] DecodeCipherText(string encryptedText, string fieldName)
    {
        try
        {
            return Convert.FromBase64String(encryptedText);
        }
        catch (FormatException ex)
        {
            throw new AuthEncryptionException($"{fieldName} 加密内容格式错误", ex);
        }
    }

    private static string DecryptText(RSA rsa, byte[] cipherBytes, string fieldName)
    {
        try
        {
            var plainBytes = rsa.Decrypt(cipherBytes, RSAEncryptionPadding.OaepSHA256);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            throw new AuthEncryptionException($"{fieldName} 解密失败", ex);
        }
    }

    private bool IsCurrentKey(string keyId)
    {
        var current = Encoding.ASCII.GetBytes(_keyId);
        var input = Encoding.ASCII.GetBytes(keyId);
        return current.Length == input.Length && CryptographicOperations.FixedTimeEquals(current, input);
    }

    private static string CreateKeyId(byte[] publicKey)
    {
        var hash = SHA256.HashData(publicKey);
        return Convert.ToBase64String(hash[..16])
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string ToPem(string label, byte[] der)
    {
        var base64 = Convert.ToBase64String(der);
        var builder = new StringBuilder();
        builder.AppendLine($"-----BEGIN {label}-----");

        for (var i = 0; i < base64.Length; i += 64)
        {
            builder.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }

        builder.AppendLine($"-----END {label}-----");
        return builder.ToString();
    }
}

public sealed class AuthEncryptionPublicKey
{
    public string KeyId { get; init; } = string.Empty;

    public string Algorithm { get; init; } = string.Empty;

    public string PublicKey { get; init; } = string.Empty;

    public string PublicKeyPem { get; init; } = string.Empty;
}

public sealed class AuthEncryptionException : Exception
{
    public AuthEncryptionException(string message) : base(message)
    {
    }

    public AuthEncryptionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
