namespace Skemex.Application.Configuration.Integrations;

/// <summary>
/// Atlassian Jira Cloud OAuth 2.0 (3LO) app credentials.
/// Inject via env <c>Jira__ClientId</c> / <c>Jira__ClientSecret</c>
/// (GitHub secrets <c>JIRA_CLIENT_ID</c>, <c>JIRA_CLIENT_SECRET</c>).
/// </summary>
public sealed class JiraOptions
{
    public const string SectionName = "Jira";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Full callback URL registered in the Atlassian developer console,
    /// e.g. <c>https://api.example.com/api/integrations/jira/callback</c>.
    /// When empty, derived from <c>Auth:External:CallbackBaseUrl</c>.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
