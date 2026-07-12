using System.Text.RegularExpressions;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.TenantColumns.CreateTenantColumn;

public sealed partial class CreateTenantColumnCommand : ICommand<TenantColumnDto>
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSortOrderForced { get; set; }

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
