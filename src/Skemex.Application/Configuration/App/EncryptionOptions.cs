namespace Skemex.Application.Configuration.App;

/// <summary>
/// Master key for encrypting secrets at rest (AI provider API keys, auth values).
/// Set via env <c>Encryption__MasterKey</c> (e.g. from GitHub Actions secrets) — not the filesystem.
/// Value must be Base64 of exactly 32 bytes (AES-256).
/// </summary>
public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";

    /// <summary>Base64-encoded 32-byte AES-256 key.</summary>
    public string MasterKey { get; set; } = string.Empty;
}
