using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace DTSoft.Core.Licensing;

public sealed class LicenseService
{
    private const string LicenseFileName = "license.lic";
    private const string ProductName = "DTSoft";
    private const string LicenseCipherPrefix = "DTLIC1:";
    private const string EncryptionKeyBase64 = "IL0TMRPqoWaVLBRQAPgG9thDWZFmSxpPdDs5cpaCKpk=";

    // The matching private key must only be kept by the license issuer.
    private const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAr6IyT7BBFBODzzhtjXCv
        Godi64hG+UpubLLG/uDvsYtnjrZwlLOLZ/sNH6l4xDiNc9m3pwWPOIiDhGAkDIbb
        +A77CyafOWGKc0H2J1j7Ke1UTupSNtRjAEsmGW/djtDqEf44gpQ+2zeUJG1rymgK
        fxNZMyXp+/KjDWwrTSDJupDc6yTN6wk+Qb95CEO0YojT2+kDuwPOwaz7GVDH6+t6
        NfOqxkrJ2SvsEEMnHO+KAAUbUHb+/xWlGP2hvnXszIWJODpGGpsRdlCujE2yPXox
        X49B6KpliZ+hhSSRm63NUKpGTokHekTEG3K2yQt+vw6phZ/osTsAbQVGQM7GW1Mn
        xwIDAQAB
        -----END PUBLIC KEY-----
        """;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public LicenseDocument Current { get; private set; } = new();
    public bool IsValid { get; private set; }
    public string? ErrorMessage { get; private set; }
    private static string L(string key, params object[] args)
    {
        var english = CultureInfo.CurrentUICulture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var text = key switch
        {
            "license.fileNotFound" => english ? "License file does not exist." : "许可证文件不存在。",
            "license.fileInvalid" => english ? "License file is invalid." : "许可文件无效。",
            "license.invalid" => english ? "License is invalid." : "许可证无效。",
            "license.unauthorized" => english ? "License error: {0}" : "授权异常：{0}",
            "license.expired" => english ? "License has expired." : "许可证已过期。",
            "license.idRequired" => english ? "License ID is required." : "许可证编号不能为空。",
            "license.productMismatch" => english ? "License product does not match." : "许可证产品不匹配。",
            "license.typeRequired" => english ? "License authorization type is required." : "许可证授权类型不能为空。",
            "license.signatureRequired" => english ? "License signature is required." : "许可证签名不能为空。",
            "license.signatureInvalid" => english ? "License signature verification failed." : "许可证签名验证失败。",
            "license.temporaryTypeInvalid" => english ? "Temporary licenses cannot contain MAC address or concurrent user authorization." : "临时许可证不能包含 MAC 地址授权或并发用户数授权。",
            "license.temporaryExpireRequired" => english ? "Temporary license is missing an expiration time." : "临时许可证缺少过期时间。",
            "license.officialMacRequired" => english ? "Official license is missing MAC address authorization." : "正式许可证缺少 MAC 地址授权。",
            "license.officialConcurrentRequired" => english ? "Official license is missing concurrent user authorization." : "正式许可证缺少并发用户数授权。",
            "license.macRequired" => english ? "MAC address authorization is missing authorized addresses." : "MAC 地址授权缺少授权地址。",
            "license.macNotAllowed" => english ? "This machine's MAC address is not within the licensed range." : "本机 MAC 地址不在许可证授权范围内。",
            "license.concurrentInvalid" => english ? "Concurrent user authorization count must be -1 or an integer greater than 0." : "并发用户授权数量必须是 -1 或大于 0 的整数。",
            _ => key
        };

        return args.Length == 0 ? text : string.Format(CultureInfo.CurrentUICulture, text, args);
    }

    public string LicensePath => Path.Combine(AppContext.BaseDirectory, LicenseFileName);

    public void ValidateOnStartup()
    {
        try
        {
            if (!File.Exists(LicensePath))
                throw new FileNotFoundException(L("license.fileNotFound"), LicensePath);

            var json = ReadEncryptedLicenseJson(LicensePath);
            var document = JsonSerializer.Deserialize<LicenseDocument>(json, SerializerOptions)
                ?? throw new InvalidOperationException(L("license.fileInvalid"));

            ValidateDocument(document);
            Current = document;
            IsValid = true;
            ErrorMessage = null;
        }
        catch (Exception exception) when (exception is FileNotFoundException or JsonException or InvalidOperationException)
        {
            Current = new LicenseDocument();
            IsValid = false;
            ErrorMessage = exception.Message;
        }
    }

    private string ReadEncryptedLicenseJson(string licensePath)
    {
        var content = File.ReadAllText(licensePath, Encoding.UTF8).Trim();
        if (!content.StartsWith(LicenseCipherPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException(L("license.fileInvalid"));

        try
        {
            var encryptedBytes = Convert.FromBase64String(content[LicenseCipherPrefix.Length..]);
            if (encryptedBytes.Length <= 28)
                throw new InvalidOperationException(L("license.fileInvalid"));

            var nonce = encryptedBytes[..12];
            var tag = encryptedBytes[12..28];
            var cipherText = encryptedBytes[28..];
            var plainText = new byte[cipherText.Length];

            using var aes = new AesGcm(Convert.FromBase64String(EncryptionKeyBase64), 16);
            aes.Decrypt(nonce, cipherText, tag, plainText);
            return Encoding.UTF8.GetString(plainText);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            throw new InvalidOperationException(L("license.fileInvalid"), exception);
        }
    }

    public bool HasConcurrentUserLimit =>
        IsValid &&
        !Current.HasType(LicenseType.Temporary) &&
        Current.HasType(LicenseType.ConcurrentUser) &&
        Current.MaxConcurrentUsers is > 0;

    public void EnsureCurrentLicenseUsable()
    {
        if (!IsValid)
            throw new InvalidOperationException(L("license.unauthorized", ErrorMessage ?? L("license.invalid")));

        if (Current.ExpireAt.HasValue &&
            DateTimeOffset.UtcNow > Current.ExpireAt.Value.ToUniversalTime())
        {
            throw new InvalidOperationException(L("license.expired"));
        }
    }

    private void ValidateDocument(LicenseDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.LicenseId))
            throw new InvalidOperationException(L("license.idRequired"));

        if (!string.Equals(document.Product, ProductName, StringComparison.Ordinal))
            throw new InvalidOperationException(L("license.productMismatch"));

        if (document.LicenseTypes.Count == 0)
            throw new InvalidOperationException(L("license.typeRequired"));

        if (string.IsNullOrWhiteSpace(document.Signature))
            throw new InvalidOperationException(L("license.signatureRequired"));

        if (!VerifySignature(document))
            throw new InvalidOperationException(L("license.signatureInvalid"));

        if (document.HasType(LicenseType.Temporary))
        {
            ValidateTemporaryLicense(document);
            return;
        }

        ValidateOfficialLicense(document);
    }

    private void ValidateTemporaryLicense(LicenseDocument document)
    {
        if (document.LicenseTypes.Count != 1)
            throw new InvalidOperationException(L("license.temporaryTypeInvalid"));

        if (!document.ExpireAt.HasValue)
            throw new InvalidOperationException(L("license.temporaryExpireRequired"));

        if (document.ExpireAt.HasValue &&
            DateTimeOffset.UtcNow > document.ExpireAt.Value.ToUniversalTime())
        {
            throw new InvalidOperationException(L("license.expired"));
        }
    }

    private void ValidateOfficialLicense(LicenseDocument document)
    {
        if (!document.HasType(LicenseType.MacAddress))
            throw new InvalidOperationException(L("license.officialMacRequired"));

        if (!document.HasType(LicenseType.ConcurrentUser))
            throw new InvalidOperationException(L("license.officialConcurrentRequired"));

        if (document.ExpireAt.HasValue &&
            DateTimeOffset.UtcNow > document.ExpireAt.Value.ToUniversalTime())
        {
            throw new InvalidOperationException(L("license.expired"));
        }

        if (document.HasType(LicenseType.MacAddress))
        {
            if (document.MacAddresses.Count == 0)
                throw new InvalidOperationException(L("license.macRequired"));

            var authorizedMacs = document.MacAddresses
                .Select(NormalizeMacAddress)
                .Where(mac => mac.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var localMacs = GetLocalMacAddresses();

            if (!localMacs.Any(authorizedMacs.Contains))
                throw new InvalidOperationException(L("license.macNotAllowed"));
        }

        if (document.HasType(LicenseType.ConcurrentUser) &&
            (!document.MaxConcurrentUsers.HasValue ||
             document.MaxConcurrentUsers == 0 ||
             document.MaxConcurrentUsers < -1))
        {
            throw new InvalidOperationException(L("license.concurrentInvalid"));
        }
    }

    private static bool VerifySignature(LicenseDocument document)
    {
        try
        {
            var signature = Convert.FromBase64String(document.Signature);
            var payload = JsonSerializer.Serialize(new LicenseSignaturePayload(document), SerializerOptions);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKeyPem);
            return rsa.VerifyData(
                Encoding.UTF8.GetBytes(payload),
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static HashSet<string> GetLocalMacAddresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .Select(networkInterface => NormalizeMacAddress(
                networkInterface.GetPhysicalAddress().ToString()))
            .Where(mac => mac.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeMacAddress(string? macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            return string.Empty;

        return new string(macAddress
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    private sealed class LicenseSignaturePayload
    {
        public string LicenseId { get; }
        public string Product { get; }
        public string Customer { get; }
        public List<LicenseType> LicenseTypes { get; }
        public DateTimeOffset? ExpireAt { get; }
        public List<string> MacAddresses { get; }
        public int? MaxConcurrentUsers { get; }
        public DateTimeOffset IssuedAt { get; }

        public LicenseSignaturePayload(LicenseDocument document)
        {
            LicenseId = document.LicenseId;
            Product = document.Product;
            Customer = document.Customer;
            LicenseTypes = document.LicenseTypes;
            ExpireAt = document.ExpireAt;
            MacAddresses = document.MacAddresses;
            MaxConcurrentUsers = document.MaxConcurrentUsers;
            IssuedAt = document.IssuedAt;
        }
    }
}
