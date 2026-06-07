namespace Skemex.Application.Configuration;

public sealed class SuperAdminOptions
{
    public const string SectionName = "SuperAdmin";

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool MatchesEmail(string? email) =>
        !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(email)
        && string.Equals(email.Trim(), Email.Trim(), StringComparison.OrdinalIgnoreCase);
}
