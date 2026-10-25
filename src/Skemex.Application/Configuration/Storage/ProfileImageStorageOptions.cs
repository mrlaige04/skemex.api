namespace Skemex.Application.Configuration.Storage;

public sealed class ProfileImageStorageOptions
{
    public const string SectionName = "Storage:ProfileImages";

    public string Bucket { get; set; } = string.Empty;
    public int PresignedDownloadExpirySeconds { get; set; }
}
