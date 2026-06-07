using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Queries.GetSaEmailTemplates;

public sealed class GetSaEmailTemplatesQuery : IQuery<IReadOnlyList<SaEmailTemplateSummaryDto>>
{
    public string? Search { get; set; }
}
