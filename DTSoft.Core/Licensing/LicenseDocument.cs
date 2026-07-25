namespace DTSoft.Core.Licensing;

public sealed class LicenseDocument
{
    public string LicenseId { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public List<LicenseType> LicenseTypes { get; set; } = [];
    public DateTimeOffset? ExpireAt { get; set; }
    public List<string> MacAddresses { get; set; } = [];
    public int? MaxConcurrentUsers { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public string Signature { get; set; } = string.Empty;

    public bool HasType(LicenseType type) => LicenseTypes.Contains(type);
}
