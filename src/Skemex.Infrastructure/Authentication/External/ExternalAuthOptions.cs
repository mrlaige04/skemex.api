namespace Skemex.Infrastructure.Authentication.External;

public sealed class ExternalAuthOptions
{
    public const string SectionName = "Auth:External";

    /// <summary>Public API origin used for OAuth redirect_uri (no trailing slash).</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;

    public ExternalProviderOptions Google { get; set; } = new();
    public ExternalProviderOptions GitHub { get; set; } = new();
}

public sealed class ExternalProviderOptions
{
    public bool Enabled { get; set; } = true;
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);
}
