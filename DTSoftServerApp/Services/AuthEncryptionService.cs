using System.Security.Cryptography;
using System.Text;

namespace DTSoftServerApp.Services;

public sealed class AuthEncryptionService
{
    private const int KeySize = 2048;
    private const string Algorithm = "RSA-OAEP-256";
    private readonly byte[] _privateKey;
    private readonly byte[] _publicKey;
    private readonly string _keyId;

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

    public string DecryptRequired(string keyId, string encryptedText, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(encryptedText))
        {
            throw new AuthEncryptionException($"{fieldName} 加密参数不能为空");
        }

        if (!IsCurrentKey(keyId))
        {
            throw new AuthEncryptionException("登录加密公钥已失效，请刷新页面后重试");
        }

        byte[] cipherBytes;
        try
        {
            cipherBytes = Convert.FromBase64String(encryptedText);
        }
        catch (FormatException ex)
        {
            throw new AuthEncryptionException($"{fieldName} 加密内容格式错误", ex);
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(_privateKey, out _);
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
