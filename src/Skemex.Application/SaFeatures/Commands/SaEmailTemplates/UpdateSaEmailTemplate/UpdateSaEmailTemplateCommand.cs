using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaEmailTemplates;

namespace Skemex.Application.SaFeatures.Commands.SaEmailTemplates.UpdateSaEmailTemplate;

public sealed class UpdateSaEmailTemplateCommand : ICommand<SaEmailTemplateDto>
{
    public Guid TemplateId { get; set; }

    public string? Title { get; set; }

    public string? Subject { get; set; }

    public string? Body { get; set; }
}
