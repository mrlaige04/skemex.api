using System.Text.RegularExpressions;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.CreateProjectColumn;

public sealed partial class CreateProjectColumnCommand : ICommand<ProjectColumnDto>
{
    public Guid ProjectId { get; init; }
    public Guid? TenantColumnId { get; init; }
    public string? Key { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }

    internal static string NormalizeKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        normalized = normalized.Replace(' ', '-');
        normalized = KeySanitizerRegex().Replace(normalized, string.Empty);
        normalized = DuplicateHyphenRegex().Replace(normalized, "-").Trim('-');
        return normalized;
    }

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex KeySanitizerRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex DuplicateHyphenRegex();
}
