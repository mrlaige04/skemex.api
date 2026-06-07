using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Queries.GetSaEmailTemplateById;

public sealed class GetSaEmailTemplateByIdQuery : IQuery<SaEmailTemplateDto>
{
    public Guid TemplateId { get; init; }
}
