namespace Skemex.Application.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
}
