using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaEmailTemplates;

namespace Skemex.Application.SaFeatures.Queries.SaEmailTemplates.GetSaEmailTemplateById;

public sealed class GetSaEmailTemplateByIdQuery : IQuery<SaEmailTemplateDto>
{
    public Guid TemplateId { get; init; }
}
