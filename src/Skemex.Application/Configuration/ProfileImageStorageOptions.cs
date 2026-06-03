namespace Skemex.Application.Configuration;

public sealed class ProfileImageStorageOptions
{
    public const string SectionName = "Storage:ProfileImages";

    /// <summary>MinIO bucket name (production) or local folder segment (e.g. branding).</summary>
    public string Bucket { get; set; } = string.Empty;
}
