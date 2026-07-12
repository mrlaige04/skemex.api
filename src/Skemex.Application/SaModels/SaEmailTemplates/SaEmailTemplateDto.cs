namespace Skemex.Application.SaModels.SaEmailTemplates;

public class SaEmailTemplateSummaryDto
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public bool IsSystem { get; init; }

    public Guid? TenantId { get; init; }

    public string? TenantName { get; init; }

    public string Scope { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}

public sealed class SaEmailTemplateDto : SaEmailTemplateSummaryDto
{
    public string Body { get; init; } = string.Empty;
}
