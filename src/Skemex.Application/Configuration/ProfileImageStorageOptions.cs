namespace Skemex.Application.Configuration;

public sealed class ProfileImageStorageOptions
{
    public const string SectionName = "Storage:ProfileImages";

    public string Bucket { get; set; } = string.Empty;
    public int PresignedDownloadExpirySeconds { get; set; }
}
