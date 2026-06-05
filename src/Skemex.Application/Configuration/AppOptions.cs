namespace Skemex.Application.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public UI base URL for links in emails (e.g. invitation accept).</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
}
